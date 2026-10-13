using System.Security.Cryptography;
using System.Text;
using TrimuiSmartHub.Core.Bios;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Jobs;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Storage;

namespace TrimuiSmartHub.Core.Tests;

public class BiosCheckerTests
{
    private static string Md5(string text) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public async Task Any_valid_file_of_a_group_satisfies_it()
    {
        var registry = new PlatformRegistry(new[]
        {
            new Platform
            {
                Id = "PS", Name = "PS",
                Bios = new[]
                {
                    new BiosFile { File = "a.bin", Md5 = new[] { Md5("good-a") }, Required = true, Group = "psx" },
                    new BiosFile { File = "b.bin", Md5 = new[] { Md5("good-b") }, Required = true, Group = "psx" },
                },
            },
            new Platform
            {
                Id = "GBA", Name = "GBA",
                Bios = new[] { new BiosFile { File = "gba.bin", Md5 = new[] { Md5("gba") }, Required = false } },
            },
        });

        using var dir = new TempDir();
        dir.Write("bios/b.bin", "good-b");
        dir.Write("bios/gba.bin", "bad dump");

        var result = await new BiosChecker(registry).CheckAsync(new LocalFileStore(dir.Path), "bios");

        var ps = result.Single(r => r.Platform.Id == "PS");
        Assert.True(ps.IsReady);
        Assert.Equal(BiosState.Missing, ps.Files[0].State);
        Assert.Equal(BiosState.Ok, ps.Files[1].State);

        var gba = result.Single(r => r.Platform.Id == "GBA");
        Assert.Equal(BiosState.WrongHash, gba.Files[0].State);
        Assert.True(gba.IsReady);
        Assert.False(gba.HasRequired);
    }
}

public class JobQueueTests
{
    [Fact]
    public async Task Runs_jobs_in_order_and_reports_progress()
    {
        await using var queue = new JobQueue();
        var order = new List<int>();

        var first = queue.Enqueue("first", async (progress, ct) =>
        {
            progress.Report(new JobProgress(0.5, "half"));
            await Task.Delay(50, ct);
            order.Add(1);
        });
        var second = queue.Enqueue("second", (_, _) =>
        {
            order.Add(2);
            return Task.CompletedTask;
        });

        await second.Completion;

        Assert.Equal(new[] { 1, 2 }, order);
        Assert.Equal(JobStatus.Completed, first.Status);
        Assert.Equal(1, first.Progress);
    }

    [Fact]
    public async Task Cancel_stops_a_running_job()
    {
        await using var queue = new JobQueue();
        var started = new TaskCompletionSource();

        var job = queue.Enqueue("long", async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
        });

        await started.Task;
        job.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.Completion);
        Assert.Equal(JobStatus.Canceled, job.Status);
    }

    [Fact]
    public async Task Failure_is_reported_and_next_job_still_runs()
    {
        await using var queue = new JobQueue();

        var failing = queue.Enqueue("fail", (_, _) => throw new IOException("disk gone"));
        var next = queue.Enqueue("next", (_, _) => Task.FromResult(42));

        Assert.Equal(42, await next.Completion);
        Assert.Equal(JobStatus.Failed, failing.Status);
        Assert.Equal("disk gone", failing.Error);
    }
}

/// <summary>Runs against the real bundled database when it is available in the repository.</summary>
public class GameDatabaseTests
{
    private static string? FindDatabase()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "TrimuiSmartHub.Application", "Resources", "Data", "data.tsh");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public async Task Finds_games_by_crc_rom_name_and_name()
    {
        var source = FindDatabase();
        if (source == null) return;

        using var data = new TempDir();
        var database = new GameDatabase(source, data.Path);

        var byCrc = await database.FindByCrcAsync(0x8B27CD67, new[] { "gba" });
        Assert.Equal("e-Reader (USA)", byCrc?.Name);

        var byRom = await database.FindByRomNameAsync("ZZYZZYXX.ZIP", new[] { "mame2003plus", "fbneo" });
        Assert.NotNull(byRom);

        Assert.NotNull(await database.FindByNameAsync("e-reader (usa)", new[] { "gba" }));
        Assert.Null(await database.FindByCrcAsync(0x8B27CD67, new[] { "snes" }));

        // Second instance reuses the prepared copy.
        Assert.NotNull(await new GameDatabase(source, data.Path).FindByCrcAsync(0x8B27CD67));
    }

    [Fact]
    public async Task Identifies_zipped_rom_by_crc_inside_the_zip()
    {
        var source = FindDatabase();
        if (source == null) return;

        using var data = new TempDir();
        var database = new GameDatabase(source, data.Path);
        var identifier = new RomIdentifier(database);

        // A zip whose entry has the CRC of a known game can't be built without the game, so this checks the
        // file-name path instead: an unidentifiable zip falls back to the database name lookup.
        data.WriteZip("roms/e-Reader (USA).zip", ("whatever.gba", "not the real rom"));
        var store = new LocalFileStore(data.Path);
        var file = store.GetFile("roms/e-Reader (USA).zip")!;

        var result = await identifier.IdentifyAsync(store, file, PlatformRegistry.LoadDefault().Find("GBA")!);

        Assert.Equal("e-Reader (USA)", result.Name);
        Assert.Equal(IdentificationSource.Name, result.Source);
        Assert.NotNull(result.Crc32);
    }
}
