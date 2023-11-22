module Worker

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Threading.Tasks
open Azure.Storage.Blobs
open Azure.Storage.Blobs.Models
open Azure.Storage.Queues
open FSharp
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FSharp.Core

module Helpers =

  module Task =
    let map mapping task' =
      task {
        let! v = task'

        return mapping v
      }

    let bind (binder: 'a -> Task<'b>) task' =
      task {
        let! v = task'

        return! binder v
      }

    let tap (action: 'a -> unit) task' =
      task {
        let! v = task'

        action v

        return v
      }

  module Option =
    let tap action option =
      match option with
      | Some v ->
        do action v
        Some v
      | None -> None

    let taskTap action option =
      task {
        return!
          match option with
          | Some v ->
            task {
              do! action v
              return Some v
            }
          | None -> None |> Task.FromResult
      }

  module JSON =
    let private options =
      JsonFSharpOptions.Default().WithUnionUntagged().ToJsonSerializerOptions()

    let serialize value =
      JsonSerializer.Serialize(value, options)

open Helpers

[<RequireQualifiedAccess>]
module Settings =

  [<CLIMutable>]
  type StorageSettings' = { Queue: string; Container: string }

  [<CLIMutable>]
  type StorageSettings =
    { ConnectionString: string
      Input: StorageSettings'
      Output: StorageSettings' }

    static member SectionName = "Storage"

  [<CLIMutable>]
  type AppSettings = { Delay: TimeSpan }

  [<CLIMutable>]
  type FFMpegSettings =
    { Path: string
      Arguments: string
      TargetExtension: string }

    static member SectionName = "FFMpeg"

[<RequireQualifiedAccess>]
module Messages =

  [<JsonFSharpConverter>]
  type InputMessage = { Id: string; Name: string }

  [<JsonFSharpConverter>]
  type OutputMessage = { Id: string; Name: string }

[<RequireQualifiedAccess>]
module FFMpeg =
  type ConvertError = ConvertError

  type Convert = FileInfo -> Task<Result<FileInfo, ConvertError>>

  let convert (settings: Settings.FFMpegSettings) (logger: ILogger) : Convert =
    fun fileInfo ->
      let fileName = Path.GetFileNameWithoutExtension fileInfo.Name

      let targetFileName = $"{fileName}.{settings.TargetExtension}"
      let targetFilePath = Path.Combine(Path.GetTempPath(), targetFileName)

      let arguments = [ $"-i {fileInfo.FullName}"; settings.Arguments; targetFilePath ]

      let processStartInfo =
        ProcessStartInfo(
          RedirectStandardError = true,
          UseShellExecute = false,
          FileName = settings.Path,
          Arguments = String.Join(" ", arguments)
        )

      try
        task {

          Logf.logfi logger "Starting conversion of %s{InputFileName} to %s{OutputFileName}" fileInfo.Name targetFileName

          use pcs = Process.Start(processStartInfo)

          let! ffmpegOutput = pcs.StandardError.ReadToEndAsync()

          do! pcs.WaitForExitAsync()

          return
            if pcs.ExitCode = 0 then
              Logf.logfi logger "Conversion of %s{InputFileName} to %s{OutputFileName} done! FFMpeg output: %s{FFMpegOutput}" fileInfo.Name targetFileName ffmpegOutput
              let outputFileInfo = FileInfo(targetFilePath)
              Ok outputFileInfo
            else
              Logf.logfe logger "FFMpeg error: %s{FFMpegError}" ffmpegOutput
              ConvertError |> Error
        }
      with e ->
        Logf.elogfe logger e "Error during file conversion:"
        ConvertError |> Error |> Task.FromResult

[<RequireQualifiedAccess>]
module Storage =

  type GetMessage = unit -> Task<Messages.InputMessage option>

  let getMessage (storageSettings: Settings.StorageSettings) logger : GetMessage =
    fun () ->
      let queueServiceClient = QueueServiceClient(storageSettings.ConnectionString)

      let inputQueueClient =
        queueServiceClient.GetQueueClient(storageSettings.Input.Queue)

      let logMessage =
        Logf.logfi logger "Received request to convert file with id: %s{FileId}"

      inputQueueClient.ReceiveMessageAsync()
      |> Task.map Option.ofObj
      |> Task.map (Option.bind (fun m -> m.Value |> Option.ofObj))
      |> Task.map (Option.filter (fun m -> not (String.IsNullOrEmpty(m.MessageText))))
      |> Task.bind (
        Option.taskTap (fun m ->
          inputQueueClient.DeleteMessageAsync(m.MessageId, m.PopReceipt)
          |> Task.map ignore)
      )
      |> Task.map (Option.bind (fun v -> v.MessageText |> Option.ofObj))
      |> Task.map (Option.map JsonSerializer.Deserialize<Messages.InputMessage>)
      |> Task.map (Option.tap (fun im -> logMessage im.Id))

  type DownloadFile = string -> Task<FileInfo>

  let downloadFile (storageSettings: Settings.StorageSettings) logger : DownloadFile =
    fun name ->
      task {
        let blobServiceClient = BlobServiceClient(storageSettings.ConnectionString)

        let inputContainerClient =
          blobServiceClient.GetBlobContainerClient(storageSettings.Input.Container)

        let blobClient = inputContainerClient.GetBlobClient(name)
        let inputFilePath = Path.Combine(Path.GetTempPath(), name)

        Logf.logfi logger "Downloading file %s{FileName}" name

        do! blobClient.DownloadToAsync(inputFilePath) |> Task.map ignore

        Logf.logfi logger "File %s{FileName} downloaded" name

        return FileInfo(inputFilePath)
      }

  type UploadFile = FileInfo -> Task<unit>

  let uploadFile (storageSettings: Settings.StorageSettings) logger : UploadFile =
    fun fileInfo ->
      task {
        let blobServiceClient = BlobServiceClient(storageSettings.ConnectionString)

        let outputContainerClient =
          blobServiceClient.GetBlobContainerClient(storageSettings.Output.Container)

        let outputBlobClient = outputContainerClient.GetBlobClient(fileInfo.Name)

        Logf.logfi logger "Uploading file %s{FileName}" fileInfo.Name

        do! outputBlobClient.UploadAsync (fileInfo.FullName, true) |> Task.map ignore

        Logf.logfi logger "File %s{FileName} uploaded" fileInfo.Name
      }

  type ConversionResult =
    | Success of name: string
    | Error of error: string

  type ConversionResultMessage =
    { Id: string; Result: ConversionResult }

  type SendMessage = ConversionResultMessage -> Task<unit>

  let sendMessage (storageSettings: Settings.StorageSettings) : SendMessage =
    fun conversionResult ->
      task {
        let queueServiceClient = QueueServiceClient(storageSettings.ConnectionString)

        let outputQueueClient =
          queueServiceClient.GetQueueClient(storageSettings.Output.Queue)

        let message = JSON.serialize conversionResult

        do! outputQueueClient.SendMessageAsync(message) |> Task.map ignore
      }

[<RequireQualifiedAccess>]
module Workflows =
  let convert
    (getMessage: Storage.GetMessage)
    (downloadFile: Storage.DownloadFile)
    (convert: FFMpeg.Convert)
    (uploadFile: Storage.UploadFile)
    (sendMessage: Storage.SendMessage)
    =
    fun () ->
      task {
        let! message = getMessage ()

        match message with
        | Some m ->
          let! inputFileInfo = downloadFile m.Name

          let! conversionResult = convert inputFileInfo

          match conversionResult with
          | Ok outputFileInfo ->
            do! uploadFile outputFileInfo

            let message: Storage.ConversionResultMessage =
              { Id = m.Id
                Result = Storage.ConversionResult.Success outputFileInfo.Name }

            do! sendMessage message
          | Result.Error FFMpeg.ConvertError ->
            let message: Storage.ConversionResultMessage =
              { Id = m.Id
                Result = Storage.ConversionResult.Error "Error during conversion!" }

            do! sendMessage message
        | None -> ()
      }

[<RequireQualifiedAccess>]
module Worker =
  open Microsoft.Extensions.Hosting

  type Worker
    (
      logger: ILogger<Worker>,
      _storageOptions: IOptions<Settings.StorageSettings>,
      _ffmpegOptions: IOptions<Settings.FFMpegSettings>,
      _appOptions: IOptions<Settings.AppSettings>
    ) =
    inherit BackgroundService()

    let ffmpegSettings = _ffmpegOptions.Value
    let appSettings = _appOptions.Value
    let storageSettings = _storageOptions.Value

    override _.ExecuteAsync(ct: CancellationToken) =
      let convert = FFMpeg.convert ffmpegSettings logger
      let getMessage = Storage.getMessage storageSettings logger
      let downloadFile = Storage.downloadFile storageSettings logger
      let uploadFile = Storage.uploadFile storageSettings logger
      let sendMessage = Storage.sendMessage storageSettings

      let convert =
        Workflows.convert getMessage downloadFile convert uploadFile sendMessage

      task {
        while not ct.IsCancellationRequested do
          try
            do! convert ()
          with e ->
            logger.LogError("Worker error: {WorkerError}", e)

          do! Task.Delay(appSettings.Delay)
      }

module Program =
  open Microsoft.Extensions.Hosting
  open Microsoft.Extensions.DependencyInjection

  let private configureAppConfig _ (builder: IConfigurationBuilder) =
    builder.AddUserSecrets(true)

    ()

  let private configureServices (hostContext: HostBuilderContext) (services: IServiceCollection) =
    let configuration = hostContext.Configuration

    services
      .Configure<Settings.AppSettings>(configuration)
      .Configure<Settings.FFMpegSettings>(configuration.GetSection(Settings.FFMpegSettings.SectionName))
      .Configure<Settings.StorageSettings>(configuration.GetSection(Settings.StorageSettings.SectionName))

    services.AddHostedService<Worker.Worker>() |> ignore

  let private createHostBuilder args =
    Host
      .CreateDefaultBuilder(args)
      .ConfigureAppConfiguration(configureAppConfig)
      .ConfigureServices(configureServices)

  [<EntryPoint>]
  let main args =
    createHostBuilder(args).Build().Run()

    0 // exit code
