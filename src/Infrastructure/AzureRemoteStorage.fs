namespace Infrastructure

open System.IO
open System.Threading.Tasks
open Azure.Storage.Blobs
open Domain
open Domain.Core
open Microsoft.Extensions.Logging
open Infrastructure.Settings
open otsom.fs.Extensions
open Infrastructure.Core

type AzureRemoteStorage
  (
    blobServiceClient: BlobServiceClient,
    storageSettings: StorageSettings,
    logger: ILogger<AzureRemoteStorage>
  ) =

  let inputContainerClient = blobServiceClient.GetBlobContainerClient(storageSettings.Input.Container)
  let outputContainerClient = blobServiceClient.GetBlobContainerClient(storageSettings.Output.Container)

  interface Repos.IRemoteStorage with
    member this.DownloadFile(inputFileName: string): Task<File> =
      task {
        let blobClient = inputContainerClient.GetBlobClient(inputFileName)

        let downloadedFileExtension = Path.GetExtension inputFileName
        let downloadedFile = File.create downloadedFileExtension

        do! blobClient.DownloadToAsync(downloadedFile.Path) |> Task.map ignore

        logger.LogInformation(
          "Remote input file {InputFileName} downloaded to local {DownloadedFileName}",
          inputFileName,
          downloadedFile.FullName)

        return downloadedFile
      }

    member this.UploadFile(file: File): Task<unit> =
      task {
        let outputBlobClient = outputContainerClient.GetBlobClient(file.FullName)

        do! outputBlobClient.UploadAsync(file.Path, true) |> Task.map ignore

        logger.LogInformation("Converted file {ConvertedFileName} uploaded", file.FullName)
      }

    member this.DeleteFile(name: string): Task<unit> =
      task {
        let inputBlobContainer = inputContainerClient.GetBlobClient(name)

        do! inputBlobContainer.DeleteIfExistsAsync() |> Task.map ignore

        logger.LogInformation("Remote input file {RemoteInputFileName} deleted", name)
      }
