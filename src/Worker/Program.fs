namespace Worker

open Infra
open Infra.Settings
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Core
open Worker.Settings
open otsom.fs.Extensions.DependencyInjection
open Domain.Workflows

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

  let private configureServices (ctx: HostBuilderContext) (services: IServiceCollection) =
    services
      .BuildSingleton<AppSettings, IConfiguration>(_.Get<AppSettings>())
      .BuildSingleton<FFMpegSettings, IConfiguration>(_.GetSection(FFMpegSettings.SectionName).Get<FFMpegSettings>())

    services
#if AZ
    |> Startup.addAzureInfra ctx.Configuration
#endif
#if AWS
    |> Startup.addAWSInfra ctx.Configuration
#endif

    services.AddHostedService<Worker.Worker>() |> ignore

    services.BuildSingleton<Converter.Convert, FFMpegSettings, ILoggerFactory>(FFMpegConverter.convert)

    services.AddApplicationInsightsTelemetryWorkerService()

    ()

  let private createHostBuilder args =
    Host.CreateDefaultBuilder(args).ConfigureAppConfiguration(configureAppConfig).ConfigureServices(configureServices)

  [<EntryPoint>]
  let main args =
    createHostBuilder(args).Build().Run()

    0 // exit code