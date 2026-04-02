namespace Worker

open Azure.Monitor.OpenTelemetry.Exporter
open Domain
open Infra
open Infra.Settings
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Core
open OpenTelemetry.Trace
open Worker.Settings
open otsom.fs.Extensions.DependencyInjection

#if AWS
open Infra.Amazon
#endif

#if AZ
open Infra.Azure
#endif

#nowarn "20"

module Program =
  open Microsoft.Extensions.Hosting
  open Microsoft.Extensions.DependencyInjection

  let private configureAppConfig _ (builder: IConfigurationBuilder) =
    builder.AddUserSecrets(true)

    ()

  let private configureLogging (builder: ILoggingBuilder) =
    builder.AddOpenTelemetry(fun b ->
      b.IncludeFormattedMessage <- true
      b.IncludeScopes <- true
      ())

    ()

  let private configureServices (ctx: HostBuilderContext) (services: IServiceCollection) =
    let cfg = ctx.Configuration

    services.BuildSingleton<WorkerSettings, IConfiguration>(_.Get<WorkerSettings>())

    services.Configure<FFMpegSettings>(cfg.GetRequiredSection(FFMpegSettings.SectionName))
    services.AddSingleton<IConverter, FFMpegConverter>()

    services
    |> Startup.addDomain cfg
#if AZ
    |> Startup.addAzureInfra cfg
#endif
#if AWS
    |> Startup.addAWSInfra ctx.Configuration
#endif

    services.AddHostedService<Worker.Worker>() |> ignore

    services
      .AddOpenTelemetry()

      .WithTracing(fun tracing ->
        tracing.AddProcessor<Observability.AzureStorageQueueReceiveMessageTracesProcessor>()

        tracing.AddSource(Observability.ActivitySource.Name, "Azure.Storage.*")

        ())
      .UseAzureMonitorExporter()

    ()

  let private createHostBuilder args =
    Host
      .CreateDefaultBuilder(args)
      .ConfigureAppConfiguration(configureAppConfig)
      .ConfigureLogging(configureLogging)
      .ConfigureServices(configureServices)

  [<EntryPoint>]
  let main args =
    createHostBuilder(args).Build().Run()

    0 // exit code