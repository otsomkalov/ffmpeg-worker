namespace Infrastructure

open System.IO
open Azure.Storage.Blobs
open FSharp
open Microsoft.Extensions.Logging
open otsom.fs.Extensions
open Domain.Workflows
open Infrastructure.Core

module RemoteStorage =
  type ContainerType =
    | Input
    | Output

  let getContainerClient (client: BlobServiceClient) (storageSettings: Settings.StorageSettings) =
    function
    | Input -> storageSettings.Input.Container
    | Output -> storageSettings.Output.Container
    >> client.GetBlobContainerClient

  let downloadFile client settings (loggerFactory: ILoggerFactory) : RemoteStorage.DownloadFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.DownloadFile)
    let inputContainerClient = getContainerClient client settings Input

    fun inputFileName ->
      task {
        let blobClient = inputContainerClient.GetBlobClient(inputFileName)

        let downloadedFileExtension = Path.GetExtension inputFileName
        let downloadedFile = File.create downloadedFileExtension

        do! blobClient.DownloadToAsync(downloadedFile.Path) |> Task.map ignore

        Logf.logfi logger "Remote input file %s{InputFileName} downloaded to local %s{DownloadedFileName}" inputFileName downloadedFile.FullName

        return downloadedFile
      }

  let uploadFile client settings (loggerFactory: ILoggerFactory) : RemoteStorage.UploadFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.UploadFile)
    let outputContainerClient = getContainerClient client settings Output

    fun file ->
      task {
        let outputBlobClient = outputContainerClient.GetBlobClient(file.FullName)

        do! outputBlobClient.UploadAsync(file.Path, true) |> Task.map ignore

        Logf.logfi logger "Converted file %s{ConvertedFileName} uploaded" file.FullName
      }

  let deleteFile client settings (loggerFactory: ILoggerFactory) : RemoteStorage.DeleteFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.DeleteFile)
    let inputContainerClient = getContainerClient client settings Input

    fun name ->
      task {
        let inputBlobContainer = inputContainerClient.GetBlobClient(name)

        do! inputBlobContainer.DeleteIfExistsAsync() |> Task.map ignore

        Logf.logfi logger "Remote input file %s{RemoteInputFileName} deleted" name
      }
