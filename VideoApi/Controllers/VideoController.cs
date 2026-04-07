using Microsoft.AspNetCore.Mvc;

namespace VideoApi.Controllers;

[ApiController]
[Route("api/video")]
public class VideoController : ControllerBase
{
    private readonly IBlobStorage _storage;
    private const string Container = "videos";

    public VideoController(IBlobStorage storage)
    {
        _storage = storage;
    }

    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        var names = await _storage.ListBlobNamesAsync(Container);
        return Ok(names);
    }

    [HttpGet("{blobName}")]
    public async Task StreamVideo(string blobName)
    {
        Response.Headers["Accept-Ranges"] = "bytes";

        var rangeHeader = Request.Headers["Range"].ToString();

        try
        {
            if (!string.IsNullOrEmpty(rangeHeader))
            {
                // Reject multi-range requests and validate format
                if (!rangeHeader.StartsWith("bytes=") || rangeHeader.Contains(","))
                {
                    Response.StatusCode = 416;
                    Response.Headers["Content-Range"] = "bytes */*";
                    return;
                }

                var value = rangeHeader.Replace("bytes=", "");
                var parts = value.Split('-');
                if (parts.Length != 2 || !long.TryParse(parts[0], out long start))
                {
                    Response.StatusCode = 416;
                    Response.Headers["Content-Range"] = "bytes */*";
                    return;
                }
                long? endParsed = !string.IsNullOrEmpty(parts[1]) && long.TryParse(parts[1], out long endVal)
                    ? endVal
                    : (long?)null;

                var result = await _storage.DownloadAsync(Container, blobName, start,
                    endParsed.HasValue ? endParsed.Value - start + 1 : null);

                long end = endParsed ?? (result.TotalSize - 1);
                long length = end - start + 1;

                Response.StatusCode = 206;
                Response.ContentType = "video/mp4";
                Response.ContentLength = length;
                Response.Headers["Content-Range"] = $"bytes {start}-{end}/{result.TotalSize}";

                await result.Content.CopyToAsync(Response.Body);
            }
            else
            {
                var result = await _storage.DownloadAsync(Container, blobName, null, null);

                Response.StatusCode = 200;
                Response.ContentType = "video/mp4";
                Response.ContentLength = result.TotalSize;

                await result.Content.CopyToAsync(Response.Body);
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            if (!Response.HasStarted)
            {
                Response.StatusCode = 404;
            }
        }
        catch (Exception)
        {
            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
            }
        }
    }
}
