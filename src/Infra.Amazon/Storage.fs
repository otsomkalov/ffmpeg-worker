namespace Infra.Amazon

open System.IO
open System.Threading.Tasks
open Amazon.S3
open Domain.Core
open Domain.Repos
open Infra
open Microsoft.Extensions.Logging
open otsom.fs.Extensions

module Storage =
  type InputStorage(s3Client: IAmazonS3, storageSettings: StorageSettings, logger: ILogger<InputStorage>) =
    let additionalProps = dict []

    interface IInputStorage with
      member this.DownloadFile(inputFileName: string) : Task<File> = task {
        let downloadedFileExtension = Path.GetExtension inputFileName
        let downloadedFile = File.create downloadedFileExtension

        do! s3Client.DownloadToFilePathAsync(storageSettings.Input.Container, inputFileName, downloadedFile.Path, additionalProps)

        logger.LogInformation(
          "Remote input file {InputFileName} downloaded to local {DownloadedFileName}",
          inputFileName,
          downloadedFile.FullName
        )

        return downloadedFile
      }

      member this.DeleteFile(name: string) : Task<unit> = task {
        do! s3Client.DeleteObjectAsync(storageSettings.Input.Container, name) |> Task.ignore

        logger.LogInformation("Remote input file {RemoteInputFileName} deleted", name)
      }

  type OutputStorage(s3Client: IAmazonS3, storageSettings: StorageSettings, logger: ILogger<OutputStorage>) =
    let additionalProps = dict []

    interface IOutputStorage with
      member this.UploadFile(file: File) : Task<unit> = task {
        do! s3Client.UploadObjectFromFilePathAsync(storageSettings.Output.Container, file.FullName, file.Path, additionalProps)

        logger.LogInformation("Converted file {ConvertedFileName} uploaded", file.FullName)
      }