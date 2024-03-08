# FFMpegAzureStorageWorker

Background FFMpeg worker using Azure Storage Queues

## Getting Started

### Prerequisites

- [.NET 8](https://dotnet.microsoft.com/download) or higher

### Configuration

| Parameter                  | Required? | Description                                                                                   |
|----------------------------|-----------|-----------------------------------------------------------------------------------------------|
| Storage__ConnectionString  | true      | Azure Storage account connection string                                                       |
| FFMpeg__Path               | true      | FFMpeg full path                                                                              |
| FFMpeg__Arguments          | true      | Arguments for FFMpeg conversion                                                               |
| Storage__Input__Container  | false     | Name of azure storage container for input files to convert. Default name: input               |
| Storage__Input__Queue      | false     | Name of azure storage queue for requests to convert file. Default name: input                 |
| Storage__Output__Container | false     | Name of azure storage container for output files after conversion. Default name: output       |
| Storage__Output__Queue     | false     | Name of azure storage queue for results of conversion. Default name: output                   |
| Delay                      | false     | Delay between job executions in hh:mm:ss format. Default: 00:01:00                            |
| FFMpeg__TargetExtension    | false     | Target extension of output file with dot. If not set - extension of source file will be taken |

## Installing

**Project:**

1. Clone project
2. Update **appsettings.json**
3. `dotnet run`

## Built With

* [azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net) - The official Azure SDK for .NET