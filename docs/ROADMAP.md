# Roadmap

Goal of v1: the app works **without the device connected**. The library on the PC is the source of truth for ROMs, box arts and BIOS; the device is a sync target. Saves are backed up (device → PC), never synced.

## v1

### Phase 0: Stabilization
- [x] Game database shipped with the app and indexed on first run (it was not copied to the output, and had no indexes)
- [x] One shared `HttpClient` with retries (`IHttpClientFactory` + resilience); no more `.Result` blocking calls
- [x] Box art scraper: per-system index downloaded once and cached (was once per ROM), progress, cancellation
- [x] File names with dots handled (`Super Mario Bros. 3.nes`)
- [x] Downloads: region parsed per row, unknown content length, file name sanitization, errors and cancellation
- [x] Device monitor without overlapping timers or leaked event handlers
- [x] CI (build + tests on every push, release on `v*` tags)

### Phase 1: Foundation
- [x] `TrimuiSmartHub.Core` (no WPF) + tests
- [x] DI, logging (Serilog), settings page
- [x] `platforms.json` replaces the hard-coded emulator dictionaries
- [x] Device layouts (`IDeviceLayout`): stock firmware, CrossMix-OS
- [x] Device detection by content via volume events (SD card readers and CFWs work)
- [x] Background job queue with progress and cancellation

### Phase 2: Local library
- [x] Library index (CRC cache, rename journal, manual title choices)
- [x] Import from device
- [x] Drag & drop import with platform detection (extension, zip content, CRC)
- [x] CRC-based identification + fuzzy fallback with manual review
- [x] Library page (virtualized list, filters), duplicates, orphan images
- [x] Library is always accessible; the device is optional

### Phase 3: Sync v1
- [x] Planner (size + date; content compare optional) with preview
- [x] Atomic writes (`.tsh-tmp` → rename), resume by re-running, size checks (free space, FAT32 4 GB limit)
- [x] Never deletes on the device without an explicit selection; caches, OS files and saves are excluded
- [x] Rename propagation: ROM, image and saves/save states renamed on the device
- [x] Game list cache invalidation after sync

### Phase 4: Save backups
- [x] Automatic backup on connect (only when something changed)
- [x] Retention (latest N + one per month)
- [x] Manual restore (current saves backed up first)

### Phase 5: v1 extras
- [x] BIOS checker (MD5) for library and device
- [x] Storage page
- [x] CrossMix-OS profile
- [x] Localization (English, Portuguese)
- [x] Update check against GitHub releases (notifies; the user downloads)

## Known limitations / to verify on real hardware
- Save folder candidates for standalone emulators (DraStic, PPSSPP) and the CrossMix version file path are based on public documentation and should be confirmed on a device.
- The stock firmware game list cache is detected by the `*_cache*.db` name pattern in ROM folders.
- `data.tsh` (57 MB) is committed to git; moving it to Git LFS or a release asset would keep clones small.

## After v1 (by value)
1. Silent auto-update (Velopack) instead of the update notice
2. Image resizing for the device screen (box arts are ~400 KB each; resizing speeds up the menu and saves space)
3. CHD conversion (`chdman`) and `.m3u` generation for multi-disc games
4. ScreenScraper as an additional image/metadata source
5. Network target (SFTP) for CFWs with SSH — `IFileStore` is already the abstraction for it
6. Two-way save sync (with conflict handling)
7. Avalonia UI for Linux/macOS (Core is already platform independent)
8. Decide the future of the download source (plugin outside the main repository)
