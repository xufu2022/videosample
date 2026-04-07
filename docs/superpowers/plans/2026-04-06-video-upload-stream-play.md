# Video Upload, Stream & Play Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build VideoUploader (.NET 10 console), VideoApi (.NET 10 Web API), and VideoPlayer (.NET 4.8 WinForms) to upload, stream, and play MP4 files via Azurite.

**Architecture:** VideoUploader scans `C:\videos` and uploads `.mp4` files to Azurite on launch, skipping blobs that already exist. VideoApi exposes a list endpoint and a streaming endpoint with HTTP Range support — piping blob bytes directly to the response body. VideoPlayer fetches the video list from the API and passes the selected video's API URL to DirectShow, which handles HTTP streaming natively.

**Tech Stack:** .NET 10, .NET Framework 4.8, Azure.Storage.Blobs, DirectShowLib-2005, Newtonsoft.Json, xUnit, Moq

---

## File Map

```
VideoSolution.sln
VideoUploader/
  VideoUploader.csproj          .NET 10 console
  Program.cs                    Entry point — wires BlobServiceClient, calls VideoUploadService
  VideoUploadService.cs         Scans directory, skips existing blobs, uploads new ones
VideoUploader.Tests/
  VideoUploader.Tests.csproj    .NET 10, xUnit, Moq
  VideoUploadServiceTests.cs    Unit tests for VideoUploadService
VideoApi/
  VideoApi.csproj               .NET 10 Web API
  Program.cs                    DI setup, CORS, URL binding
  IBlobStorage.cs               Interface over Azure SDK — makes controller testable
  AzureBlobStorage.cs           IBlobStorage backed by BlobServiceClient
  Controllers/
    VideoController.cs          GET /api/video/list and GET /api/video/{blobName}
VideoApi.Tests/
  VideoApi.Tests.csproj         .NET 10, xUnit, Moq
  VideoControllerTests.cs       Unit tests for VideoController
VideoPlayer/
  VideoPlayer.csproj            net48, WinForms, DirectShowLib-2005, Newtonsoft.Json
  Program.cs                    [STAThread] entry point
  MainForm.cs                   Logic: refresh list, play selected video via DirectShow
  MainForm.Designer.cs          Controls layout: ListBox, buttons, video panel
```

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `VideoSolution.sln`
- Create: `VideoUploader/VideoUploader.csproj`
- Create: `VideoUploader.Tests/VideoUploader.Tests.csproj`
- Create: `VideoApi/VideoApi.csproj`
- Create: `VideoApi.Tests/VideoApi.Tests.csproj`
- Create: `VideoPlayer/VideoPlayer.csproj`

- [ ] **Step 1: Create solution and .NET 10 projects**

```bash
cd D:/dev/apr/video
dotnet new sln -n VideoSolution
dotnet new console -n VideoUploader --framework net10.0
dotnet new xunit -n VideoUploader.Tests --framework net10.0
dotnet new webapi -n VideoApi --framework net10.0 --no-openapi
dotnet new xunit -n VideoApi.Tests --framework net10.0
mkdir VideoPlayer
```

- [ ] **Step 2: Add NuGet packages to VideoUploader**

```bash
dotnet add VideoUploader/VideoUploader.csproj package Azure.Storage.Blobs
dotnet add VideoUploader.Tests/VideoUploader.Tests.csproj package Moq
dotnet add VideoUploader.Tests/VideoUploader.Tests.csproj reference VideoUploader/VideoUploader.csproj
```

- [ ] **Step 3: Add NuGet packages to VideoApi**

```bash
dotnet add VideoApi/VideoApi.csproj package Azure.Storage.Blobs
dotnet add VideoApi.Tests/VideoApi.Tests.csproj package Moq
dotnet add VideoApi.Tests/VideoApi.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add VideoApi.Tests/VideoApi.Tests.csproj reference VideoApi/VideoApi.csproj
```

- [ ] **Step 4: Create VideoPlayer.csproj (net48 WinForms)**

