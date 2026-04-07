# Video Upload, Stream & Play — Design Spec

**Date:** 2026-04-06

## Overview

Three .NET projects that together upload `.mp4` files to a local Azure Blob Storage emulator (Azurite), serve them as HTTP streams, and play them in a WinForms application.

```
VideoUploader (Console, .NET 10)
        |
        v
  Azurite (Azure Blob Emulator)
        |
        v
  VideoApi (Web API, .NET 10)  <--- DirectShow HTTP stream
        |
        v
  VideoPlayer (WinForms, .NET 4.8)
```

---

## Project 1: VideoUploader (.NET 10 Console)

### Purpose
Scans `C:\videos` at launch and uploads all `.mp4` files to the Azurite `videos` blob container.

### Behaviour
- On startup, enumerate all `*.mp4` files in `C:\videos`
- For each file:
  - Call `BlobClient.ExistsAsync()` — skip if already uploaded
  - Upload via `BlobClient.UploadAsync()` if not present
  - Print status per file (skipped / uploaded)
- Exit when all files are processed

### Dependencies
- `Azure.Storage.Blobs` NuGet package
- Connection string: `UseDevelopmentStorage=true` (Azurite default)
- Blob container: `videos` — auto-created if missing

---

## Project 2: VideoApi (.NET 10 Web API)

### Purpose
Exposes HTTP endpoints to list blobs and stream video bytes from Azurite to clients.

### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/video/list` | Returns JSON array of blob names in the `videos` container |
| GET | `/api/video/{blobName}` | Streams blob bytes with HTTP Range support |

### Streaming Design
- Reads `Range` request header
- Calls `BlobClient.DownloadStreamingAsync()` with the requested byte range
- Returns `206 Partial Content` for range requests, `200` for full requests
- Response headers: `Content-Type: video/mp4`, `Accept-Ranges: bytes`, `Content-Length`
- Pipes blob stream directly to `Response.Body` — no full load into memory

### Configuration
- Listens on `http://localhost:5000`
- CORS enabled for all origins (VideoPlayer HttpClient + DirectShow HTTP source)
- Connection string: `UseDevelopmentStorage=true`

---

## Project 3: VideoPlayer (.NET Framework 4.8 WinForms)

### Purpose
Presents a list of available videos fetched from VideoApi, and plays the selected video using DirectShow.

### UI Layout
- **ListBox** — populated with blob names from `GET /api/video/list`
- **Refresh button** — re-fetches the video list
- **Play button** — plays the selected video
- **Video panel** — DirectShow renders video into this panel

### Playback Design
- On Play: construct URL `http://localhost:5000/api/video/{selectedName}`
- Pass URL to DirectShow via `IMediaControl` / `IGraphBuilder` using `DirectShowLib-2005` NuGet package
- DirectShow handles HTTP streaming, buffering, and seeking natively
- No temp file download; no byte array management in player code

### Dependencies
- `DirectShowLib-2005` NuGet package
- Targets `net48` (required for DirectShow COM interop)

---

## Data Flow

```
1. VideoUploader scans C:\videos\*.mp4
2. Skips blobs that already exist in Azurite
3. Uploads new files to Azurite "videos" container

4. VideoPlayer calls GET /api/video/list → populates ListBox
5. User selects a video, clicks Play
6. VideoPlayer builds URL: http://localhost:5000/api/video/{name}
7. DirectShow opens HTTP stream to VideoApi
8. VideoApi streams blob bytes from Azurite with Range support
9. DirectShow buffers and renders video in the player panel
```

---

## Key Constraints

- VideoPlayer must stay on `net48` — DirectShow COM interop not supported on .NET 5+
- VideoUploader and VideoApi target `.NET 10`
- Only `.mp4` files are supported
- Azurite must be running before any project is started
- No authentication on API (local dev only)
