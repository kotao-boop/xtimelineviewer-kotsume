using System.IO.Compression;
using XTimelineViewer.Models;
using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public class SettingsBackupServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public SettingsBackupServiceTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task CreateAndRestore_RoundTripsAllConfigurationWithoutLoginSessions()
    {
        var source = Path.Combine(_tempDir, "source");
        var target = Path.Combine(_tempDir, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        SettingsService.SaveSettings(Path.Combine(source, "settings.json"), new AppSettings { Theme = "Dark" });
        SettingsService.SaveProfiles(Path.Combine(source, "profiles.json"),
            [new ProfileConfig { Id = "work", Name = "Work", ScreenName = "example" }]);
        await TimelineStore.SaveAsync(Path.Combine(source, "timelines.json"),
            [new TimelineConfig { Name = "Home", Url = "https://x.com/home", ProfileId = "work" }]);
        WorkspaceStore.Save(Path.Combine(source, "workspaces.json"),
            [new WorkspaceConfig { Name = "Morning", LayoutMode = "Auto" }]);

        var archive = Path.Combine(_tempDir, "settings.xtvbackup");
        Assert.Equal(4, SettingsBackupService.CreateBackup(archive, source, "2.3.3"));

        using (var zip = ZipFile.OpenRead(archive))
        {
            var names = zip.Entries.Select(e => e.FullName).ToArray();
            Assert.Contains("backup.json", names);
            Assert.DoesNotContain(names, n => n.Contains("cookie", StringComparison.OrdinalIgnoreCase));
            using var reader = new StreamReader(zip.GetEntry("backup.json")!.Open());
            Assert.Contains("\"IncludesLoginSessions\": false", reader.ReadToEnd());
        }

        var result = SettingsBackupService.RestoreBackup(archive, target, "2.3.3");
        Assert.Equal(4, result.RestoredFileCount);
        Assert.Equal("Dark", SettingsService.LoadSettings(Path.Combine(target, "settings.json")).Theme);
        Assert.Equal("example", SettingsService.LoadProfiles(Path.Combine(target, "profiles.json"))[0].ScreenName);
        Assert.Equal("Home", TimelineStore.Load(Path.Combine(target, "timelines.json"))[0].Name);
        Assert.Equal("Morning", WorkspaceStore.Load(Path.Combine(target, "workspaces.json"))[0].Name);
    }

    [Fact]
    public void Restore_CreatesSafetyBackupBeforeReplacingExistingSettings()
    {
        var source = Path.Combine(_tempDir, "source");
        var target = Path.Combine(_tempDir, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        SettingsService.SaveSettings(Path.Combine(source, "settings.json"), new AppSettings { Theme = "Dark" });
        SettingsService.SaveSettings(Path.Combine(target, "settings.json"), new AppSettings { Theme = "Light" });
        var archive = Path.Combine(_tempDir, "settings.xtvbackup");
        SettingsBackupService.CreateBackup(archive, source, "2.3.3");

        var result = SettingsBackupService.RestoreBackup(archive, target, "2.3.3");

        Assert.NotNull(result.SafetyBackupPath);
        Assert.True(File.Exists(result.SafetyBackupPath));
        Assert.Equal("Dark", SettingsService.LoadSettings(Path.Combine(target, "settings.json")).Theme);
    }

    [Fact]
    public void Restore_InvalidJson_DoesNotReplaceCurrentSettings()
    {
        var target = Path.Combine(_tempDir, "target");
        Directory.CreateDirectory(target);
        SettingsService.SaveSettings(Path.Combine(target, "settings.json"), new AppSettings { Theme = "Light" });
        var archive = Path.Combine(_tempDir, "bad.xtvbackup");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            Write(zip, "backup.json", """{"FormatVersion":1,"AppVersion":"2.3.3","CreatedAtUtc":"2026-08-29T00:00:00Z","IncludesLoginSessions":false,"Files":["settings.json"]}""");
            Write(zip, "settings.json", "not json");
        }

        Assert.Throws<InvalidDataException>(() => SettingsBackupService.RestoreBackup(archive, target, "2.3.3"));
        Assert.Equal("Light", SettingsService.LoadSettings(Path.Combine(target, "settings.json")).Theme);
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }
}
