namespace Worker.Models;

public record InputMessage(
    int Id,
    string BlobName,
    string Arguments,
    string TargetExtension
);