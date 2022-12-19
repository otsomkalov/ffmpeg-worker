namespace Worker.Settings;

public record SourceSettings
{
    public string ContainerName { get; set; }

    public string QueueName { get; set; }
}

public record AzureSettings
{
    public const string SectionName = "Azure";

    public string ConnectionString { get; set; }

    public SourceSettings InputStorage { get; set; }

    public SourceSettings OutputStorage { get; set; }
}