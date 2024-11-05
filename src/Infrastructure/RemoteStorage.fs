namespace Infrastructure

open System.IO
open Azure.Storage.Blobs
open FSharp
open Microsoft.Extensions.Logging
open otsom.fs.Extensions
open Domain.Workflows
open Infrastructure.Core

module RemoteStorage =
  let downloadFile
    (client: BlobServiceClient)
    (settings: Settings.StorageSettings)
    (loggerFactory: ILoggerFactory)
    : RemoteStorage.DownloadFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.DownloadFile)
    let inputContainerClient = client.GetBlobContainerClient(settings.Input.Container)

    fun inputFileName ->
      task {
        let blobClient = inputContainerClient.GetBlobClient(inputFileName)

        let downloadedFileExtension = Path.GetExtension inputFileName
        let downloadedFile = File.create downloadedFileExtension

        do! blobClient.DownloadToAsync(downloadedFile.Path) |> Task.map ignore

        Logf.logfi
          logger
          "Remote input file %s{InputFileName} downloaded to local %s{DownloadedFileName}"
          inputFileName
          downloadedFile.FullName

        return downloadedFile
      }

  let uploadFile
    (client: BlobServiceClient)
    (settings: Settings.StorageSettings)
    (loggerFactory: ILoggerFactory)
    : RemoteStorage.UploadFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.UploadFile)
    let outputContainerClient = client.GetBlobContainerClient(settings.Output.Container)

    fun file ->
      task {
        let outputBlobClient = outputContainerClient.GetBlobClient(file.FullName)

        do! outputBlobClient.UploadAsync(file.Path, true) |> Task.map ignore

        Logf.logfi logger "Converted file %s{ConvertedFileName} uploaded" file.FullName
      }

  let deleteFile
    (client: BlobServiceClient)
    (settings: Settings.StorageSettings)
    (loggerFactory: ILoggerFactory)
    : RemoteStorage.DeleteFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.DeleteFile)
    let inputContainerClient = client.GetBlobContainerClient(settings.Input.Container)

    fun name ->
      task {
        let inputBlobContainer = inputContainerClient.GetBlobClient(name)

        do! inputBlobContainer.DeleteIfExistsAsync() |> Task.map ignore

        Logf.logfi logger "Remote input file %s{RemoteInputFileName} deleted" name
      }
