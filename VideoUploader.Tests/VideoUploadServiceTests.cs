using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Moq;

namespace VideoUploader.Tests;

public class VideoUploadServiceTests
{
    private readonly Mock<BlobServiceClient> _blobServiceMock = new();
    private readonly Mock<BlobContainerClient> _containerMock = new();
    private readonly Mock<BlobClient> _blobClientMock = new();

    public VideoUploadServiceTests()
    {
        _blobServiceMock
            .Setup(s => s.GetBlobContainerClient("videos"))
            .Returns(_containerMock.Object);

        _containerMock
            .Setup(c => c.CreateIfNotExistsAsync(default, default, default, default))
            .ReturnsAsync((Response<BlobContainerInfo>)null!);
    }

    [Fact]
    public async Task UploadVideosAsync_SkipsBlobThatAlreadyExists()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "test.mp4");
        await File.WriteAllBytesAsync(file, new byte[] { 1, 2, 3 });

        _containerMock
            .Setup(c => c.GetBlobClient("test.mp4"))
            .Returns(_blobClientMock.Object);
        _blobClientMock
            .Setup(b => b.ExistsAsync(default))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var svc = new VideoUploadService(_blobServiceMock.Object);
        await svc.UploadVideosAsync(dir);

        _blobClientMock.Verify(b => b.UploadAsync(It.IsAny<Stream>(), false, default), Times.Never);
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task UploadVideosAsync_UploadsBlobThatDoesNotExist()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "new.mp4");
        await File.WriteAllBytesAsync(file, new byte[] { 1, 2, 3 });

        _containerMock
            .Setup(c => c.GetBlobClient("new.mp4"))
            .Returns(_blobClientMock.Object);
        _blobClientMock
            .Setup(b => b.ExistsAsync(default))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
        _blobClientMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), false, default))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        var svc = new VideoUploadService(_blobServiceMock.Object);
        await svc.UploadVideosAsync(dir);

        _blobClientMock.Verify(b => b.UploadAsync(It.IsAny<Stream>(), false, default), Times.Once);
        Directory.Delete(dir, true);
    }
}
