module Infrastructure.Startup

#nowarn "20"

open Azure.Storage.Blobs
open Azure.Storage.Queues
open Infrastructure.Settings
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open otsom.fs.Extensions.DependencyInjection
open Domain.Repos

let addIntegrationsCore (cfg: IConfiguration) (services: IServiceCollection) =
  services.Configure<StorageSettings>(cfg.GetSection(StorageSettings.SectionName))

  services.BuildSingleton<StorageSettings, IOptions<StorageSettings>>(_.Value)

  services
    .BuildSingleton<BlobServiceClient, StorageSettings>(fun cfg -> BlobServiceClient(cfg.ConnectionString))
    .BuildSingleton<QueueServiceClient, StorageSettings>(fun cfg -> QueueServiceClient(cfg.ConnectionString))

  services.AddSingleton<IRemoteStorage, AzureRemoteStorage>()