using System;
using System.IO;
using Xunit;

namespace XTimelineViewer.Tests;

public class ProductivityFeatureStructureTests
{
    private static string ReadRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void MainWindow_ExposesWorkspaceTabsAndUnifiedColumnCreator()
    {
        var xaml = ReadRepoFile("Views/MainWindow.xaml");
        Assert.Contains("x:Name=\"WorkspaceTabsPanel\"", xaml);
        Assert.Contains("Click=\"AddTimelineToolbarBtn_Click\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"WorkspaceAddBtn\"", xaml);
    }

    [Fact]
    public void TimelinePane_ExposesUnreadRefreshAndOverflowControls()
    {
        var xaml = ReadRepoFile("Views/Controls/TimelinePane.xaml");
        Assert.Contains("AutomationProperties.AutomationId=\"PaneNewItemsBtn\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"PaneRefreshBtn\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"PaneActionsBtn\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"PaneTranslationMenuItem\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"PaneFocusMenuItem\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"PaneTemporaryHideMenuItem\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"PaneSettingsMenuItem\"", xaml);
    }

    [Fact]
    public void UnreadCounter_SendsOnlyAnIntegerNotPostText()
    {
        var source = ReadRepoFile("Views/MainWindow.Unread.cs");
        Assert.Contains("postMessage('unread:' + unread)", source);
        Assert.DoesNotContain("innerText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("textContent", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Translator_UsesNativeHeaderCommandInsteadOfPageOverlay()
    {
        var source = ReadRepoFile("extensions/xtv-translator/content.js");
        var css = ReadRepoFile("extensions/xtv-translator/content.css");
        Assert.Contains("xtv-translator-command", source);
        Assert.Contains("data-xtv-translation-state", source);
        Assert.DoesNotContain("injectHeaderToggle", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".xtv-toggle-container", css, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindow_UsesRoomyScreenAwareInitialSize()
    {
        var source = ReadRepoFile("Views/SettingsWindow.xaml.cs");
        Assert.Contains("const int preferredWidth = 1100", source);
        Assert.Contains("const int preferredHeight = 760", source);
        Assert.Contains("DisplayArea.GetFromWindowId", source);
        Assert.Contains("AppWindow.Move", source);
        Assert.DoesNotContain("AppWindow.Resize(new SizeInt32(900, 620))", source, StringComparison.Ordinal);
    }
}
