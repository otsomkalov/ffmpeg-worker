namespace Worker

open Azure.Storage.Queues
open Infrastructure
open Infrastructure.Settings
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FSharp.Core
open Worker.Settings
open otsom.fs.Extensions.DependencyInjection
open Domain.Workflows

#nowarn "20"

module Program =
  open Microsoft.Extensions.Hosting
  open Microsoft.Extensions.DependencyInjection

  let private configureAppConfig _ (builder: IConfigurationBuilder) =
    builder.AddUserSecrets(true)

    ()

  let private configureQueueServiceClient (options: IOptions<StorageSettings>) =
    let settings = options.Value

    QueueServiceClient(settings.ConnectionString)

  let private configureServices _ (services: IServiceCollection) =
    services
      .BuildSingleton<AppSettings, IConfiguration>(fun cfg -> cfg.Get<AppSettings>())
      .BuildSingleton<FFMpegSettings, IConfiguration>(fun cfg ->
        cfg
          .GetSection(FFMpegSettings.SectionName)
          .Get<FFMpegSettings>())
      .BuildSingleton<StorageSettings, IConfiguration>(fun cfg ->
        cfg
          .GetSection(StorageSettings.SectionName)
          .Get<StorageSettings>())

    services.AddHostedService<Worker.Worker>() |> ignore

    services
      .BuildSingleton<RemoteStorage.DownloadFile, StorageSettings, ILogger>(RemoteStorage.downloadFile)
      .BuildSingleton<RemoteStorage.UploadFile, StorageSettings, ILogger>(RemoteStorage.uploadFile)
      .BuildSingleton<RemoteStorage.DeleteFile, StorageSettings, ILogger>(RemoteStorage.deleteFile)
      .BuildSingleton<Converter.Convert, FFMpegSettings, ILogger>(FFMpegConverter.convert)
      .BuildSingleton<Queue.GetMessage, StorageSettings>(Queue.getMessage)
      .BuildSingleton<Queue.DeleteMessageFactory, StorageSettings, ILogger>(Queue.deleteMessageFactory)
      .BuildSingleton<Queue.SendSuccessMessageFactory, StorageSettings, ILogger>(Queue.sendSuccessMessageFactory)
      .BuildSingleton<Queue.SendFailureMessageFactory, StorageSettings, ILogger>(Queue.sendFailureMessageFactory)

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
