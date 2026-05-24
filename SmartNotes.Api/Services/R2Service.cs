using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace SmartNotes.Api.Services;

public class R2Service
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly ILogger<R2Service> _logger;

    public R2Service(IAmazonS3 s3, IConfiguration config, ILogger<R2Service> logger)
    {
        _s3 = s3;
        _bucketName = config["CloudflareR2:Bucket"] ?? "smartnotes";
        _logger = logger;
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var key = $"{Guid.NewGuid()}_{fileName}";
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType,
            DisablePayloadSigning = true
        };
        await _s3.PutObjectAsync(request, ct);
        return key;
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };
        var response = await _s3.GetObjectAsync(request, ct);
        return new DisposableResponseStream(response.ResponseStream, response);
    }

    private sealed class DisposableResponseStream : Stream
    {
        private readonly Stream _inner;
        private readonly IDisposable _response;

        public DisposableResponseStream(Stream inner, IDisposable response)
        {
            _inner = inner;
            _response = response;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public async Task DownloadToFileAsync(string key, string localPath, CancellationToken ct = default)
    {
        using var stream = await DownloadAsync(key, ct);
        using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };
        await _s3.DeleteObjectAsync(request, ct);
    }

    public string GeneratePresignedUrl(string key, int expirationMinutes = 60)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            Verb = HttpVerb.GET
        };
        return _s3.GetPreSignedURL(request);
    }

    public string GeneratePresignedUrl(string key, TimeSpan expiration)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiration),
            Verb = HttpVerb.GET
        };
        return _s3.GetPreSignedURL(request);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };
            await _s3.GetObjectMetadataAsync(request, ct);
            return true;
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
