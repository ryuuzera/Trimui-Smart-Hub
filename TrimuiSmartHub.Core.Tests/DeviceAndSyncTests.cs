using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Library;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Saves;
using TrimuiSmartHub.Core.Storage;
using TrimuiSmartHub.Core.Sync;

namespace TrimuiSmartHub.Core.Tests;

public class DeviceLayoutTests
{
    [Fact]
    public void Detects_stock_card_and_reads_emulator_configs()
    {
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA", "FC");

        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        Assert.Equal("trimui-stock", device.Layout.Id);
        var platforms = device.Layout.GetPlatforms(device.Store);
        Assert.Equal(new[] { "FC", "GBA" }, platforms.Select(p => p.PlatformId));
        Assert.Equal("Roms/GBA", platforms[1].RomsDir);
        Assert.Equal("Imgs/GBA", platforms[1].ImagesDir);
        Assert.Contains("gba", platforms[1].Extensions);
    }

    [Fact]
    public void Detects_crossmix_and_its_version()
    {
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");
        card.Write(CrossMixLayout.VersionFile, "1.2.1\n");

        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        Assert.Equal("crossmix", device.Layout.Id);
        Assert.Equal("CrossMix-OS 1.2.1", device.FirmwareDisplay);
    }

    [Fact]
    public void Ignores_drives_that_are_not_devices()
    {
        using var folder = new TempDir();
        folder.Write("Emus/readme.txt", "no configs here");

        Assert.Null(DeviceDetector.CreateDefault().TryOpen(folder.Path));
    }

    [Fact]
    public void Identity_is_stable_across_connections()
    {
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");

        var first = DeviceDetector.CreateDefault().TryOpen(card.Path)!;
        var second = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        Assert.Equal(first.Id, second.Id);
        Assert.True(card.Exists(DeviceIdentity.FileName));
    }

    [Theory]
    [InlineData("GBA_cache6.db", true)]
    [InlineData("FC_cache7.db", true)]
    [InlineData("Pokemon.gba", false)]
    [InlineData("cache.txt", false)]
    public void Recognizes_game_list_caches(string fileName, bool expected)
    {
        Assert.Equal(expected, TrimuiStockLayout.IsGameListCache(fileName));
    }

    [Fact]
    public void Save_dirs_are_only_the_existing_ones()
    {
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");
        card.Sub("RetroArch/.retroarch/saves");

        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        Assert.Equal(new[] { "RetroArch/.retroarch/saves" }, device.Layout.GetSaveDirs(device.Store));
    }
}

public class SyncTests
{
    private static readonly DateTime OldTime = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static LibraryService NewLibrary(TempDir dir) => new(dir.Path, PlatformRegistry.LoadDefault(), database: null);

    [Fact]
    public async Task Copies_new_files_offers_orphans_and_never_touches_caches()
    {
        using var libraryDir = new TempDir();
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");
        card.Write("Roms/GBA/OnlyOnDevice.gba", "x");
        card.Write("Roms/GBA/GBA_cache6.db", "cache");

        var library = NewLibrary(libraryDir);
        libraryDir.Write("roms/GBA/Game.gba", "rom-content");
        libraryDir.Write("media/GBA/boxart/Game.png", "png");
        libraryDir.Write("bios/gba_bios.bin", "bios");
        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        var plan = await new SyncPlanner().PlanToDeviceAsync(library, device);

        Assert.Contains(plan.Items, i => i.Kind == SyncActionKind.Copy && i.TargetPath == "Roms/GBA/Game.gba");
        Assert.Contains(plan.Items, i => i.Kind == SyncActionKind.Copy && i.TargetPath == "Imgs/GBA/Game.png");
        Assert.Contains(plan.Items, i => i.Kind == SyncActionKind.Copy && i.TargetPath == "BIOS/gba_bios.bin");

        var orphan = Assert.Single(plan.OnlyOnTarget);
        Assert.Equal("Roms/GBA/OnlyOnDevice.gba", orphan.TargetPath);
        Assert.False(orphan.Selected);
        Assert.DoesNotContain(plan.Items.Concat(plan.OnlyOnTarget), i => i.TargetPath.EndsWith(".db"));

        var result = await new SyncExecutor().ExecuteAsync(plan, device);

        Assert.Empty(result.Failures);
        Assert.Equal("rom-content", card.Read("Roms/GBA/Game.gba"));
        Assert.True(card.Exists("Roms/GBA/OnlyOnDevice.gba"));
        Assert.False(card.Exists("Roms/GBA/GBA_cache6.db"));
        Assert.Equal(1, result.CachesInvalidated);

        var second = await new SyncPlanner().PlanToDeviceAsync(library, device);
        Assert.Empty(second.Items);
    }

