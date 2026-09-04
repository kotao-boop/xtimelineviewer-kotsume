using System;
using System.Collections.Generic;
using System.IO;
using XTimelineViewer.Models;
using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public SettingsServiceTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose()         => Directory.Delete(_tempDir, recursive: true);

    private string At(string filename) => Path.Combine(_tempDir, filename);

    // ── LoadSettings ──────────────────────────────────────────────────────────

    [Fact]
    public void LoadSettings_MissingFile_ReturnsDefaults()
    {
        var s = SettingsService.LoadSettings(At("settings.json"));
        Assert.NotNull(s);
        Assert.Equal("Default", s.Theme);
        Assert.Equal("system",  s.Language);
    }

    [Fact]
    public void LoadSettingsResult_MissingFile_DistinguishesMissingFromEmptyDefaults()
    {
        var result = SettingsService.LoadSettingsResult(At("settings.json"));

        Assert.Equal(PersistenceLoadStatus.Missing, result.Status);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Default", result.Value.Theme);
    }

    [Fact]
    public void LoadSettings_ValidJson_ReturnsCorrectValues()
    {
        File.WriteAllText(At("settings.json"),
            """{"Theme":"Dark","Language":"ja-JP"}""");

        var s = SettingsService.LoadSettings(At("settings.json"));
        Assert.Equal("Dark",  s.Theme);
        Assert.Equal("ja-JP", s.Language);
    }

    [Fact]
    public void LoadSettings_InvalidJson_ReturnsDefaults()
    {
        File.WriteAllText(At("settings.json"), "not { valid json }");

        var s = SettingsService.LoadSettings(At("settings.json"));
        Assert.NotNull(s);
        Assert.Equal("Default", s.Theme);
    }

    [Fact]
    public void LoadSettingsResult_InvalidJson_ReportsCorrupt()
    {
        File.WriteAllText(At("settings.json"), "not { valid json }");

        var result = SettingsService.LoadSettingsResult(At("settings.json"));

        Assert.Equal(PersistenceLoadStatus.Corrupt, result.Status);
        Assert.IsType<System.Text.Json.JsonException>(result.Error);
        Assert.Equal("Default", result.Value.Theme);
    }

    [Fact]
    public void LoadSettingsResult_NullRoot_ReportsCorrupt()
    {
        File.WriteAllText(At("settings.json"), "null");

        var result = SettingsService.LoadSettingsResult(At("settings.json"));

        Assert.Equal(PersistenceLoadStatus.Corrupt, result.Status);
        Assert.Equal("Default", result.Value.Theme);
    }

    [Fact]
    public void LoadSettingsResult_DirectoryPath_ReportsAccessError()
    {
        var result = SettingsService.LoadSettingsResult(_tempDir);

        Assert.Equal(PersistenceLoadStatus.AccessError, result.Status);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void LoadSettingsResult_LockedFile_ReportsIoError()
    {
        var path = At("settings.json");
        File.WriteAllText(path, "{}");
        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = SettingsService.LoadSettingsResult(path);

        Assert.Equal(PersistenceLoadStatus.IoError, result.Status);
        Assert.NotNull(result.Error);
    }

    // ── SaveSettings / Round-trip ─────────────────────────────────────────────

    [Fact]
    public void SaveSettings_CreatesFileAndParentDirectory()
    {
        var path = Path.Combine(_tempDir, "sub", "settings.json");

        SettingsService.SaveSettings(path, new AppSettings());

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveAndLoadSettings_RoundTrip()
    {
        var path = At("settings.json");
        var original = new AppSettings
        {
            Theme = "Light", Language = "en-US"
        };

        SettingsService.SaveSettings(path, original);
        var loaded = SettingsService.LoadSettings(path);

        Assert.Equal("Light",  loaded.Theme);
        Assert.Equal("en-US",  loaded.Language);
    }

    [Fact]
    public void SaveSettings_ReplacingFile_PreservesPreviousVersionAsBackup()
    {
        var path = At("settings.json");
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Light" });

        SettingsService.SaveSettings(path, new AppSettings { Theme = "Dark" });

        Assert.Equal("Dark", SettingsService.LoadSettings(path).Theme);
        Assert.Equal("Light", SettingsService.LoadSettings(path + ".bak").Theme);
        Assert.Empty(Directory.GetFiles(_tempDir, "settings.json.*.tmp"));
    }

    [Fact]
    public void LoadSettingsBackupResult_ReturnsPreviousVersionExplicitly()
    {
        var path = At("settings.json");
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Light" });
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Dark" });

        var result = SettingsService.LoadSettingsBackupResult(path);

        Assert.True(result.IsSuccess);
        Assert.Equal("Light", result.Value.Theme);
    }

    [Fact]
    public void RestoreSettingsFromBackup_ArchivesCorruptPrimaryAndKeepsBackup()
    {
        var path = At("settings.json");
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Light" });
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Dark" });
        File.WriteAllText(path, "broken primary");

        var result = SettingsService.RestoreSettingsFromBackup(path);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ArchivedPrimaryPath);
        Assert.True(File.Exists(result.ArchivedPrimaryPath));
        Assert.Equal("broken primary", File.ReadAllText(result.ArchivedPrimaryPath));
        Assert.Equal("Light", SettingsService.LoadSettings(path).Theme);
        Assert.Equal("Light", SettingsService.LoadSettings(path + ".bak").Theme);
        Assert.Empty(Directory.GetFiles(_tempDir, "settings.json.*.tmp"));
    }

    [Fact]
    public void RestoreSettingsFromBackup_InvalidBackup_DoesNotTouchPrimary()
    {
        var path = At("settings.json");
        File.WriteAllText(path, "primary stays");
        File.WriteAllText(path + ".bak", "invalid backup");

        var result = SettingsService.RestoreSettingsFromBackup(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistenceLoadStatus.Corrupt, result.Status);
        Assert.Null(result.ArchivedPrimaryPath);
        Assert.Equal("primary stays", File.ReadAllText(path));
        Assert.Equal("invalid backup", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void RestoreSettingsFromBackup_WhenReplaceFails_KeepsPrimaryAndBackup()
    {
        var path = At("settings.json");
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Light" });
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Dark" });

        PersistenceRestoreResult result;
        using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            result = SettingsService.RestoreSettingsFromBackup(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(PersistenceLoadStatus.IoError, result.Status);
        Assert.Null(result.ArchivedPrimaryPath);
        Assert.Equal("Dark", SettingsService.LoadSettings(path).Theme);
        Assert.Equal("Light", SettingsService.LoadSettings(path + ".bak").Theme);
        Assert.Empty(Directory.GetFiles(_tempDir, "settings.json.*.tmp"));
    }

    [Fact]
    public void SaveSettings_WhenAtomicReplaceFails_KeepsExistingFile()
    {
        var path = At("settings.json");
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Light" });
        Directory.CreateDirectory(path + ".bak");

        Assert.ThrowsAny<Exception>(() =>
            SettingsService.SaveSettings(path, new AppSettings { Theme = "Dark" }));

        Assert.Equal("Light", SettingsService.LoadSettings(path).Theme);
        Assert.Empty(Directory.GetFiles(_tempDir, "settings.json.*.tmp"));
    }

    [Fact]
    public void SaveSettings_CorruptPrimary_BlocksOverwriteAndKeepsBackup()
    {
        var path = At("settings.json");
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Light" });
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Dark" });
        File.WriteAllText(path, "broken primary");

        var error = Assert.Throws<PersistenceSaveBlockedException>(() =>
            SettingsService.SaveSettings(path, new AppSettings { Theme = "Default" }));

        Assert.Equal(PersistenceLoadStatus.Corrupt, error.LoadStatus);
        Assert.Equal("broken primary", File.ReadAllText(path));
        Assert.Equal("Light", SettingsService.LoadSettings(path + ".bak").Theme);
    }

    // ── 後方互換 ──────────────────────────────────────────────────────────────

    [Fact]
    public void LoadSettings_OldVersionJson_NewPropertiesGetDefaults()
    {
        // v1.4 相当の settings.json（後から追加されたプロパティを含まない）
        File.WriteAllText(At("settings.json"),
            """{"OpenComposerInBrowser":true,"Theme":"Dark","Language":"ja-JP"}""");

        var s = SettingsService.LoadSettings(At("settings.json"));

        Assert.True(s.OpenComposerInBrowser);
        // 後から追加されたプロパティはクラス既定値で初期化される
        Assert.False(s.DefaultHideSidebar);
        Assert.True(s.DefaultHideCompose);
        Assert.False(s.DefaultHideListHeader);
        Assert.Equal("system", s.ExternalBrowser);
        Assert.Null(s.LastUsedProfileId);
        Assert.NotNull(s.SavedSearchQueries);
        Assert.Empty(s.SavedSearchQueries);
    }

    [Fact]
    public void LoadSettings_UnknownProperty_Ignored()
    {
        // 将来バージョンからのダウングレードを想定（未知プロパティが含まれる）
        File.WriteAllText(At("settings.json"),
            """{"Theme":"Light","FuturePropertyFromV99":{"nested":[1,2,3]}}""");

        var s = SettingsService.LoadSettings(At("settings.json"));

        Assert.Equal("Light", s.Theme);
    }

    [Fact]
    public void LoadSettings_NullLayoutWeights_AreRecoveredAsEmpty()
    {
        File.WriteAllText(At("settings.json"),
            """{"LayoutColumnWeights":null,"LayoutRowWeights":null}""");

        var s = SettingsService.LoadSettings(At("settings.json"));

        Assert.NotNull(s.LayoutColumnWeights);
        Assert.Empty(s.LayoutColumnWeights);
        Assert.NotNull(s.LayoutRowWeights);
        Assert.Empty(s.LayoutRowWeights);
    }

    [Fact]
    public void SaveAndLoadSettings_RoundTrip_AllProperties()
    {
        var path = At("settings.json");
        var original = new AppSettings
        {
            OpenComposerInBrowser  = true,
            OpenTimestampInBrowser = true,
            Theme                  = "Dark",
            Language               = "en-US",
            CachedLatestVersion    = "v9.9.9",
            DefaultHideSidebar     = true,
            DefaultHideCompose     = false,
            DefaultHideListHeader  = true,
            ExternalBrowser        = "edge",
            EdgeProfileDirectory   = "Profile 1",
            LastUsedProfileId      = "abc123",
            SavedSearchQueries     = ["/search?q=%E6%97%A5%E6%9C%AC&f=live", "/search?q=test"],
        };

        SettingsService.SaveSettings(path, original);
        var loaded = SettingsService.LoadSettings(path);

        Assert.True(loaded.OpenComposerInBrowser);
        Assert.True(loaded.OpenTimestampInBrowser);
        Assert.Equal("Dark",      loaded.Theme);
        Assert.Equal("en-US",     loaded.Language);
        Assert.Equal("v9.9.9",    loaded.CachedLatestVersion);
        Assert.True(loaded.DefaultHideSidebar);
        Assert.False(loaded.DefaultHideCompose);
        Assert.True(loaded.DefaultHideListHeader);
        Assert.Equal("edge",      loaded.ExternalBrowser);
        Assert.Equal("Profile 1", loaded.EdgeProfileDirectory);
        Assert.Equal("abc123",    loaded.LastUsedProfileId);
        Assert.Equal(["/search?q=%E6%97%A5%E6%9C%AC&f=live", "/search?q=test"],
                     loaded.SavedSearchQueries);
    }

    // ── LoadProfiles ──────────────────────────────────────────────────────────

    [Fact]
    public void LoadProfiles_MissingFile_ReturnsEmpty()
    {
        var profiles = SettingsService.LoadProfiles(At("profiles.json"));
        Assert.Empty(profiles);
    }

    [Fact]
    public void LoadProfiles_InvalidJson_ReturnsEmpty()
    {
        File.WriteAllText(At("profiles.json"), "not json");

        var profiles = SettingsService.LoadProfiles(At("profiles.json"));
        Assert.Empty(profiles);
    }

    [Fact]
    public void LoadProfilesResult_EmptyList_IsSuccess()
    {
        File.WriteAllText(At("profiles.json"), "[]");

        var result = SettingsService.LoadProfilesResult(At("profiles.json"));

        Assert.Equal(PersistenceLoadStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void LoadProfilesResult_InvalidJson_ReportsCorruptInsteadOfEmptySuccess()
    {
        File.WriteAllText(At("profiles.json"), "not json");

        var result = SettingsService.LoadProfilesResult(At("profiles.json"));

        Assert.Equal(PersistenceLoadStatus.Corrupt, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void LoadProfiles_ValidJson_ReturnsProfiles()
    {
        File.WriteAllText(At("profiles.json"),
            """[{"Id":"abc","Name":"Work"},{"Id":"def","Name":"Personal"}]""");

        var profiles = SettingsService.LoadProfiles(At("profiles.json"));

        Assert.Equal(2,          profiles.Count);
        Assert.Equal("Work",     profiles[0].Name);
        Assert.Equal("Personal", profiles[1].Name);
    }

    // ── SaveProfiles / Round-trip ─────────────────────────────────────────────

    [Fact]
    public void SaveAndLoadProfiles_RoundTrip()
    {
        var path = At("profiles.json");
        var original = new List<ProfileConfig>
        {
            new() { Id = "aaa", Name = "Work"     },
            new() { Id = "bbb", Name = "Personal" },
        };

        SettingsService.SaveProfiles(path, original);
        var loaded = SettingsService.LoadProfiles(path);

        Assert.Equal(2,          loaded.Count);
        Assert.Equal("aaa",      loaded[0].Id);
        Assert.Equal("Work",     loaded[0].Name);
        Assert.Equal("Personal", loaded[1].Name);
    }

    [Fact]
    public void SaveProfiles_ReplacingFile_PreservesPreviousVersionAsBackup()
    {
        var path = At("profiles.json");
        SettingsService.SaveProfiles(path, [new ProfileConfig { Id = "old", Name = "Old" }]);

        SettingsService.SaveProfiles(path, [new ProfileConfig { Id = "new", Name = "New" }]);

        Assert.Equal("new", Assert.Single(SettingsService.LoadProfiles(path)).Id);
        Assert.Equal("old", Assert.Single(SettingsService.LoadProfiles(path + ".bak")).Id);
        Assert.Empty(Directory.GetFiles(_tempDir, "profiles.json.*.tmp"));
    }

    [Fact]
    public void LoadProfilesBackupResult_ReturnsPreviousVersionExplicitly()
    {
        var path = At("profiles.json");
        SettingsService.SaveProfiles(path, [new ProfileConfig { Id = "old", Name = "Old" }]);
        SettingsService.SaveProfiles(path, [new ProfileConfig { Id = "new", Name = "New" }]);

        var result = SettingsService.LoadProfilesBackupResult(path);

        Assert.True(result.IsSuccess);
        Assert.Equal("old", Assert.Single(result.Value).Id);
    }

    [Fact]
    public void RestoreProfilesFromBackup_ArchivesPrimaryAndKeepsBackup()
    {
        var path = At("profiles.json");
        SettingsService.SaveProfiles(path, [new ProfileConfig { Id = "old", Name = "Old" }]);
        SettingsService.SaveProfiles(path, [new ProfileConfig { Id = "new", Name = "New" }]);
        File.WriteAllText(path, "broken primary");

        var result = SettingsService.RestoreProfilesFromBackup(path);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ArchivedPrimaryPath);
        Assert.Equal("broken primary", File.ReadAllText(result.ArchivedPrimaryPath));
        Assert.Equal("old", Assert.Single(SettingsService.LoadProfiles(path)).Id);
        Assert.Equal("old", Assert.Single(SettingsService.LoadProfiles(path + ".bak")).Id);
    }

    [Fact]
    public void ArchivePrimaryForRecovery_MissingFile_DoesNothing()
    {
        var result = JsonFilePersistence.ArchivePrimaryForRecovery(At("missing.json"));

        Assert.Null(result);
    }

    [Fact]
    public void ArchivePrimaryForRecovery_ThenInitialize_KeepsArchiveAndBackup()
    {
        var path = At("settings.json");
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Light" });
        SettingsService.SaveSettings(path, new AppSettings { Theme = "Dark" });
        File.WriteAllText(path, "broken primary");

        var archivePath = JsonFilePersistence.ArchivePrimaryForRecovery(path);
        SettingsService.SaveSettings(path, new AppSettings());

        Assert.NotNull(archivePath);
        Assert.Equal("broken primary", File.ReadAllText(archivePath));
        Assert.Equal("Default", SettingsService.LoadSettings(path).Theme);
        Assert.Equal("Light", SettingsService.LoadSettings(path + ".bak").Theme);
    }

    [Fact]
    public void ArchivePrimaryForRecovery_WhenMoveFails_ThrowsAndKeepsPrimary()
    {
        var path = At("settings.json");
        File.WriteAllText(path, "primary stays");

        using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() =>
                JsonFilePersistence.ArchivePrimaryForRecovery(path));
        }

        Assert.Equal("primary stays", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_tempDir, "settings.json.corrupt-*"));
    }

    // ── CleanupOrphanedProfileFolders ─────────────────────────────────────────

    [Fact]
    public void Cleanup_RemovesOrphanedFolders()
    {
        var dir = Path.Combine(_tempDir, "profiles");
        Directory.CreateDirectory(Path.Combine(dir, "known1"));
        Directory.CreateDirectory(Path.Combine(dir, "known2"));
        Directory.CreateDirectory(Path.Combine(dir, "orphan"));

        SettingsService.CleanupOrphanedProfileFolders(dir, ["known1", "known2"]);

        Assert.True (Directory.Exists(Path.Combine(dir, "known1")));
        Assert.True (Directory.Exists(Path.Combine(dir, "known2")));
        Assert.False(Directory.Exists(Path.Combine(dir, "orphan")));
    }

    [Fact]
    public void Cleanup_KeepsAllFolders_WhenAllAreKnown()
    {
        var dir = Path.Combine(_tempDir, "profiles");
        Directory.CreateDirectory(Path.Combine(dir, "id1"));
        Directory.CreateDirectory(Path.Combine(dir, "id2"));

        SettingsService.CleanupOrphanedProfileFolders(dir, ["id1", "id2"]);

        Assert.True(Directory.Exists(Path.Combine(dir, "id1")));
        Assert.True(Directory.Exists(Path.Combine(dir, "id2")));
    }

    [Fact]
    public void Cleanup_NonExistentDirectory_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            SettingsService.CleanupOrphanedProfileFolders(
                Path.Combine(_tempDir, "nonexistent"), []));
        Assert.Null(ex);
    }
}
