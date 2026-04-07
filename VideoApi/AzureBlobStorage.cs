using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace VideoApi;

public class AzureBlobStorage : IBlobStorage
{
    private readonly BlobServiceClient _client;

    public AzureBlobStorage(BlobServiceClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<string>> ListBlobNamesAsync(string container)
    {
        var containerClient = _client.GetBlobContainerClient(container);
        var names = new List<string>();
        await foreach (var blob in containerClient.GetBlobsAsync())
            names.Add(blob.Name);
        return names;
    }

    public async Task<BlobDownloadResult> DownloadAsync(
        string container, string blobName, long? rangeStart, long? rangeLength)
    {
        var containerClient = _client.GetBlobContainerClient(container);
        var blobClient = containerClient.GetBlobClient(blobName);

        var properties = await blobClient.GetPropertiesAsync();
        long totalSize = properties.Value.ContentLength;

        BlobDownloadOptions options = null;
        if (rangeStart.HasValue)
        {
            options = new BlobDownloadOptions
            {
                Range = rangeLength.HasValue
                    ? new HttpRange(rangeStart.Value, rangeLength.Value)
                    : new HttpRange(rangeStart.Value)   // open-ended: bytes=X-
            };
        }

        var download = await blobClient.DownloadStreamingAsync(options);
        return new BlobDownloadResult(download.Value.Content, totalSize);
    }
}
