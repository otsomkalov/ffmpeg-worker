using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Worker.Factories;

namespace Worker.Services;

public class InputStorageService
{
    private readonly BlobContainerClient _inputContainerClient;
    private readonly QueueClient _inputQueueClient;

    public InputStorageService(ContainerClientFactory containerClientFactory, QueueClientFactory queueClientFactory)
    {
        _inputContainerClient = containerClientFactory.GetBlobClient(ContainerType.Input);
        _inputQueueClient = queueClientFactory.GetClient(QueueType.Input);
    }

    public async Task<string> DownloadBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        var inputBlobClient = _inputContainerClient.GetBlobClient(blobName);

        var inputFilePath = Path.Combine(Path.GetTempPath(), inputBlobClient.Name);

        await inputBlobClient.DownloadToAsync(inputFilePath, cancellationToken);

        return inputFilePath;
    }

    public async Task DeleteBlobAsync(string inputBlobName, CancellationToken cancellationToken)
    {
        var inputBlobClient = _inputContainerClient.GetBlobClient(inputBlobName);

        await inputBlobClient.DeleteAsync(cancellationToken: cancellationToken);
    }

    public async Task<T> ReceiveMessageAsync<T>(CancellationToken cancellationToken) where T: class
    {
        var messageResponse = await _inputQueueClient.ReceiveMessageAsync(cancellationToken: cancellationToken);

        if (messageResponse.Value == null)
        {
            return null;
        }

        await _inputQueueClient.DeleteMessageAsync(messageResponse.Value.MessageId, messageResponse.Value.PopReceipt, cancellationToken);

        if (messageResponse.Value.Body is {} body)
        {
            return body.ToObjectFromJson<T>();
        }

        return null;
    }
}