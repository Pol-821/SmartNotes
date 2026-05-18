FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SmartNotes.slnx .
COPY SmartNotes.Api/SmartNotes.Api.csproj SmartNotes.Api/
COPY SmartNotes.Whisper/SmartNotes.Whisper.csproj SmartNotes.Whisper/
COPY SmartNotes.Whisper.Test/SmartNotes.Whisper.Test.csproj SmartNotes.Whisper.Test/

RUN dotnet restore SmartNotes.slnx

COPY . .

RUN dotnet publish SmartNotes.Api/SmartNotes.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*

EXPOSE 80

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SmartNotes.Api.dll"]
