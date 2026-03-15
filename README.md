# Batch Image Resizer

A Windows desktop app for batch resizing images across folders and subfolders. Built as an improvement on the PowerToys Image Resizer — which only works on individually selected files — this lets you point it at entire folder trees and get on with your day.

## Features

- **Recursive folder walking** — select one or more folders and it'll process everything inside, including subfolders
- **Resize presets** — common sizes out of the box (Thumbnail up to 4K), or set your own width/height
- **Resize modes** — Fit, Fill, Stretch, Longest Side, Shortest Side, or scale by percentage
- **Output options** — overwrite originals, save to a subfolder, a custom folder, or mirror the original folder structure
- **Format conversion** — keep the original format or convert to JPEG, PNG, WebP, or BMP
- **Quality control** — adjustable quality for JPEG and WebP output
- **Metadata preservation** — keeps EXIF, XMP and ICC profiles by default; optionally strip all metadata
- **Timestamp preservation** — copies the original file's creation and modified dates to the output
- **Filename control** — add a prefix and/or suffix to output filenames
- **Settings are remembered** — all your preferences are saved between sessions

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Windows 10 / 11 (WPF is Windows-only)
- Visual Studio 2022, JetBrains Rider, or VS Code with the C# Dev Kit extension

## Building from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

```bash
git clone https://github.com/JezzWTF/resizer.git
cd resizer
dotnet build src/BatchResizer/BatchResizer.csproj
```

To produce a self-contained single-file executable:

```bash
dotnet publish src/BatchResizer/BatchResizer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./publish
```

## Test data generation

Use the placeholder generator to create a realistic nested photo library for resize and storage testing.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\generate-placeholder-images.ps1
```

What it prompts for:

- destination folder
- number of folders
- images per folder
- resolution preset (`Mixed 1080p + 4K`, `1080p`, `4K`, or custom)
- output format (`jpg` or `png`)
- target JPEG size in MB (approximate, when `jpg` is selected)

Default test profile:

- `Mixed 1080p + 4K` preset (70/30 split)
- `jpg` output
- target JPEG size `3.5 MB`

This mode creates master images once, then copies them into the generated folder tree for fast dataset generation.

## Stack

- .NET 8 / WPF
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) — image processing
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM framework
- [Ookii.Dialogs.Wpf](https://github.com/ookii-dialogs/ookii-dialogs-wpf) — folder picker dialog
