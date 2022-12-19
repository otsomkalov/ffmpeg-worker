using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Worker.Settings;

namespace Worker.Factories;

public enum ContainerType
{
    Input,
    Output
}

public class ContainerClientFactory
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureSettings _azureSettings;

    public ContainerClientFactory(BlobServiceClient blobServiceClient, IOptions<AzureSettings> azureSettings)
    {
        _blobServiceClient = blobServiceClient;
        _azureSettings = azureSettings.Value;
    }

    public BlobContainerClient GetBlobClient(ContainerType containerType)
    {
        return containerType switch
        {
            ContainerType.Input => _blobServiceClient.GetBlobContainerClient(_azureSettings.InputStorage.ContainerName),
            ContainerType.Output => _blobServiceClient.GetBlobContainerClient(_azureSettings.OutputStorage.ContainerName)
        };
    }
}