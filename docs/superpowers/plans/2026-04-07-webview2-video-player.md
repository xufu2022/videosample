# WebView2 HTML Video Player Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace DirectShow COM interop video playback in VideoPlayer with a WebView2 control that hosts an HTML `<video>` element sourced from the API stream URL.

**Architecture:** A `WebView2` WinForms control docks to fill the form area previously occupied by `videoPanel`. On Play click, C# generates an HTML string with a `<video controls autoplay>` tag pointing to the API URL and calls `NavigateToString`. WebView2's Chromium engine handles all HTTP Range streaming and buffering; native browser video controls handle the user interaction.

**Tech Stack:** .NET 4.8, WinForms, Microsoft.Web.WebView2 1.0.2792.45 (already in csproj), C#

---

### Task 1: Remove DirectShow from the project file

**Files:**
- Modify: `VideoPlayer/VideoPlayer.csproj`

This removes the local `DirectShowLib.dll` reference that is no longer needed. `Microsoft.Web.WebView2` is already present.

- [ ] **Step 1: Open `VideoPlayer/VideoPlayer.csproj` and verify current content**

The file currently looks like:
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
    <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2792.45" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="System.Net.Http" />
  </ItemGroup>
</Project>
```

Note: `DirectShowLib` is referenced as a local DLL (copied to bin), not as a NuGet package. It will be removed by deleting the physical reference in the next step and cleaning the bin output.

- [ ] **Step 2: Verify DirectShowLib.dll is only a bin artifact**

Run:
```bash
grep -r "DirectShow" VideoPlayer/VideoPlayer.csproj
```
Expected: no output (it is not in the csproj — it was added as a manual bin reference, not via the project file). The DLL in `VideoPlayer/bin/Debug/net48/DirectShowLib.dll` is a leftover artifact.

- [ ] **Step 3: Build to confirm the project already compiles without a DirectShowLib csproj entry**

Run:
```bash
cd VideoPlayer && dotnet build -c Debug
```
Expected: build succeeds (DirectShowLib is imported via `using DirectShowLib` in `MainForm.cs` — this will fail until Task 3 removes those usings, but confirms the csproj itself needs no changes).

> **Note:** The build will fail here because `MainForm.cs` still references DirectShow types. That is expected — it will be fixed in Task 3. The point of this task is confirming no csproj edit is needed for the DLL reference.

- [ ] **Step 4: Commit (csproj unchanged — document the finding)**

```bash
git commit --allow-empty -m "chore: DirectShowLib not in csproj, removal is code-only in Tasks 2-3"
```

---

### Task 2: Replace videoPanel with WebView2 in the designer

**Files:**
- Modify: `VideoPlayer/MainForm.Designer.cs`

Swap the `Panel videoPanel` control for a `Microsoft.Web.WebView2.WinForms.WebView2 webView` control docked to fill.

- [ ] **Step 1: Replace the designer file content**

Replace the entire content of `VideoPlayer/MainForm.Designer.cs` with:

```csharp
namespace VideoPlayer
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.ListBox listBoxVideos;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnPlay;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
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
            this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
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
            this.btnPlay.Enabled = false;
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);

            // listBoxVideos
            this.listBoxVideos.Dock = System.Windows.Forms.DockStyle.Top;
            this.listBoxVideos.Height = 160;

            // webView
            this.webView.Dock = System.Windows.Forms.DockStyle.Fill;

            // MainForm
            this.Text = "Video Player";
            this.ClientSize = new System.Drawing.Size(800, 560);
            this.Controls.Add(this.webView);
            this.Controls.Add(this.listBoxVideos);
            this.Controls.Add(this.controlPanel);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
        }
    }
}
```

Key changes from the original:
- `Panel videoPanel` → `WebView2 webView`
- `webView.Dock = DockStyle.Fill` (same as videoPanel had)
- `btnPlay.Enabled = false` (disabled until WebView2 async init completes)
- `Controls.Add(this.webView)` replaces `Controls.Add(this.videoPanel)`

- [ ] **Step 2: Attempt build (expect compile errors from MainForm.cs)**

Run:
```bash
cd VideoPlayer && dotnet build -c Debug 2>&1 | head -40
```
Expected: errors about `DirectShowLib`, `IGraphBuilder`, `IMediaControl`, `IVideoWindow`, `Marshal`, etc. — these come from `MainForm.cs` which is updated in Task 3. The designer change itself should not introduce new errors.

---

### Task 3: Rewrite MainForm.cs to use WebView2

**Files:**
- Modify: `VideoPlayer/MainForm.cs`

Remove all DirectShow fields, imports, and methods. Add WebView2 async initialization. Replace `PlayVideo` with `NavigateToString`.

- [ ] **Step 1: Replace the entire content of `VideoPlayer/MainForm.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace VideoPlayer
{
    public partial class MainForm : Form
    {
        private const string ApiBase = "http://localhost:5000";
        private static readonly HttpClient _httpClient = new HttpClient();

        public MainForm()
        {
            InitializeComponent();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await InitializeWebViewAsync();
            await RefreshVideoListAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);
                btnPlay.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 initialization failed: {ex.Message}");
            }
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

        private async Task RefreshVideoListAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync($"{ApiBase}/api/video/list");
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
            var html = $@"<!DOCTYPE html>
<html>
<body style=""margin:0;background:#000"">
  <video controls autoplay style=""width:100%;height:100vh""
         src=""{url}"">
  </video>
</body>
</html>";
            webView.NavigateToString(html);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // WebView2 is a managed control; disposed automatically with the form.
        }
    }
}
```

Removed compared to original:
- `using DirectShowLib;`
- `using System.Runtime.InteropServices;`
- `IGraphBuilder _graphBuilder`, `IMediaControl _mediaControl`, `IVideoWindow _videoWindow` fields
- `StopPlayback()` method
- All COM interop in `PlayVideo()`
- COM cleanup in `MainForm_FormClosing`

Added:
- `using System.Threading.Tasks;`
- `InitializeWebViewAsync()` — calls `EnsureCoreWebView2Async`, enables btnPlay on success
- `PlayVideo(string url)` — builds HTML string, calls `webView.NavigateToString(html)`

- [ ] **Step 2: Build the project**

Run:
```bash
cd VideoPlayer && dotnet build -c Debug
```
Expected output ends with:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 3: Commit**

```bash
git add VideoPlayer/MainForm.Designer.cs VideoPlayer/MainForm.cs
git commit -m "feat: replace DirectShow with WebView2 HTML video player"
```

---

### Task 4: Smoke test end-to-end

**Files:** none (manual verification)

- [ ] **Step 1: Start the API**

In a terminal:
```bash
cd VideoApi && dotnet run
```
Expected: API listening on `http://localhost:5000`

- [ ] **Step 2: Start the VideoPlayer**

In another terminal:
```bash
cd VideoPlayer && dotnet run
```
Expected: form opens, Play button is disabled briefly then becomes enabled once WebView2 initialises.

- [ ] **Step 3: Verify video list loads**

Expected: `listBoxVideos` populates with blob names from the API.

- [ ] **Step 4: Select a video and click Play**

Expected:
- The WebView2 area renders a black page with a Chromium `<video>` player.
- Video begins playing automatically (autoplay).
- Native controls (play/pause, seek bar, volume, fullscreen button) are visible.
- Seeking works (scrubbing the seek bar sends Range requests to the API).

- [ ] **Step 5: Verify no COM errors or crashes on form close**

Close the form. Expected: clean exit, no exception dialogs.

- [ ] **Step 6: Commit smoke test result note**

```bash
git commit --allow-empty -m "chore: WebView2 video player smoke tested OK"
```
