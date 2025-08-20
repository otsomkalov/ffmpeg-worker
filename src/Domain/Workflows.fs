namespace Domain

open System.Threading.Tasks
open Domain.Core
open otsom.fs.Extensions
open Domain

module Workflows =
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
      { Convert: Converter.Convert
        DeleteLocalFile: LocalStorage.DeleteFile
        SendSuccessMessage: Queue.SendSuccessMessage
        SendFailureMessage: Queue.SendFailureMessage
        DeleteInputMessage: Queue.DeleteMessage }

    let run (remoteStorage: Repos.IRemoteStorage) (io: RunIO) : Conversion.Run =
      fun req ->
        remoteStorage.DownloadFile req.Name
        |> Task.bind (fun inputFile ->
          io.Convert inputFile
          |> Task.bind (function
            | Ok outputFile ->
              task {
                do! remoteStorage.UploadFile outputFile
                do! remoteStorage.DeleteFile req.Name
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
              }))
