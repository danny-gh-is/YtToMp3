# YtToMp3

A small Windows Forms (.NET 8) wrapper around [yt-dlp](https://github.com/yt-dlp/yt-dlp). Paste a YouTube link and save it to your Downloads folder as either:

- **HQ MP3** – audio extracted and converted at the best quality.
- **Best MP4** – best available video + audio merged into one MP4 (up to 1080p and above when offered).

The log window shows yt-dlp output; after an MP4 download it prints `Saved: <path> [WxH]` with the real resolution.

## Requirements

- Windows, .NET 8 SDK
- `yt-dlp` on PATH (keep it up to date: `yt-dlp -U`)
- `ffmpeg` on PATH (MP3 conversion and MP4 merging)
- A JavaScript runtime such as [deno](https://deno.com) on PATH (needed by yt-dlp for YouTube)

## Build and run

```
dotnet run
```

## How the MP4 format selection works

YouTube returns HTTP 403 for the regular DASH (https) streams when no PO Token is available, which makes the default `bv*+ba` selector fail. The HLS (m3u8) streams download at full resolution without one, so the app uses:

```
bv*[protocol^=m3u8]+ba[protocol^=m3u8]/bv*+ba/b
```

i.e. HLS video+audio first, then regular DASH, then the best single combined file. If HLS ever stops working, the next step is a PO Token provider such as [bgutil-ytdlp-pot-provider](https://github.com/Brainicism/bgutil-ytdlp-pot-provider).

## Layout

- `Program.cs` – the whole app (`MainForm`, process runner, Downloads-folder lookup).
- `Form1.cs` / `Form1.Designer.cs` – unused leftover template form.
