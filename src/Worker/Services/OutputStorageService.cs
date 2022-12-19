using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Worker.Factories;
using Worker.Models;

namespace Worker.Services;

public class OutputStorageService
{
    private readonly BlobContainerClient _outputContainerClient;
    private readonly QueueClient _outputQueueClient;

    public OutputStorageService(ContainerClientFactory containerClientFactory, QueueClientFactory queueClientFactory)
    {
        _outputContainerClient = containerClientFactory.GetBlobClient(ContainerType.Output);
        _outputQueueClient = queueClientFactory.GetClient(QueueType.Output);
    }

    public async Task UploadBlobAsync(string blobName, string filePath, CancellationToken cancellationToken)
    {
        var outputBlobClient = _outputContainerClient.GetBlobClient(blobName);

        await outputBlobClient.UploadAsync(filePath, cancellationToken);
    }

    public async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken)
    {
        var messageBody = JsonSerializer.Serialize(message);

        await _outputQueueClient.SendMessageAsync(messageBody, cancellationToken);
    }
}