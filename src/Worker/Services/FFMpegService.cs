using System.Diagnostics;
using Microsoft.Extensions.Options;
using Worker.Settings;

namespace Worker.Services;

public class FFMpegService
{
    private readonly FFMpegSettings _settings;
    private readonly ILogger<FFMpegService> _logger;

    public FFMpegService(IOptions<FFMpegSettings> settings, ILogger<FFMpegService> logger)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<FileInfo> ConvertAsync(string inputFilePath, string arguments, string targetExtension)
    {
        var targetFileName = Guid.NewGuid() + targetExtension;
        var targetFileInfo = new FileInfo(Path.Combine(Path.GetTempPath(), targetFileName));

        var argumentsParts = new List<string>
        {
            $"-i {inputFilePath}",
            arguments,
            targetFileInfo.FullName
        };

        var processStartInfo = new ProcessStartInfo
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            FileName = _settings.Path,
            Arguments = string.Join(' ', argumentsParts)
        };

        using var process = Process.Start(processStartInfo);

        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            return targetFileInfo;
        }

        _logger.LogError("Error during FFMpeg file conversion: {Error}", error);

        return null;
    }
}