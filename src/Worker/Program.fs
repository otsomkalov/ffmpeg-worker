namespace Worker

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Threading.Tasks
open Azure.Storage.Blobs
open Azure.Storage.Queues
open Azure.Storage.Queues.Models
open FSharp
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FSharp.Core
open Worker.Domain
open otsom.FSharp.Extensions
open otsom.FSharp.Extensions.ServiceCollection

module Helpers =

  module Task =
    let tap (action: 'a -> unit) task' =
      task {
        let! v = task'

        action v

        return v
      }

  module Option =
    let taskMap mapping =
      function
      | Some v -> mapping v |> Task.map Some
      | None -> None |> Task.FromResult

  module JSON =
    let private options =
      JsonFSharpOptions.Default().WithUnionUntagged().ToJsonSerializerOptions()

    let serialize value =
      JsonSerializer.Serialize(value, options)

    let deserialize<'a> (str: string) =
      JsonSerializer.Deserialize<'a>(str, options)

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

  type Convert = File -> Task<Result<File, ConvertError>>

  let convertFile (settings: Settings.FFMpegSettings) (logger: ILogger) : Convert =
    fun file ->
      let targetExtension = settings.TargetExtension |> Option.ofObj |> Option.defaultValue file.Extension

      let outputFile = File.create targetExtension

      let arguments = [ $"-i {file.Path}"; settings.Arguments; outputFile.Path ]

      let processStartInfo =
        ProcessStartInfo(
          RedirectStandardError = true,
          UseShellExecute = false,
          FileName = settings.Path,
          Arguments = String.Join(" ", arguments)
        )

      try
        task {
          Logf.logfi logger "Starting conversion of %s{InputFileName} to %s{OutputFileName}" file.FullName outputFile.FullName

          use pcs = Process.Start(processStartInfo)

          let! ffmpegOutput = pcs.StandardError.ReadToEndAsync()

          do! pcs.WaitForExitAsync()

          return
            if pcs.ExitCode = 0 then
              Logf.logfi
                logger
                "Conversion of %s{InputFileName} to %s{OutputFileName} done! FFMpeg output: %s{FFMpegOutput}"
                file.FullName
                outputFile.FullName
                ffmpegOutput

              outputFile |> Ok
            else
              Logf.logfe logger "FFMpeg error: %s{FFMpegError}" ffmpegOutput
              ConvertError |> Error
        }
      with e ->
        Logf.elogfe logger e "Error during file conversion:"
        ConvertError |> Error |> Task.FromResult

[<RequireQualifiedAccess>]
module Queue =
  type QueueType =
    | Input
    | Output

  type GetQueueClient = QueueType -> QueueClient

  let getQueueClient (settings: Settings.StorageSettings) =
    let queueServiceClient = QueueServiceClient(settings.ConnectionString)

    function
    | Input -> settings.Input.Container
    | Output -> settings.Output.Container
    >> queueServiceClient.GetQueueClient

  type GetInputMessage = unit -> Task<QueueMessage option>

  let getInputMessage (getQueueClient: GetQueueClient) : GetInputMessage =
    let inputQueueClient = getQueueClient Input

    inputQueueClient.ReceiveMessageAsync
    >> Task.map Option.ofObj
    >> Task.map (Option.bind (fun m -> m.Value |> Option.ofObj))
    >> Task.map (Option.filter (fun m -> not (String.IsNullOrEmpty(m.MessageText))))

  type DeleteInputMessage = string * string -> Task<unit>

  let deleteInputMessage (getQueueClient: GetQueueClient) : DeleteInputMessage =
    let inputQueueClient = getQueueClient Input

    fun (id, popReceipt) -> inputQueueClient.DeleteMessageAsync(id, popReceipt) |> Task.map ignore

  type ConversionResult =
    | Success of name: string
    | Error of error: string

  type ConversionResultMessage =
    { Id: string; Result: ConversionResult }

  let private sendOutputMessage (getQueueClient: GetQueueClient) =
    let outputQueueClient = getQueueClient Output

    JSON.serialize >> outputQueueClient.SendMessageAsync >> Task.map ignore

  type SendSuccessMessage = string -> string -> Task<unit>

  let sendSuccessMessage getQueueClient : SendSuccessMessage =
    fun id name ->
      let message: ConversionResultMessage =
        { Id = id
          Result = ConversionResult.Success name }

      sendOutputMessage getQueueClient message

  type SendFailureMessage = string -> Task<unit>

  let sendFailureMessage getQueueClient : SendFailureMessage =
    fun id ->
      let message: ConversionResultMessage =
        { Id = id
          Result = ConversionResult.Error "Error during conversion!" }

      sendOutputMessage getQueueClient message

[<RequireQualifiedAccess>]
module Storage =
  type ContainerType =
    | Input
    | Output

  type GetContainerClient = ContainerType -> BlobContainerClient

  type DownloadInputFile = string -> Task<File>

  let getContainerClient (storageSettings: Settings.StorageSettings) : GetContainerClient =
    let blobServiceClient = BlobServiceClient(storageSettings.ConnectionString)

    function
    | Input -> storageSettings.Input.Container
    | Output -> storageSettings.Output.Container
    >> blobServiceClient.GetBlobContainerClient

  let downloadInputFile (getBlobContainer: GetContainerClient) logger : DownloadInputFile =
    let inputContainerClient = getBlobContainer Input

    fun inputFileName ->
      task {
        let blobClient = inputContainerClient.GetBlobClient(inputFileName)

        let downloadedFileExtension = Path.GetExtension inputFileName
        let downloadedFile = File.create downloadedFileExtension

        Logf.logfi logger "Downloading input file %s{InputFileName} to %s{DownloadedFileName}" inputFileName downloadedFile.FullName

        do! blobClient.DownloadToAsync(downloadedFile.Path) |> Task.map ignore

        Logf.logfi logger "Input file %s{InputFileName} downloaded to %s{DownloadedFileName}" inputFileName downloadedFile.FullName

        return downloadedFile
      }

  type UploadOutputFile = File -> Task<unit>

  let uploadOutputFile (getBlobContainer: GetContainerClient) logger : UploadOutputFile =
    let outputContainerClient = getBlobContainer Output

    fun file ->
      task {
        let outputBlobClient = outputContainerClient.GetBlobClient(file.FullName)

        Logf.logfi logger "Uploading output file %s{ConvertedFileName}" file.FullName

        do! outputBlobClient.UploadAsync(file.Path, true) |> Task.map ignore

        Logf.logfi logger "Output file %s{ConvertedFileName} uploaded" file.FullName
      }

  type DeleteInputFile = string -> Task<unit>

  let deleteInputFile (getBlobContainer: GetContainerClient) logger : DeleteInputFile =
    let inputContainerClient = getBlobContainer Input

    fun name ->
      task {
        let inputBlobContainer = inputContainerClient.GetBlobClient(name)

        Logf.logfi logger "Deleting input file %s{InputFileName}" name

        do! inputBlobContainer.DeleteIfExistsAsync() |> Task.map ignore

        Logf.logfi logger "Input file %s{InputFileName} deleted" name
      }

[<RequireQualifiedAccess>]
module Files =
  type DeleteDownloadedFile = string -> unit

  let deleteDownloadedFile: DeleteDownloadedFile = fun path -> File.Delete(path)

  type DeleteConvertedFile = string -> unit

  let deleteConvertedFile: DeleteConvertedFile = fun path -> File.Delete(path)

[<RequireQualifiedAccess>]
module Workflows =
  let convert
    (getInputMessage: Queue.GetInputMessage)
    (downloadInputFile: Storage.DownloadInputFile)
    (convert: FFMpeg.Convert)
    (uploadOutputFile: Storage.UploadOutputFile)
    (deleteInputMessage: Queue.DeleteInputMessage)
    (deleteInputFile: Storage.DeleteInputFile)
    (deleteDownloadedFile: Files.DeleteDownloadedFile)
    (deleteConvertedFile: Files.DeleteConvertedFile)
    (sendSuccessMessage: Queue.SendSuccessMessage)
    (sendFailureMessage: Queue.SendFailureMessage)
    : unit -> Task<unit> =
    let processQueueMessage (m: QueueMessage) =
      task {
        let inputMessage = JSON.deserialize<Messages.InputMessage> m.MessageText

        let! inputFileInfo = downloadInputFile inputMessage.Name

        let! conversionResult = convert inputFileInfo

        do!
          match conversionResult with
          | Ok outputFile ->
            task {
              do! uploadOutputFile outputFile
              do! deleteInputFile inputMessage.Name
              do deleteDownloadedFile inputFileInfo.Path
              do deleteConvertedFile outputFile.Path
              do! sendSuccessMessage inputMessage.Id outputFile.FullName
            }
          | Result.Error FFMpeg.ConvertError -> sendFailureMessage inputMessage.Id

        do! deleteInputMessage (m.MessageId, m.PopReceipt)

        return ()
      }

    fun () ->
      getInputMessage ()
      |> Task.bind (Option.taskMap processQueueMessage)
      |> Task.map (Option.defaultWith id)

[<RequireQualifiedAccess>]
module Worker =
  open Microsoft.Extensions.Hosting

  type Worker
    (
      logger: ILogger<Worker>,
      ffmpegSettings: Settings.FFMpegSettings,
      appSettings: Settings.AppSettings,
      getBlobContainer: Storage.GetContainerClient,
      getQueueClient: Queue.GetQueueClient
    ) =
    inherit BackgroundService()

    override _.ExecuteAsync(ct: CancellationToken) =
      let getInputMessage = Queue.getInputMessage getQueueClient

      let downloadInputFile = Storage.downloadInputFile getBlobContainer logger

      let convertFile = FFMpeg.convertFile ffmpegSettings logger

      let uploadOutputFile = Storage.uploadOutputFile getBlobContainer logger

      let deleteInputMessage = Queue.deleteInputMessage getQueueClient

      let deleteInputFile = Storage.deleteInputFile getBlobContainer logger

      let deleteDownloadedFile = Files.deleteDownloadedFile

      let deleteConvertedFile = Files.deleteConvertedFile

      let sendSuccessMessage = Queue.sendSuccessMessage getQueueClient

      let sendFailureMessage = Queue.sendFailureMessage getQueueClient

      let convert =
        Workflows.convert
          getInputMessage
          downloadInputFile
          convertFile
          uploadOutputFile
          deleteInputMessage
          deleteInputFile
          deleteDownloadedFile
          deleteConvertedFile
          sendSuccessMessage
          sendFailureMessage

      task {
        while not ct.IsCancellationRequested do
          try
            do! convert ()
          with e ->
            logger.LogError(e, "Worker error:")

          do! Task.Delay(appSettings.Delay)
      }

#nowarn "20"

module Program =
  open Microsoft.Extensions.Hosting
  open Microsoft.Extensions.DependencyInjection

  let private configureAppConfig _ (builder: IConfigurationBuilder) =
    builder.AddUserSecrets(true)

    ()

  let private configureQueueServiceClient (options: IOptions<Settings.StorageSettings>) =
    let settings = options.Value

    QueueServiceClient(settings.ConnectionString)

  let private configureServices _ (services: IServiceCollection) =
    services
      .AddSingletonFunc<Settings.AppSettings, IConfiguration>(fun cfg -> cfg.Get<Settings.AppSettings>())
      .AddSingletonFunc<Settings.FFMpegSettings, IConfiguration>(fun cfg ->
        cfg
          .GetSection(Settings.FFMpegSettings.SectionName)
          .Get<Settings.FFMpegSettings>())
      .AddSingletonFunc<Settings.StorageSettings, IConfiguration>(fun cfg ->
        cfg
          .GetSection(Settings.StorageSettings.SectionName)
          .Get<Settings.StorageSettings>())

    services
      .AddSingletonFunc<QueueServiceClient, Settings.StorageSettings>(fun settings -> QueueServiceClient(settings.ConnectionString))
      .AddSingletonFunc<Storage.GetContainerClient, Settings.StorageSettings>(Storage.getContainerClient)
      .AddSingletonFunc<Queue.GetQueueClient, Settings.StorageSettings>(Queue.getQueueClient)

    services.AddHostedService<Worker.Worker>() |> ignore

    services.AddApplicationInsightsTelemetryWorkerService()

    ()

  let private createHostBuilder args =
    Host
      .CreateDefaultBuilder(args)
      .ConfigureAppConfiguration(configureAppConfig)
      .ConfigureServices(configureServices)

  [<EntryPoint>]
  let main args =
    createHostBuilder(args).Build().Run()

    0 // exit code
