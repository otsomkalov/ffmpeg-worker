# FFMpegAzureStorageWorker

Background FFMpeg worker powered by Azure Storage Blob/Queue or AWS S3/SQS

## Configure

The next environment variables are used by an app and shared between both Azure and AWS versions:

| Parameter                             | Required? | Description                                                                                   |
|---------------------------------------|-----------|-----------------------------------------------------------------------------------------------|
| APPLICATIONINSIGHTS_CONNECTION_STRING | false     | Azure App Insights connection string for logging & tracebility                                |
| FFMpeg__Path                          | true      | FFMpeg full path                                                                              |
| FFMpeg__Arguments                     | true      | Arguments for FFMpeg conversion                                                               |
| Storage__Input__Container             | false     | Name of azure storage container for input files to convert. Default name: input               |
| Storage__Input__Queue                 | false     | Name of azure storage queue for requests to convert file. Default name: input                 |
| Storage__Output__Container            | false     | Name of azure storage container for output files after conversion. Default name: output       |
| Storage__Output__Queue                | false     | Name of azure storage queue for results of conversion. Default name: output                   |
| Delay                                 | false     | Delay between job executions in hh:mm:ss format. Default: 00:01:00                            |
| Name                                  | true      | Name of an application instance for traceability                                              |
| FFMpeg__TargetExtension               | false     | Target extension of output file with dot. If not set - extension of source file will be taken |

### Azure

| Parameter                             | Required? | Description                                                                                   |
|---------------------------------------|-----------|-----------------------------------------------------------------------------------------------|
| Storage__ConnectionString             | true      | Azure Storage account connection string                                                       |

### AWS

Refer to the AWS guide in setting up a connection. No additional parameters are required.

## Deploy

### Docker

Use [pre-built image](https://hub.docker.com/repository/docker/infinitu1327/ffmpeg-azure-storage-worker/general) from DockerHub.

There are multiple tags available:
- `aws-nightly` - latest AWS build for PR
- `az-nightly` - latest Azure build for PR
- `aws-stable` - latest AWS build for `main` branch
- `az-stable` - latest Azure build for `main` branch
- `x.x.xxxx-aws-alpha` - image for a particular AWS PR build in Azure DevOps
- `x.x.xxxx-az-alpha` - image for a particular Azure PR build in Azure DevOps
- `x.x.xxxx-aws` - image for a particular AWS `main` branch build in Azure DevOps
- `x.x.xxxx-az` - image for a particular Azure `main` branch build in Azure DevOps

## Local development

### Prerequisites

- [.NET 9](https://dotnet.microsoft.com/download) or higher
- [FFmpeg](https://ffmpeg.org/download.html)

### Running

**Project:**

1. Clone project
2. Update **appsettings.json**/**secrets.json** or set environment variables
3. Add `AZ`/`AWS` property to MsBuild `DefineConstants`
4. `dotnet run` or your favorite IDE

## Built With

* [azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net) - The official Azure SDK for .NET
* [aws-sdk-net](https://github.com/aws/aws-sdk-net) - The official AWS SDK for .NET.