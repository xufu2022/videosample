# VideoPlayer: WebView2 HTML Video Player

**Date:** 2026-04-07  
**Status:** Approved

## Summary

Replace the DirectShow COM interop video player with a WebView2 control hosting an HTML `<video>` element. When the user selects a video and clicks Play, C# generates an HTML string pointing to the API stream URL and calls `NavigateToString`. The Chromium engine inside WebView2 handles HTTP Range requests, buffering, and seeking. Native browser video controls (play/pause, seek bar, volume, fullscreen) are used.

## Components Changed

### `VideoPlayer.csproj`
- Remove the local `DirectShowLib.dll` reference (currently pulled from `bin/Debug/net48/`).
- `Microsoft.Web.WebView2` is already present — no new packages needed.

### `MainForm.Designer.cs`
- Remove `Panel videoPanel` field.
- Add `Microsoft.Web.WebView2.WinForms.WebView2 webView` field, docked to fill, replacing videoPanel.

### `MainForm.cs`
- Remove all DirectShow fields: `_graphBuilder`, `_mediaControl`, `_videoWindow`.
- Remove `PlayVideo(string url)` and `StopPlayback()` methods and all `using DirectShowLib` / `using System.Runtime.InteropServices` imports.
- Add `async Task InitializeAsync()` called from `MainForm_Load` to call `webView.EnsureCoreWebView2Async()`. Disable `btnPlay` until init completes.
- Replace `btnPlay_Click` body: build HTML string with `<video controls autoplay>` sourced at the API URL, call `webView.NavigateToString(html)`.
- No explicit cleanup needed — WebView2 is a managed control disposed with the form.

## HTML Template

```html
<!DOCTYPE html>
<html>
<body style="margin:0;background:#000">
  <video controls autoplay style="width:100%;height:100vh"
         src="{url}">
  </video>
</body>
</html>
```

`{url}` is replaced with `http://localhost:5000/api/video/{blobName}` at runtime.

## Data Flow

1. App loads → `InitializeAsync()` → WebView2 ready → `btnPlay` enabled.
2. User selects video from list → clicks Play.
3. C# builds HTML string with stream URL → `webView.NavigateToString(html)`.
4. WebView2 (Chromium) fetches video via HTTP Range requests to the API.
5. Native browser controls appear; user interacts directly with the video element.

## Error Handling

- WebView2 init failure: catch exception in `MainForm_Load`, show `MessageBox`, leave `btnPlay` disabled.
- No video selected: guard in `btnPlay_Click` (existing behavior retained).

## What Is Not Changing

- `VideoApi` — no changes.
- `VideoUploader` — no changes.
- List refresh logic and UI layout (control panel, list box) — unchanged.
