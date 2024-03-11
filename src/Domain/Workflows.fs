namespace Domain

open System.Threading.Tasks
open Domain.Core
open otsom.fs.Extensions

module Workflows =
  [<RequireQualifiedAccess>]
  module RemoteStorage =
    type DownloadFile = string -> Task<File>
    type UploadFile = File -> Task<unit>
    type DeleteFile = string -> Task<unit>

  [<RequireQualifiedAccess>]
  module LocalStorage =
    type DeleteFile = File -> unit

  [<RequireQualifiedAccess>]
  module Converter =
    type ConvertError = ConvertError
    type Convert = File -> Task<Result<File, ConvertError>>

  [<RequireQualifiedAccess>]
  module Queue =
    type SendSuccessMessage = string -> Task<unit>
    type SendFailureMessage = unit -> Task<unit>
    type DeleteMessage = unit -> Task<unit>

  [<RequireQualifiedAccess>]
  module Conversion =
    type RunIO =
      { DownloadFile: RemoteStorage.DownloadFile
        Convert: Converter.Convert
        UploadFile: RemoteStorage.UploadFile
        DeleteRemoteFile: RemoteStorage.DeleteFile
        DeleteLocalFile: LocalStorage.DeleteFile
        SendSuccessMessage: Queue.SendSuccessMessage
        SendFailureMessage: Queue.SendFailureMessage
        DeleteInputMessage: Queue.DeleteMessage }

    let private convertFile io =
      fun inputFile ->
        io.Convert inputFile
        |> Task.bind (function
          | Ok outputFile ->
            task {
              do! io.UploadFile outputFile
              do! io.DeleteRemoteFile inputFile.FullName
              do io.DeleteLocalFile inputFile
              do io.DeleteLocalFile outputFile
              do! io.SendSuccessMessage outputFile.FullName
              do! io.DeleteInputMessage()
            }
          | Result.Error Converter.ConvertError ->
            task {
              do io.DeleteLocalFile inputFile
              do! io.SendFailureMessage()
              do! io.DeleteInputMessage()
            })

    let run (io: RunIO) : Conversion.Run =
      let convertFile = convertFile io

      fun req ->
        io.DownloadFile req.Name |> Task.bind convertFile
