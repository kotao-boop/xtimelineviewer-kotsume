using System;
using System.IO;
using XTimelineViewer.Models;
using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public class ExtensionSettingsServiceTests
{
    [Fact]
    public void GetKey_DistinguishesBundledAndUserExtensions()
    {
        var path = Path.Combine("root", "xtv-translator");

        Assert.Equal("bundled:xtv-translator", ExtensionSettingsService.GetKey(path, false));
        Assert.Equal("user:xtv-translator", ExtensionSettingsService.GetKey(path, true));
    }

    [Fact]
    public void NewExtension_IsEnabledByDefault_AndTogglePersists()
    {
        var settings = new AppSettings();
        var path = Path.Combine("root", "my-extension");

        Assert.True(ExtensionSettingsService.IsEnabled(settings, path, true));

        ExtensionSettingsService.SetEnabled(settings, path, true, false);
        Assert.False(ExtensionSettingsService.IsEnabled(settings, path, true));

        ExtensionSettingsService.SetEnabled(settings, path, true, true);
        Assert.True(ExtensionSettingsService.IsEnabled(settings, path, true));
        Assert.Empty(settings.DisabledExtensionKeys);
    }

    [Fact]
    public void SetEnabled_RemovesDuplicateKeysWithoutChangingOtherExtensions()
    {
        var settings = new AppSettings
        {
            DisabledExtensionKeys = ["user:my-extension", "USER:MY-EXTENSION", "bundled:other"]
        };

        ExtensionSettingsService.SetEnabled(settings, Path.Combine("root", "my-extension"), true, true);

        Assert.Equal(["bundled:other"], settings.DisabledExtensionKeys);
    }
}
