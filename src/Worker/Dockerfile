FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine
WORKDIR /app
COPY ["Worker", "./"]

RUN apk -U upgrade
RUN apk add ffmpeg

ENTRYPOINT ["./Worker"]