    [Fact]
    public async Task Updates_changed_files_and_deletes_only_when_selected()
    {
        using var libraryDir = new TempDir();
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");
        card.Write("Roms/GBA/Game.gba", "old", OldTime);
        card.Write("Roms/GBA/Removed.gba", "x");

        var library = NewLibrary(libraryDir);
        libraryDir.Write("roms/GBA/Game.gba", "new-version");
        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        var plan = await new SyncPlanner().PlanToDeviceAsync(library, device);
        Assert.Contains(plan.Items, i => i.Kind == SyncActionKind.Update && i.TargetPath == "Roms/GBA/Game.gba");

        plan.OnlyOnTarget.Single().Selected = true;
        await new SyncExecutor().ExecuteAsync(plan, device);

        Assert.Equal("new-version", card.Read("Roms/GBA/Game.gba"));
        Assert.False(card.Exists("Roms/GBA/Removed.gba"));
    }

    [Fact]
    public async Task Skips_platforms_not_installed_on_the_device()
    {
        using var libraryDir = new TempDir();
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");

        var library = NewLibrary(libraryDir);
        libraryDir.Write("roms/SFC/Mario.sfc", "x");
        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        var plan = await new SyncPlanner().PlanToDeviceAsync(library, device);

        Assert.Empty(plan.Items);
        Assert.Contains(plan.Warnings, w => w.StartsWith("SFC"));
    }

    [Fact]
    public async Task Renaming_in_the_library_renames_rom_image_and_saves_on_the_device()
    {
        using var libraryDir = new TempDir();
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");
        card.Write("Roms/GBA/pkmn.gba", "rom");
        card.Write("Imgs/GBA/pkmn.png", "png");
        card.Write("RetroArch/.retroarch/saves/pkmn.srm", "battery");
        card.Write("RetroArch/.retroarch/states/pkmn.state1", "state");
        card.Write("RetroArch/.retroarch/states/pkmn.state.auto.png", "thumb");
        card.Write("RetroArch/.retroarch/saves/pkmn (hack).srm", "other game");

        var library = NewLibrary(libraryDir);
        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        await new SyncExecutor().ExecuteAsync(await new SyncPlanner().PlanFromDeviceAsync(library, device));
        var rom = library.GetRoms("GBA").Single();
        library.Rename(rom, "Pokemon - Emerald Version (USA, Europe)");

        var plan = await new SyncPlanner().PlanToDeviceAsync(library, device);

        Assert.DoesNotContain(plan.Items, i => i.Kind is SyncActionKind.Copy or SyncActionKind.Update);
        Assert.Equal(5, plan.Items.Count(i => i.Kind == SyncActionKind.Rename));

        await new SyncExecutor().ExecuteAsync(plan, device);

        const string newName = "Pokemon - Emerald Version (USA, Europe)";
        Assert.True(card.Exists($"Roms/GBA/{newName}.gba"));
        Assert.True(card.Exists($"Imgs/GBA/{newName}.png"));
        Assert.Equal("battery", card.Read($"RetroArch/.retroarch/saves/{newName}.srm"));
        Assert.True(card.Exists($"RetroArch/.retroarch/states/{newName}.state1"));
        Assert.True(card.Exists($"RetroArch/.retroarch/states/{newName}.state.auto.png"));
        Assert.True(card.Exists("RetroArch/.retroarch/saves/pkmn (hack).srm"));
        Assert.False(card.Exists("Roms/GBA/pkmn.gba"));

        var again = await new SyncPlanner().PlanToDeviceAsync(library, device);
        Assert.Empty(again.Items);
    }

