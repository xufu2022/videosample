using Azure.Storage.Blobs;

namespace VideoUploader;

public class VideoUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private const string ContainerName = "videos";

    public VideoUploadService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task UploadVideosAsync(string directory)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync();

        foreach (var file in Directory.GetFiles(directory, "*.mp4"))
        {
            var blobName = Path.GetFileName(file);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (await blobClient.ExistsAsync())
            {
                Console.WriteLine($"Skipped: {blobName}");
                continue;
            }

            await using var stream = File.OpenRead(file);
            await blobClient.UploadAsync(stream, overwrite: false);
            Console.WriteLine($"Uploaded: {blobName}");
        }
    }
}
