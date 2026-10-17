# Trimui Smart Hub

**Trimui Smart Hub** is an unofficial Windows tool to manage the games on your Trimui device (Smart Pro, Brick and other devices running the stock firmware or CrossMix-OS).

Your collection lives in a **library on your PC**: you organize it, download box arts and fix names without the device connected, then **sync** it to the device when you plug it in. Saves are backed up automatically every time you connect.

<p align="center"> <img src="https://github.com/user-attachments/assets/a64da849-b48a-488d-8671-984686dca669" width="550px" /> <br> <img src="https://github.com/user-attachments/assets/23e613ea-ce6d-4687-a65e-5e02baa94fa2" width="550px" /> </p>

## Features

- **Works without the device**
  The library (ROMs, box arts, BIOS, save backups) is a regular folder on your PC. Everything except syncing works offline from the device.

- **Device detection by content**
  The device is recognized by its SD card structure, not by its USB name: USB storage mode, an SD card in a card reader and custom firmwares all work. Detection uses Windows volume events, so nothing runs while idle.

- **Safe sync, with preview**
  Compare the library with the device, review the changes and apply. Copies are written to a temporary file and renamed when complete, so unplugging the cable never leaves a broken file; sync again to continue where it stopped. Nothing is deleted from the device unless you select it.

- **Renames keep your saves**
  When you rename a ROM in the library, the next sync renames the ROM, its image *and its saves/save states* on the device, instead of copying a new file and orphaning your progress.

- **Box arts that match**
  ROMs are identified by checksum (CRC32) against a bundled database of ~500k games, so `mario3.zip` maps to *Super Mario Bros. 3 (USA)* and its exact libretro thumbnail. Uncertain matches go to a review list instead of being applied blindly. Works on the library or directly on the device.

- **Save backups**
  Saves and save states are copied to the PC every time the device is connected (only when something changed), with configurable retention. Restore is always manual and backs up the current saves first.

- **BIOS checker**
  Shows which emulators are missing required BIOS files and validates them against known good dumps (MD5), in the library and on the device.

- **Library tools**
  Import by drag & drop (the platform is detected from the file or its checksum), rename to official names, find duplicates, clean orphan images, storage usage per platform.

- **Downloads** *(optional)*
  Search and download games from a third-party source into the library. Only download games you own.

- **Background tasks**
  Long operations run in a task queue with progress and cancellation, so the app stays responsive.

- English and Portuguese (Brazil).

## Getting started

1. Download the latest release and run `TrimuiSmartHub.exe`.
2. Connect your device (Apps → *USB Storage*) or put its SD card in a card reader.
3. On the **Device** page, click **Import from device** to build your library from what is already on the card.
4. Organize the library, download box arts, then **Sync to device**.

## Where data is stored

| What | Where |
|---|---|
| Library (ROMs, box arts, BIOS, save backups) | `%USERPROFILE%\Trimui Smart Hub` by default (configurable) |
| Settings | `%APPDATA%\TrimuiSmartHub\settings.json` |
| Indexed game database, caches, logs | `%LOCALAPPDATA%\TrimuiSmartHub` |
| Device id and name | `.trimui-smart-hub.json` at the root of the SD card |

## Building

Requires the .NET 8 SDK on Windows.

```bash
dotnet build TrimuiSmartHub.sln
dotnet test TrimuiSmartHub.Core.Tests
```

Releases are built by GitHub Actions when a `v*` tag is pushed.

### Project structure

- `TrimuiSmartHub.Core` — platform-independent logic (no WPF): platforms, device layouts, library, sync, saves, scraping, BIOS. Supported platforms and their BIOS are defined in `Platforms/platforms.json`.
- `TrimuiSmartHub.Application` — WPF app (MVVM with CommunityToolkit.Mvvm, MahApps.Metro).
- `TrimuiSmartHub.Core.Tests` — unit tests.

See [docs/ROADMAP.md](docs/ROADMAP.md) for what's next.

## Contributing

Contributions are welcome! If you have suggestions for improvements, feel free to submit an issue or a pull request.

## License

This project is not affiliated with or endorsed by Trimui. All trademarks and copyrights are the property of their respective owners.

**This project does not store any games or copyrighted content. The optional download feature searches a third-party website; if you believe copyrighted material is present there, report it to that website.**