    [Fact]
    public async Task Import_from_device_never_deletes_and_is_idempotent()
    {
        using var libraryDir = new TempDir();
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");
        card.Write("Roms/GBA/A.gba", "a");
        card.Write("Imgs/GBA/A.png", "img");
        card.Write("BIOS/gba_bios.bin", "bios");

        var library = NewLibrary(libraryDir);
        libraryDir.Write("roms/GBA/OnlyInLibrary.gba", "keep");
        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;

        var plan = await new SyncPlanner().PlanFromDeviceAsync(library, device);
        Assert.Empty(plan.OnlyOnTarget);
        await new SyncExecutor().ExecuteAsync(plan, device);

        Assert.True(libraryDir.Exists("roms/GBA/A.gba"));
        Assert.True(libraryDir.Exists("media/GBA/boxart/A.png"));
        Assert.True(libraryDir.Exists("bios/gba_bios.bin"));
        Assert.True(libraryDir.Exists("roms/GBA/OnlyInLibrary.gba"));

        Assert.Empty((await new SyncPlanner().PlanFromDeviceAsync(library, device)).Items);
    }
}

public class SaveBackupTests
{
    [Fact]
    public async Task Backs_up_only_when_saves_change_and_restores()
    {
        using var libraryDir = new TempDir();
        using var card = new TempDir();
        card.MakeTrimuiCard("GBA");
        card.Write("RetroArch/.retroarch/saves/Game.srm", "v1");

        var library = new LibraryService(libraryDir.Path, PlatformRegistry.LoadDefault(), null);
        var device = DeviceDetector.CreateDefault().TryOpen(card.Path)!;
        var service = new SaveBackupService(library);

        var first = await service.BackupAsync(device);
        Assert.NotNull(first);
        Assert.Null(await service.BackupAsync(device));

        await Task.Delay(1100); // backup folders are named by second
        card.Write("RetroArch/.retroarch/saves/Game.srm", "v2-longer", DateTime.UtcNow.AddMinutes(1));
        var second = await service.BackupAsync(device);
        Assert.NotNull(second);
        Assert.Equal(2, service.ListBackups(device.Id).Count);

        await Task.Delay(1100);
        card.Write("RetroArch/.retroarch/saves/Game.srm", "corrupted", DateTime.UtcNow.AddMinutes(2));
        var restored = await service.RestoreAsync(device, first!);

        Assert.Equal(1, restored);
        Assert.Equal("v1", card.Read("RetroArch/.retroarch/saves/Game.srm"));
        // The corrupted state was backed up before being overwritten.
        Assert.Equal(3, service.ListBackups(device.Id).Count);
    }

    [Fact]
    public void Retention_keeps_latest_and_removes_incomplete_backups()
    {
        using var libraryDir = new TempDir();
        var library = new LibraryService(libraryDir.Path, PlatformRegistry.LoadDefault(), null);
        var service = new SaveBackupService(library);

        for (var day = 1; day <= 5; day++)
        {
            var stamp = $"2024-03-0{day}_120000";
            libraryDir.Write($"backups/saves/dev1/{stamp}/manifest.json",
                $$"""{"DeviceId":"dev1","Stamp":"{{stamp}}","CreatedUtc":"2024-03-0{{day}}T12:00:00Z","Files":[]}""");
        }
        libraryDir.Sub("backups/saves/dev1/2024-01-01_000000");

        var deleted = new List<string>();
        var removed = service.ApplyRetention("dev1", new SaveRetention { KeepLatest = 2, KeepMonthly = 12 }, deleted.Add);

        // Keeps the 2 latest (05, 04) and the oldest of March (01); drops 02, 03 and the incomplete one.
        Assert.Equal(3, removed);
        Assert.Contains(deleted, d => d.EndsWith("2024-03-02_120000"));
        Assert.Contains(deleted, d => d.EndsWith("2024-03-03_120000"));
        Assert.Contains(deleted, d => d.EndsWith("2024-01-01_000000"));
    }
}

