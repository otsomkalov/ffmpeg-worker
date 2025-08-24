namespace Domain

open System.Threading.Tasks
open Domain.Core
open Domain.Repos

module Workflows =
  [<RequireQualifiedAccess>]
  module LocalStorage =
    type DeleteFile = File -> unit

  [<RequireQualifiedAccess>]
  module Converter =
    type ConvertError = ConvertError
    type Convert = File -> Task<Result<File, ConvertError>>

  [<RequireQualifiedAccess>]
  module Conversion =
    type RunIO =
      { Convert: Converter.Convert
        DeleteLocalFile: LocalStorage.DeleteFile }

    let run (remoteStorage: IRemoteStorage) (queue: IQueue) (msgClient: IMessageClient) (io: RunIO) : Conversion.Run =
      fun req ->
        task {
          let! inputFile = remoteStorage.DownloadFile req.Name

          let! conversionResult = io.Convert inputFile

          match conversionResult with
          | Ok outputFile ->
            do! remoteStorage.UploadFile outputFile
            do! remoteStorage.DeleteFile req.Name
            do io.DeleteLocalFile inputFile
            do io.DeleteLocalFile outputFile
            do! queue.SendSuccessMessage(req.OperationId, req.Id, outputFile.FullName)
            do! msgClient.Delete()
          | Result.Error Converter.ConvertError ->
            do io.DeleteLocalFile inputFile
            do! queue.SendFailureMessage(req.OperationId, req.Id)
            do! msgClient.Delete()
        }