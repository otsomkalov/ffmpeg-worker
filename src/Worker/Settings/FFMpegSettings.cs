namespace Worker.Settings;

public record FFMpegSettings
{
    public const string SectionName = "FFMpeg";

    public string Path { get; init; }
}