public class LibraryImportTests
{
    [Fact]
    public async Task Imports_by_extension_and_by_extension_inside_zip()
    {
        using var libraryDir = new TempDir();
        using var downloads = new TempDir();
        var library = new LibraryService(libraryDir.Path, PlatformRegistry.LoadDefault(), null);

        var gba = downloads.Write("Game.gba", "rom");
        var zip = downloads.WriteZip("Mario.zip", ("Mario (USA).sfc", "snes"));
        var unknown = downloads.Write("Disc.iso", "iso");

        var outcomes = await library.ImportAsync(new[] { gba, zip, unknown });

        Assert.Equal(ImportStatus.Imported, outcomes[0].Status);
        Assert.Equal("SFC", outcomes[1].Platform!.Id);
        Assert.Equal(ImportStatus.Unresolved, outcomes[2].Status);
        Assert.True(libraryDir.Exists("roms/GBA/Game.gba"));
        Assert.True(libraryDir.Exists("roms/SFC/Mario.zip"));

        var again = await library.ImportAsync(new[] { gba });
        Assert.Equal(ImportStatus.AlreadyExists, again[0].Status);
    }

    [Fact]
    public async Task Uses_platform_hint_for_ambiguous_files_and_extracts_when_needed()
    {
        using var libraryDir = new TempDir();
        using var downloads = new TempDir();
        var library = new LibraryService(libraryDir.Path, PlatformRegistry.LoadDefault(), null);

        var zip = downloads.WriteZip("Crash.zip", ("Crash (USA).cue", "cue"), ("Crash (USA).bin", "bin"));

        var outcome = (await library.ImportAsync(new[] { zip }, platformHint: "PS")).Single();

        Assert.Equal(ImportStatus.Imported, outcome.Status);
        Assert.True(libraryDir.Exists("roms/PS/Crash (USA).cue"));
        Assert.True(libraryDir.Exists("roms/PS/Crash (USA).bin"));
        Assert.False(libraryDir.Exists("roms/PS/Crash.zip"));
    }

    [Fact]
    public async Task Rejects_zip_entries_escaping_the_rom_folder()
    {
        using var libraryDir = new TempDir();
        using var downloads = new TempDir();
        var library = new LibraryService(libraryDir.Path, PlatformRegistry.LoadDefault(), null);

        var zip = downloads.WriteZip("Evil.zip", ("../../../evil.bin", "x"));

        var outcome = (await library.ImportAsync(new[] { zip }, platformHint: "PS")).Single();

        Assert.Equal(ImportStatus.Failed, outcome.Status);
        Assert.False(File.Exists(Path.Combine(libraryDir.Path, "..", "evil.bin")));
    }

    [Fact]
    public void Lists_roms_with_boxart_and_orphans()
    {
        using var libraryDir = new TempDir();
        var library = new LibraryService(libraryDir.Path, PlatformRegistry.LoadDefault(), null);
        libraryDir.Write("roms/GBA/A.gba", "a");
        libraryDir.Write("roms/GBA/B.gba", "b");
        libraryDir.Write("roms/GBA/Thumbs.db", "x");
        libraryDir.Write("media/GBA/boxart/A.png", "img");
        libraryDir.Write("media/GBA/boxart/Gone.png", "img");

        var roms = library.GetRoms("GBA");
        var summary = library.GetPlatformSummaries().Single();

        Assert.Equal(new[] { "A.gba", "B.gba" }, roms.Select(r => r.File.Name));
        Assert.NotNull(roms[0].BoxartPath);
        Assert.Null(roms[1].BoxartPath);
        Assert.Equal(1, summary.MissingBoxart);
        Assert.Equal("Gone.png", library.GetOrphanBoxarts("GBA").Single().Name);
    }
}
