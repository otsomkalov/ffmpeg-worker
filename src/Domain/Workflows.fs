namespace Domain

open System
open System.Threading.Tasks
open Domain.Repos
open Domain.Settings
open Microsoft.Extensions.Options

type ConvertError = ConvertError

type IConverter =
  abstract Convert: File * string -> Task<Result<File, ConvertError>>

module Workflows =
  [<RequireQualifiedAccess>]
  module LocalStorage =
    type DeleteFile = File -> unit

  [<RequireQualifiedAccess>]
  module Conversion =
    let run
      (inputStorage: IInputStorage)
      (outputStorage: IOutputStorage)
      (inputMsgClient: IInputMsgClient)
      (outputQueue: IOutputQueue)
      (settings: IOptions<AppSettings>)
      (converter: IConverter)
      (deleteLocalFile: LocalStorage.DeleteFile)
      : Conversion.Run =
      let settings = settings.Value

      fun req -> task {
        let! inputFile = inputStorage.DownloadFile req.Name

        let targetExtension =
          settings.TargetExtension
          |> Option.ofObj
          |> Option.filter (String.IsNullOrEmpty >> not)
          |> Option.defaultValue inputFile.Extension

        let! conversionResult = converter.Convert(inputFile, targetExtension)

        match conversionResult with
        | Ok outputFile ->
          do! outputStorage.UploadFile outputFile
          do! inputStorage.DeleteFile req.Name
          do deleteLocalFile inputFile
          do deleteLocalFile outputFile
          do! outputQueue.SendSuccessMessage(outputFile.FullName)
          do! inputMsgClient.Delete()
        | Result.Error ConvertError ->
          do deleteLocalFile inputFile
          do! outputQueue.SendFailureMessage()
          do! inputMsgClient.Delete()
      }