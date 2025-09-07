#nowarn "20"

namespace Infra.Amazon

open Amazon.S3
open Amazon.SQS
open Domain.Repos
open Infra.Amazon.Queue
open Infra.Amazon.Storage
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open otsom.fs.Extensions.DependencyInjection

module Startup =
  let addAWSInfra (cfg: IConfiguration) (services: IServiceCollection) =
    services.Configure<StorageSettings>(cfg.GetSection(StorageSettings.SectionName))

    services.BuildSingleton<StorageSettings, IOptions<StorageSettings>>(_.Value)

    services.AddDefaultAWSOptions(cfg.GetAWSOptions()).AddAWSService<IAmazonSQS>().AddAWSService<IAmazonS3>()

    services
      .AddSingleton<IInputQueue, InputQueue>()
      .AddSingleton<IInputStorage, InputStorage>()
      .AddSingleton<IOutputStorage, OutputStorage>()
      .BuildSingleton<GetOutputQueue, _, _, _>(Queue.getOutputQueue)