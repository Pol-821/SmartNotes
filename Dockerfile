FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SmartNotes.Api/SmartNotes.Api.csproj SmartNotes.Api/

RUN dotnet restore SmartNotes.Api/SmartNotes.Api.csproj

COPY . .

RUN dotnet publish SmartNotes.Api/SmartNotes.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*

EXPOSE 80

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SmartNotes.Api.dll"]
