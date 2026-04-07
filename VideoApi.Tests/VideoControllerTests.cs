using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VideoApi;
using VideoApi.Controllers;

namespace VideoApi.Tests;

public class VideoControllerTests
{
    private readonly Mock<IBlobStorage> _storageMock = new();

    [Fact]
    public async Task List_ReturnsAllBlobNames()
    {
        _storageMock
            .Setup(s => s.ListBlobNamesAsync("videos"))
            .ReturnsAsync(new[] { "a.mp4", "b.mp4" });

        var controller = new VideoController(_storageMock.Object);
        var result = await controller.List() as OkObjectResult;

        Assert.NotNull(result);
        var names = Assert.IsAssignableFrom<IEnumerable<string>>(result.Value);
        Assert.Equal(new[] { "a.mp4", "b.mp4" }, names);
    }

    [Fact]
    public async Task StreamVideo_ReturnsFullContent_WhenNoRangeHeader()
    {
        var fakeContent = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        _storageMock
            .Setup(s => s.DownloadAsync("videos", "test.mp4", null, null))
            .ReturnsAsync(new BlobDownloadResult(fakeContent, 5));

        var controller = new VideoController(_storageMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Response.Body = new MemoryStream();

        await controller.StreamVideo("test.mp4");

        Assert.Equal("video/mp4", controller.HttpContext.Response.ContentType);
        Assert.Equal(200, controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task StreamVideo_Returns206_WhenRangeHeaderPresent()
    {
        var fakeContent = new MemoryStream(new byte[] { 2, 3, 4 });
        _storageMock
            .Setup(s => s.DownloadAsync("videos", "test.mp4", 1, 3))
            .ReturnsAsync(new BlobDownloadResult(fakeContent, 5));

        var controller = new VideoController(_storageMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Range"] = "bytes=1-3";
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.StreamVideo("test.mp4");

        Assert.Equal(206, controller.HttpContext.Response.StatusCode);
        Assert.Equal("bytes 1-3/5", controller.HttpContext.Response.Headers["Content-Range"].ToString());
    }
}
