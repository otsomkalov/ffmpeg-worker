namespace Infra.Azure.Storage

open System.IO
open System.Threading.Tasks
open Azure.Storage.Blobs
open Domain.Core
open Domain.Repos
open Infra
open Infra.Azure
open Microsoft.Extensions.Logging
open otsom.fs.Extensions
open FsToolkit.ErrorHandling

type InputStorage(blobServiceClient: BlobServiceClient, storageSettings: StorageSettings, logger: ILogger<InputStorage>) =
  let containerClient =
    blobServiceClient.GetBlobContainerClient(storageSettings.Input.Container)

  interface IInputStorage with
    member this.DownloadFile(inputFileName: string) : Task<File> = task {
      let blobClient = containerClient.GetBlobClient(inputFileName)

      let downloadedFileExtension = Path.GetExtension inputFileName
      let downloadedFile = File.create downloadedFileExtension

      do! blobClient.DownloadToAsync(downloadedFile.Path) |> Task.map ignore

      logger.LogInformation(
        "Remote input file {InputFileName} downloaded to local {DownloadedFileName}",
        inputFileName,
        downloadedFile.FullName
      )

      return downloadedFile
    }

    member this.DeleteFile(name: string) : Task<unit> = task {
      let inputBlobContainer = containerClient.GetBlobClient(name)

      do! inputBlobContainer.DeleteIfExistsAsync() |> Task.map ignore

      logger.LogInformation("Remote input file {RemoteInputFileName} deleted", name)
    }

type OutputStorage(blobServiceClient: BlobServiceClient, storageSettings: StorageSettings, logger: ILogger<OutputStorage>) =
  let outputContainer =
    blobServiceClient.GetBlobContainerClient(storageSettings.Output.Container)

  interface IOutputStorage with
    member this.UploadFile(file: File) : Task<unit> = task {
      let outputBlobClient = outputContainer.GetBlobClient(file.FullName)

      do! outputBlobClient.UploadAsync(file.Path, true) |> Task.map ignore

      logger.LogInformation("Converted file {ConvertedFileName} uploaded", file.FullName)
    }