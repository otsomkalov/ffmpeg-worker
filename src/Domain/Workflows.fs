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

    let run
      (inputStorage: IInputStorage)
      (outputStorage: IOutputStorage)
      (inputMsgClient: IInputMsgClient)
      (outputQueue: IOutputQueue)
      (io: RunIO)
      : Conversion.Run =
      fun req -> task {
        let! inputFile = inputStorage.DownloadFile req.Name

        let! conversionResult = io.Convert inputFile

        match conversionResult with
        | Ok outputFile ->
          do! outputStorage.UploadFile outputFile
          do! inputStorage.DeleteFile req.Name
          do io.DeleteLocalFile inputFile
          do io.DeleteLocalFile outputFile
          do! outputQueue.SendSuccessMessage(outputFile.FullName)
          do! inputMsgClient.Delete()
        | Result.Error Converter.ConvertError ->
          do io.DeleteLocalFile inputFile
          do! outputQueue.SendFailureMessage()
          do! inputMsgClient.Delete()
      }