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

  let getContainerClient (storageSettings: Settings.StorageSettings) =
    let blobServiceClient = BlobServiceClient(storageSettings.ConnectionString)

    function
    | Input -> storageSettings.Input.Container
    | Output -> storageSettings.Output.Container
    >> blobServiceClient.GetBlobContainerClient

  let downloadFile settings (loggerFactory: ILoggerFactory) : RemoteStorage.DownloadFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.DownloadFile)
    let getBlobContainer = getContainerClient settings
    let inputContainerClient = getBlobContainer Input

    fun inputFileName ->
      task {
        let blobClient = inputContainerClient.GetBlobClient(inputFileName)

        let downloadedFileExtension = Path.GetExtension inputFileName
        let downloadedFile = File.create downloadedFileExtension

        do! blobClient.DownloadToAsync(downloadedFile.Path) |> Task.map ignore

        Logf.logfi logger "Remove input file %s{InputFileName} downloaded to local %s{DownloadedFileName}" inputFileName downloadedFile.FullName

        return downloadedFile
      }

  let uploadFile settings (loggerFactory: ILoggerFactory) : RemoteStorage.UploadFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.UploadFile)
    let getBlobContainer = getContainerClient settings
    let outputContainerClient = getBlobContainer Output

    fun file ->
      task {
        let outputBlobClient = outputContainerClient.GetBlobClient(file.FullName)

        do! outputBlobClient.UploadAsync(file.Path, true) |> Task.map ignore

        Logf.logfi logger "Converted file %s{ConvertedFileName} uploaded" file.FullName
      }

  let deleteFile settings (loggerFactory: ILoggerFactory) : RemoteStorage.DeleteFile =
    let logger = loggerFactory.CreateLogger(nameof RemoteStorage.DeleteFile)
    let getBlobContainer = getContainerClient settings
    let inputContainerClient = getBlobContainer Input

    fun name ->
      task {
        let inputBlobContainer = inputContainerClient.GetBlobClient(name)

        do! inputBlobContainer.DeleteIfExistsAsync() |> Task.map ignore

        Logf.logfi logger "Remote input file %s{RemoteInputFileName} deleted" name
      }
