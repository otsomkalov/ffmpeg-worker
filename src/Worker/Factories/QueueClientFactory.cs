using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using Worker.Settings;

namespace Worker.Factories;

public enum QueueType
{
    Input,
    Output
}

public class QueueClientFactory
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly AzureSettings _azureSettings;

    public QueueClientFactory(QueueServiceClient queueServiceClient, IOptions<AzureSettings> azureSettings)
    {
        _queueServiceClient = queueServiceClient;
        _azureSettings = azureSettings.Value;
    }

    public QueueClient GetClient(QueueType queueType)
    {
        return queueType switch
        {
            QueueType.Input => _queueServiceClient.GetQueueClient(_azureSettings.InputStorage.QueueName),
            QueueType.Output => _queueServiceClient.GetQueueClient(_azureSettings.OutputStorage.QueueName)
        };
    }
}