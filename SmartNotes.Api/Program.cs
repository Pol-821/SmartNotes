using AspNetCoreRateLimit;
using SmartNotes.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using SmartNotes.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using SmartNotes.Api.Services.AI;
using Amazon.S3;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new() { Endpoint = "*/api/auth/login", Limit = 10, Period = "1m" },
        new() { Endpoint = "*/api/auth/register", Limit = 5, Period = "1m" },
        new() { Endpoint = "*/api/auth/forgot-password", Limit = 3, Period = "1h" },
        new() { Endpoint = "*/api/auth/reset-password", Limit = 3, Period = "1h" },
    };
});
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// Port dinàmic per Render (variable d'entorn PORT)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200L * 1024L * 1024L; // 200 MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 200L * 1024L * 1024L; // 200 MB
});

builder.Services.AddSingleton<AudioPreprocessor>();
builder.Services.AddSingleton<FfmpegRunner>();

builder.Services.AddSingleton<TranscriptionQueue>();
builder.Services.AddSingleton<TranscriptionStore>();
builder.Services.AddHostedService<TranscriptionWorker>();

// Groq:ApiKey es carrega des de variables d'entorn (appsettings.json o env vars)
var groqApiKey = builder.Configuration["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey no configurada");
var groqModel = builder.Configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

builder.Services.AddHttpClient("GroqChat", client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {groqApiKey}");
    client.BaseAddress = new Uri("https://api.groq.com");
});
builder.Services.AddSingleton<GroqClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new GroqClient(factory.CreateClient("GroqChat"), groqModel, sp.GetRequiredService<ILogger<GroqClient>>());
});

builder.Services.AddHttpClient("GroqAudio", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {groqApiKey}");
    client.BaseAddress = new Uri("https://api.groq.com");
});
builder.Services.AddSingleton<GroqAudioClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new GroqAudioClient(factory.CreateClient("GroqAudio"), sp.GetRequiredService<ILogger<GroqAudioClient>>(), sp.GetRequiredService<FfmpegRunner>());
});

builder.Services.AddSingleton<SmartNotesEngine>();

builder.Services.AddDbContext<SmartNotesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Introdueix el token així: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});


builder.Services.AddEndpointsApiExplorer();

builder.Services.AddScoped<UserService>(); 
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<EmailService>();

builder.Services.AddSingleton<WhisperService>();

builder.Services.AddScoped<NoteService>();

builder.Services.AddScoped<ClassroomService>();

// Orígens CORS: appsettings.json + env var override + hardcoded locals
var corsOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()?.ToList() ?? new();
corsOrigins.Add("http://localhost:3000");
corsOrigins.Add("http://localhost:5173");
var envOrigins = Environment.GetEnvironmentVariable("AllowedOrigins");
if (!string.IsNullOrEmpty(envOrigins))
{
    corsOrigins.AddRange(envOrigins.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}
corsOrigins = corsOrigins.Distinct().ToList();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins(corsOrigins.ToArray())
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var r2Config = builder.Configuration.GetSection("CloudflareR2");
var s3Config = new AmazonS3Config
{
    ServiceURL = r2Config["ServiceUrl"],
    ForcePathStyle = true 
};

builder.Services.AddSingleton<IAmazonS3>(sp => 
    new AmazonS3Client(r2Config["AccessKey"], r2Config["SecretKey"], s3Config)
);

builder.Services.AddSingleton<R2Service>();

var app = builder.Build();

app.UseCors("AllowReactApp");

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseHttpsRedirection();

app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartNotesDbContext>();
    
    if (!db.SubscriptionPlans.Any())
    {
        db.SubscriptionPlans.AddRange(new[]
        {
            new SmartNotes.Api.Models.SubscriptionPlan { Name = "Free", Description = "Per estudiants casuals. 4 hores d'àudio al mes.", PriceMonthly = 0m, SecondsPerMonth = 14400, IsActive = true },
            new SmartNotes.Api.Models.SubscriptionPlan { Name = "Pro", Description = "Per estudiants seriosos. 40 hores d'àudio al mes.", PriceMonthly = 9.99m, SecondsPerMonth = 144000, IsActive = true },
            new SmartNotes.Api.Models.SubscriptionPlan { Name = "Enterprise", Description = "Per professionals. 100 hores d'àudio al mes.", PriceMonthly = 24.99m, SecondsPerMonth = 360000, IsActive = true }
        });
        db.SaveChanges();
    }
}

app.Run();