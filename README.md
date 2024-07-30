# FFMpegAzureStorageWorker

Background FFMpeg worker using Azure Storage Queues

## Configure

The next environment variables are used by an app:

| Parameter                             | Required? | Description                                                                                   |
|---------------------------------------|-----------|-----------------------------------------------------------------------------------------------|
| APPLICATIONINSIGHTS_CONNECTION_STRING | false     | Azure App Insights connection string for logging & tracebility                                |
| Storage__ConnectionString             | true      | Azure Storage account connection string                                                       |
| FFMpeg__Path                          | true      | FFMpeg full path                                                                              |
| FFMpeg__Arguments                     | true      | Arguments for FFMpeg conversion                                                               |
| Storage__Input__Container             | false     | Name of azure storage container for input files to convert. Default name: input               |
| Storage__Input__Queue                 | false     | Name of azure storage queue for requests to convert file. Default name: input                 |
| Storage__Output__Container            | false     | Name of azure storage container for output files after conversion. Default name: output       |
| Storage__Output__Queue                | false     | Name of azure storage queue for results of conversion. Default name: output                   |
| Delay                                 | false     | Delay between job executions in hh:mm:ss format. Default: 00:01:00                            |
| Name                                  | true      | Name of an application instance for traceability                                              |
| FFMpeg__TargetExtension               | false     | Target extension of output file with dot. If not set - extension of source file will be taken |

## Deploy

### Docker

Use [pre-build image](https://hub.docker.com/repository/docker/infinitu1327/ffmpeg-azure-storage-worker/general) from DockerHub.

There are multiple tags available:
- `nightly` - latest build for PR
- `stable` - latest `main` version
- `x.x.xxxx-alpha` - image that corresponds to a particular PR build in Azure DevOps
- `x.x.xxxx` - image that corresponds to a particular `main` branch build in Azure DevOps

## Local development

### Prerequisites

- [.NET 8](https://dotnet.microsoft.com/download) or higher
- [FFmpeg](https://ffmpeg.org/download.html)

### Running

**Project:**

1. Clone project
2. Update **appsettings.json** or environment variables
3. `dotnet run` or your favotire IDE

## Built With

* [azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net) - The official Azure SDK for .NET