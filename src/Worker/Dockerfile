FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Worker/Worker.fsproj", "Worker/"]
RUN dotnet restore "Worker/Worker.fsproj" --runtime linux-musl-x64
COPY . .
WORKDIR "/src/Worker"
RUN dotnet build "Worker.fsproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Worker.fsproj" -c Release -o /app/publish \
    --runtime linux-musl-x64 \
    --self-contained true \
    /p:PublishSingleFile=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN apk -U upgrade
RUN apk add ffmpeg
ENTRYPOINT ["./Worker"]