Write `VideoPlayer/VideoPlayer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <RootNamespace>VideoPlayer</RootNamespace>
    <AssemblyName>VideoPlayer</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="DirectShowLib" Version="1.0.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Add all projects to solution**

```bash
dotnet sln VideoSolution.sln add VideoUploader/VideoUploader.csproj
dotnet sln VideoSolution.sln add VideoUploader.Tests/VideoUploader.Tests.csproj
dotnet sln VideoSolution.sln add VideoApi/VideoApi.csproj
dotnet sln VideoSolution.sln add VideoApi.Tests/VideoApi.Tests.csproj
dotnet sln VideoSolution.sln add VideoPlayer/VideoPlayer.csproj
```

- [ ] **Step 6: Verify solution builds (expect compile errors on default templates — that's fine)**

```bash
dotnet build VideoSolution.sln
```

- [ ] **Step 7: Commit**

```bash
git init
git add VideoSolution.sln VideoUploader/VideoUploader.csproj VideoUploader.Tests/VideoUploader.Tests.csproj VideoApi/VideoApi.csproj VideoApi.Tests/VideoApi.Tests.csproj VideoPlayer/VideoPlayer.csproj
git commit -m "chore: scaffold solution and projects"
```

---

### Task 2: VideoUploadService — tests then implementation

**Files:**
- Create: `VideoUploader/VideoUploadService.cs`
- Modify: `VideoUploader.Tests/VideoUploadServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Replace `VideoUploader.Tests/VideoUploadServiceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test VideoUploader.Tests/VideoUploader.Tests.csproj
```

Expected: compilation error — `VideoUploadService` does not exist yet.

- [ ] **Step 3: Implement VideoUploadService**

Create `VideoUploader/VideoUploadService.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test VideoUploader.Tests/VideoUploader.Tests.csproj
```

Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add VideoUploader/VideoUploadService.cs VideoUploader.Tests/VideoUploadServiceTests.cs
git commit -m "feat: VideoUploadService with skip-if-exists logic"
```

---

### Task 3: VideoUploader — Program.cs entry point

**Files:**
- Modify: `VideoUploader/Program.cs`

- [ ] **Step 1: Replace Program.cs**

```csharp
using Azure.Storage.Blobs;
using VideoUploader;

const string Directory = @"C:\videos";
const string ConnectionString = "UseDevelopmentStorage=true";

var blobServiceClient = new BlobServiceClient(ConnectionString);
var service = new VideoUploadService(blobServiceClient);

Console.WriteLine($"Scanning {Directory} for .mp4 files...");
await service.UploadVideosAsync(Directory);
Console.WriteLine("Done.");
```

- [ ] **Step 2: Build**

```bash
dotnet build VideoUploader/VideoUploader.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add VideoUploader/Program.cs
git commit -m "feat: VideoUploader entry point"
```

---

### Task 4: VideoApi — IBlobStorage, AzureBlobStorage, DI setup

**Files:**
- Create: `VideoApi/IBlobStorage.cs`
- Create: `VideoApi/AzureBlobStorage.cs`
- Modify: `VideoApi/Program.cs`

- [ ] **Step 1: Create IBlobStorage interface**

Create `VideoApi/IBlobStorage.cs`:

```csharp
namespace VideoApi;

public interface IBlobStorage
{
    Task<IEnumerable<string>> ListBlobNamesAsync(string container);
    Task<BlobDownloadResult> DownloadAsync(string container, string blobName, long? rangeStart, long? rangeLength);
}

public record BlobDownloadResult(Stream Content, long TotalSize);
```

- [ ] **Step 2: Implement AzureBlobStorage**

Create `VideoApi/AzureBlobStorage.cs`:

```csharp
using Azure;
using Azure.Storage.Blobs;

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

        var range = (rangeStart.HasValue && rangeLength.HasValue)
            ? new HttpRange(rangeStart.Value, rangeLength.Value)
            : default;

        var download = await blobClient.DownloadStreamingAsync(range);
        return new BlobDownloadResult(download.Value.Content, totalSize);
    }
}
```

- [ ] **Step 3: Replace Program.cs**

```csharp
using Azure.Storage.Blobs;
using VideoApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ => new BlobServiceClient("UseDevelopmentStorage=true"));
builder.Services.AddSingleton<IBlobStorage, AzureBlobStorage>();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.MapControllers();
app.Urls.Add("http://localhost:5000");
app.Run();

public partial class Program { }
```

- [ ] **Step 4: Build**

```bash
dotnet build VideoApi/VideoApi.csproj
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add VideoApi/IBlobStorage.cs VideoApi/AzureBlobStorage.cs VideoApi/Program.cs
git commit -m "feat: VideoApi DI setup and IBlobStorage abstraction"
```

---

### Task 5: VideoApi — List endpoint, tests then implementation

**Files:**
- Create: `VideoApi/Controllers/VideoController.cs`
- Modify: `VideoApi.Tests/VideoControllerTests.cs`

- [ ] **Step 1: Write failing test for list endpoint**

Replace `VideoApi.Tests/VideoControllerTests.cs`:

```csharp
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
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test VideoApi.Tests/VideoApi.Tests.csproj
```

Expected: compilation error — `VideoController` does not exist yet.

- [ ] **Step 3: Create VideoController with List endpoint**

Create `VideoApi/Controllers/VideoController.cs`:

```csharp
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
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test VideoApi.Tests/VideoApi.Tests.csproj
```

Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add VideoApi/Controllers/VideoController.cs VideoApi.Tests/VideoControllerTests.cs
git commit -m "feat: VideoApi list endpoint"
```

