module Infrastructure.Startup

#nowarn "20"

open Azure.Storage.Blobs
open Azure.Storage.Queues
open Infrastructure.Settings
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open otsom.fs.Extensions.DependencyInjection
open Domain.Workflows

let addIntegrationsCore (cfg: IConfiguration) (services: IServiceCollection) =
  services.Configure<StorageSettings>(cfg.GetSection(StorageSettings.SectionName))

  services.BuildSingleton<StorageSettings, IOptions<StorageSettings>>(_.Value)

  services
    .BuildSingleton<BlobServiceClient, StorageSettings>(fun cfg -> BlobServiceClient(cfg.ConnectionString))
    .BuildSingleton<QueueServiceClient, StorageSettings>(fun cfg -> QueueServiceClient(cfg.ConnectionString))

  services
    .BuildSingleton<RemoteStorage.DownloadFile, BlobServiceClient, StorageSettings, ILoggerFactory>(RemoteStorage.downloadFile)
    .BuildSingleton<RemoteStorage.UploadFile, BlobServiceClient, StorageSettings, ILoggerFactory>(RemoteStorage.uploadFile)
    .BuildSingleton<RemoteStorage.DeleteFile, BlobServiceClient, StorageSettings, ILoggerFactory>(RemoteStorage.deleteFile)