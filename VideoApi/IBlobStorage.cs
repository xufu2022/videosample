namespace VideoApi;

public interface IBlobStorage
{
    Task<IEnumerable<string>> ListBlobNamesAsync(string container);
    Task<BlobDownloadResult> DownloadAsync(string container, string blobName, long? rangeStart, long? rangeLength);
}

public record BlobDownloadResult(Stream Content, long TotalSize);