---

### Task 6: VideoApi — Stream endpoint with Range support

**Files:**
- Modify: `VideoApi/Controllers/VideoController.cs`
- Modify: `VideoApi.Tests/VideoControllerTests.cs`

- [ ] **Step 1: Add failing tests for stream endpoint**

Add these tests inside the `VideoControllerTests` class (append to the existing file):

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test VideoApi.Tests/VideoApi.Tests.csproj
```

Expected: compilation error — `StreamVideo` does not exist yet.

- [ ] **Step 3: Add StreamVideo to VideoController**

Add this method to `VideoApi/Controllers/VideoController.cs` inside the class:

```csharp
[HttpGet("{blobName}")]
public async Task StreamVideo(string blobName)
{
    Response.Headers["Accept-Ranges"] = "bytes";

    var rangeHeader = Request.Headers["Range"].ToString();

    if (!string.IsNullOrEmpty(rangeHeader))
    {
        var value = rangeHeader.Replace("bytes=", "");
        var parts = value.Split('-');
        long start = long.Parse(parts[0]);
        long? endParsed = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
            ? long.Parse(parts[1])
            : null;

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
```

- [ ] **Step 4: Run all tests to verify they pass**

```bash
dotnet test VideoApi.Tests/VideoApi.Tests.csproj
```

Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add VideoApi/Controllers/VideoController.cs VideoApi.Tests/VideoControllerTests.cs
git commit -m "feat: VideoApi stream endpoint with Range support"
```

---

### Task 7: VideoPlayer — Project setup and MainForm UI

**Files:**
- Create: `VideoPlayer/Program.cs`
- Create: `VideoPlayer/MainForm.cs`
- Create: `VideoPlayer/MainForm.Designer.cs`

- [ ] **Step 1: Create Program.cs**

```csharp
using System;
using System.Windows.Forms;

namespace VideoPlayer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
```

- [ ] **Step 2: Create MainForm.Designer.cs**

```csharp
namespace VideoPlayer
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.ListBox listBoxVideos;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.Panel videoPanel;
        private System.Windows.Forms.Panel controlPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.listBoxVideos = new System.Windows.Forms.ListBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.videoPanel = new System.Windows.Forms.Panel();
            this.controlPanel = new System.Windows.Forms.Panel();

            // controlPanel
            this.controlPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.controlPanel.Height = 40;
            this.controlPanel.Controls.Add(this.btnPlay);
            this.controlPanel.Controls.Add(this.btnRefresh);

            // btnRefresh
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.Width = 80;
            this.btnRefresh.Location = new System.Drawing.Point(4, 8);
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

            // btnPlay
            this.btnPlay.Text = "Play";
            this.btnPlay.Width = 80;
            this.btnPlay.Location = new System.Drawing.Point(92, 8);
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);

            // listBoxVideos
            this.listBoxVideos.Dock = System.Windows.Forms.DockStyle.Top;
            this.listBoxVideos.Height = 160;

            // videoPanel
            this.videoPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.videoPanel.BackColor = System.Drawing.Color.Black;

            // MainForm
            this.Text = "Video Player";
            this.ClientSize = new System.Drawing.Size(800, 560);
            this.Controls.Add(this.videoPanel);
            this.Controls.Add(this.listBoxVideos);
            this.Controls.Add(this.controlPanel);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
        }
    }
}
```

- [ ] **Step 3: Create MainForm.cs (stub — no playback yet)**

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace VideoPlayer
{
    public partial class MainForm : Form
    {
        private const string ApiBase = "http://localhost:5000";

        public MainForm()
        {
            InitializeComponent();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await RefreshVideoListAsync();
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await RefreshVideoListAsync();
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (listBoxVideos.SelectedItem == null) return;
            var blobName = listBoxVideos.SelectedItem.ToString();
            var url = $"{ApiBase}/api/video/{blobName}";
            PlayVideo(url);
        }

        private async System.Threading.Tasks.Task RefreshVideoListAsync()
        {
            try
            {
                using var client = new HttpClient();
                var json = await client.GetStringAsync($"{ApiBase}/api/video/list");
                var names = JsonConvert.DeserializeObject<List<string>>(json);
                listBoxVideos.Items.Clear();
                foreach (var name in names)
                    listBoxVideos.Items.Add(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch video list: {ex.Message}");
            }
        }

        private void PlayVideo(string url)
        {
            // DirectShow wired in Task 8
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopPlayback();
        }

        private void StopPlayback()
        {
            // DirectShow cleanup wired in Task 8
        }
    }
}
```

- [ ] **Step 4: Build VideoPlayer**

```bash
dotnet build VideoPlayer/VideoPlayer.csproj
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add VideoPlayer/Program.cs VideoPlayer/MainForm.cs VideoPlayer/MainForm.Designer.cs
git commit -m "feat: VideoPlayer UI scaffold with video list refresh"
```

---

### Task 8: VideoPlayer — DirectShow playback

**Files:**
- Modify: `VideoPlayer/MainForm.cs`

- [ ] **Step 1: Add DirectShow fields and PlayVideo/StopPlayback implementation**

Replace the contents of `VideoPlayer/MainForm.cs` with the complete version below.
Note: `DirectShowLib` is in the `DirectShowLib` namespace (not `DirectShowLib2005`).

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DirectShowLib;
using Newtonsoft.Json;

namespace VideoPlayer
{
    public partial class MainForm : Form
    {
        private const string ApiBase = "http://localhost:5000";

        private IGraphBuilder _graphBuilder;
        private IMediaControl _mediaControl;
        private IVideoWindow _videoWindow;

        public MainForm()
        {
            InitializeComponent();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await RefreshVideoListAsync();
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await RefreshVideoListAsync();
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (listBoxVideos.SelectedItem == null) return;
            var blobName = listBoxVideos.SelectedItem.ToString();
            var url = $"{ApiBase}/api/video/{blobName}";
            PlayVideo(url);
        }

        private async System.Threading.Tasks.Task RefreshVideoListAsync()
        {
            try
            {
                using var client = new HttpClient();
                var json = await client.GetStringAsync($"{ApiBase}/api/video/list");
                var names = JsonConvert.DeserializeObject<List<string>>(json);
                listBoxVideos.Items.Clear();
                foreach (var name in names)
                    listBoxVideos.Items.Add(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch video list: {ex.Message}");
            }
        }

        private void PlayVideo(string url)
        {
            StopPlayback();

            try
            {
                _graphBuilder = (IGraphBuilder)new FilterGraph();
                _mediaControl = (IMediaControl)_graphBuilder;
                _videoWindow = (IVideoWindow)_graphBuilder;

                int hr = _graphBuilder.RenderFile(url, null);
                DsError.ThrowExceptionForHR(hr);

                _videoWindow.put_Owner(videoPanel.Handle);
                _videoWindow.put_WindowStyle(WindowStyle.Child | WindowStyle.ClipChildren);
                _videoWindow.SetWindowPosition(0, 0, videoPanel.Width, videoPanel.Height);

                _mediaControl.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playback error: {ex.Message}");
                StopPlayback();
            }
        }

        private void StopPlayback()
        {
            try
            {
                _mediaControl?.Stop();

                if (_videoWindow != null)
                {
                    _videoWindow.put_Visible(OABool.False);
                    _videoWindow.put_Owner(IntPtr.Zero);
                }
            }
            finally
            {
                if (_graphBuilder != null)
                {
                    Marshal.ReleaseComObject(_graphBuilder);
                    _graphBuilder = null;
                    _mediaControl = null;
                    _videoWindow = null;
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopPlayback();
        }
    }
}
```

- [ ] **Step 2: Build VideoPlayer**

```bash
dotnet build VideoPlayer/VideoPlayer.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Run all tests one final time**

```bash
dotnet test VideoSolution.slnx
```

Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add VideoPlayer/MainForm.cs
git commit -m "feat: VideoPlayer DirectShow HTTP streaming playback"
```

---

## End-to-End Smoke Test

Prerequisites: Azurite is running (`azurite --silent --location ./azurite-data`), at least one `.mp4` file is in `C:\videos`.

1. Start VideoApi: `dotnet run --project VideoApi/VideoApi.csproj`
2. Upload files: `dotnet run --project VideoUploader/VideoUploader.csproj`
3. Verify list: `curl http://localhost:5000/api/video/list` — should return JSON array
4. Verify stream: `curl -I http://localhost:5000/api/video/<name>.mp4` — should return `Accept-Ranges: bytes`
5. Run VideoPlayer: `dotnet run --project VideoPlayer/VideoPlayer.csproj`
6. Click Refresh — list populates, select a video, click Play — video plays
