module Worker

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
open otsom.FSharp.Extensions
open otsom.FSharp.Extensions.ServiceCollection
open shortid

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

  type Convert = FileInfo -> Task<Result<FileInfo, ConvertError>>

  let convertFile (settings: Settings.FFMpegSettings) (logger: ILogger) : Convert =
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
              Logf.logfi
                logger
                "Conversion of %s{InputFileName} to %s{OutputFileName} done! FFMpeg output: %s{FFMpegOutput}"
                fileInfo.Name
                targetFileName
                ffmpegOutput

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

  type DownloadInputFile = string -> Task<FileInfo>

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

        let downloadedFileName = ShortId.Generate()
        let downloadedFileExtension = Path.GetExtension inputFileName
        let fullDownloadedFileName = sprintf "%s%s" downloadedFileName downloadedFileExtension

        let downloadedFilePath = Path.Combine(Path.GetTempPath(), fullDownloadedFileName)

        Logf.logfi logger "Downloading file %s{InputFileName} to %s{DownloadedFileName}" inputFileName fullDownloadedFileName

        do! blobClient.DownloadToAsync(downloadedFilePath) |> Task.map ignore

        Logf.logfi logger "File %s{InputFileName} downloaded to %s{DownloadedFileName}" inputFileName fullDownloadedFileName

        return FileInfo(downloadedFilePath)
      }

  type UploadOutputFile = FileInfo -> Task<unit>

  let uploadOutputFile (getBlobContainer: GetContainerClient) logger : UploadOutputFile =
    let outputContainerClient = getBlobContainer Output

    fun fileInfo ->
      task {
        let outputBlobClient = outputContainerClient.GetBlobClient(fileInfo.Name)

        Logf.logfi logger "Uploading file %s{ConvertedFileName}" fileInfo.Name

        do! outputBlobClient.UploadAsync(fileInfo.FullName, true) |> Task.map ignore

        Logf.logfi logger "File %s{ConvertedFileName} uploaded" fileInfo.Name
      }

  type DeleteInputFile = string -> Task<unit>

  let deleteInputFile (getBlobContainer: GetContainerClient) logger : DeleteInputFile =
    let inputContainerClient = getBlobContainer Input

    fun name ->
      task {
        let outputBlobClient = inputContainerClient.GetBlobClient(name)

        Logf.logfi logger "Deleting file %s{InputFileName}" name

        do! outputBlobClient.DeleteIfExistsAsync() |> Task.map ignore

        Logf.logfi logger "File %s{InputFileName} deleted" name
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
          | Ok outputFileInfo ->
            task {
              do! uploadOutputFile outputFileInfo
              do! deleteInputFile inputMessage.Name
              do deleteDownloadedFile inputFileInfo.FullName
              do deleteConvertedFile outputFileInfo.FullName
              do! sendSuccessMessage inputMessage.Id outputFileInfo.Name
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
          do! convert ()

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

    services.Configure<HostOptions>(fun (opts: HostOptions) -> opts.BackgroundServiceExceptionBehavior <- BackgroundServiceExceptionBehavior.Ignore)

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
