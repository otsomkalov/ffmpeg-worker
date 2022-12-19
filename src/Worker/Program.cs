using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using Worker.Factories;
using Worker.Services;
using Worker.Settings;

void ConfigureServices(HostBuilderContext context, IServiceCollection services)
{
    var configuration = context.Configuration;

    services.Configure<AzureSettings>(configuration.GetSection(AzureSettings.SectionName))
        .Configure<FFMpegSettings>(configuration.GetSection(FFMpegSettings.SectionName))
        .Configure<GlobalSettings>(configuration);

    services.AddSingleton<BlobServiceClient>(sp =>
    {
        var settings = sp.GetRequiredService<IOptions<AzureSettings>>().Value;

        return new(settings.ConnectionString);
    });

    services.AddSingleton<QueueServiceClient>(sp =>
    {
        var settings = sp.GetRequiredService<IOptions<AzureSettings>>().Value;

        return new(settings.ConnectionString);
    });

    services.AddSingleton<ContainerClientFactory>().AddSingleton<QueueClientFactory>();

    services.AddSingleton<FFMpegService>()
        .AddSingleton<InputStorageService>()
        .AddSingleton<OutputStorageService>();

    services.AddHostedService<Worker.Worker>();

    services.AddApplicationInsightsTelemetryWorkerService();
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(ConfigureServices)
    .Build();

await host.RunAsync();