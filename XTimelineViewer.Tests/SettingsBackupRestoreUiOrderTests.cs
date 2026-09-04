using System;
using System.IO;
using XTimelineViewer.Models;
using Xunit;

namespace XTimelineViewer.Tests;

public sealed class SettingsBackupRestoreUiOrderTests
{
    [Fact]
    public void NormalClose_KeepsSettingsPersistenceEnabled()
    {
        var state = new SettingsWindowCloseState();

        Assert.True(state.ShouldSaveSettingsOnClose);
    }

    [Fact]
    public void BackupRestore_DisablesOldSettingsPersistence()
    {
        var state = new SettingsWindowCloseState();

        state.SuppressSettingsSaveAfterRestore();

        Assert.False(state.ShouldSaveSettingsOnClose);
    }

    [Fact]
    public void RestorePage_SuppressesOldSaveBeforeReloadCallback()
    {
        var source = ReadRepoFile("Views/Settings/UserDataPage.xaml.cs");
        var suppressIndex = source.IndexOf(
            "_parent.SuppressSettingsSaveAfterRestore();",
            StringComparison.Ordinal);
        var callbackIndex = source.IndexOf(
            "await _parent.BackupRestored();",
            StringComparison.Ordinal);

        Assert.True(suppressIndex >= 0, "復元後の設定保存を止める呼び出しがありません。");
        Assert.True(callbackIndex > suppressIndex, "保存停止は再読込コールバックより先に必要です。");
    }

    [Fact]
    public void SettingsWindow_SavesSizeOnlyForNormalClose()
    {
        var source = ReadRepoFile("Views/SettingsWindow.xaml.cs");

        Assert.Contains("if (_closeState.ShouldSaveSettingsOnClose)", source, StringComparison.Ordinal);
        Assert.Contains("Settings.SettingsWindowWidth", source, StringComparison.Ordinal);
        Assert.Contains("Settings.SettingsWindowHeight", source, StringComparison.Ordinal);
        Assert.Contains("SaveSettingsOnly?.Invoke();", source, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
