FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Bot/Bot.csproj", "Worker/"]
RUN dotnet restore "Worker/Worker.csproj" --runtime alpine-x64
COPY . .
WORKDIR "/src/Worker"
RUN dotnet build "Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Worker.csproj" -c Release -o /app/publish \
    --runtime alpine-x64 \
    --self-contained true \
    /p:PublishSingleFile=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN apk -U upgrade
RUN apk add ffmpeg
ENTRYPOINT ["./Worker"]
