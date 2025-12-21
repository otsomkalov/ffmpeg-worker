module Domain.Startup

open Domain.Settings
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection

let addDomain (cfg: IConfiguration) (services: IServiceCollection) = services.Configure<AppSettings>(cfg)