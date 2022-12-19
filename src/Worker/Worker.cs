using Microsoft.Extensions.Options;
using Worker.Models;
using Worker.Services;
using Worker.Settings;

namespace Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly FFMpegService _ffMpegService;
    private readonly InputStorageService _inputStorageService;
    private readonly OutputStorageService _outputStorageService;

    public Worker(ILogger<Worker> logger, IOptions<GlobalSettings> globalSettings, FFMpegService ffMpegService,
        InputStorageService inputStorageService, OutputStorageService outputStorageService)
    {
        _logger = logger;
        _ffMpegService = ffMpegService;
        _inputStorageService = inputStorageService;
        _outputStorageService = outputStorageService;
        _globalSettings = globalSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during Worker execution:");
            }

            await Task.Delay(_globalSettings.Delay, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var message = await _inputStorageService.ReceiveMessageAsync<InputMessage>(cancellationToken);

        if (message == null)
        {
            return;
        }

        var inputFilePath = await _inputStorageService.DownloadBlobAsync(message.BlobName, cancellationToken);

        var conversionResult = await _ffMpegService.ConvertAsync(inputFilePath, message.Arguments, message.TargetExtension);

        if (conversionResult != null)
        {
            await _outputStorageService.UploadBlobAsync(conversionResult.Name, conversionResult.FullName, cancellationToken);

            var resultQueueMessage = new OutputMessage(message.Id, conversionResult.Name);

            await _outputStorageService.SendMessageAsync(resultQueueMessage, cancellationToken);
        }
    }
}