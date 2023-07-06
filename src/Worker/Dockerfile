FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine
WORKDIR /app
COPY . .

RUN apk -U upgrade
RUN apk add ffmpeg

RUN chmod +x ./Worker

ENTRYPOINT ["./Worker"]
