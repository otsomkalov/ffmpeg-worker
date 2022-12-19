namespace Worker.Settings;

public record GlobalSettings
{
    public TimeSpan Delay { get; init; }
}