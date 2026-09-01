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
        Assert.Contains("AutomationProperties.AutomationId=\"PaneRefreshMenuItem\"", xaml);
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
    public void SecondaryWindows_UseDpiAwareSizingAndSettingsSizePersistence()
    {
        var settings = ReadRepoFile("Views/SettingsWindow.xaml.cs");
        var onboarding = ReadRepoFile("Views/OnboardingWindow.xaml.cs");
        var addProfile = ReadRepoFile("Views/AddProfileWindow.xaml.cs");
        var sizing = ReadRepoFile("Services/WindowSizingService.cs");
        Assert.Contains("WindowSizingService.ResizeAndCenter", settings);
        Assert.Contains("Settings.SettingsWindowWidth", settings);
        Assert.Contains("AppWindow.Changed", settings);
        Assert.Contains("SaveSettingsOnly?.Invoke()", settings);
        Assert.Contains("WindowSizingService.ResizeAndCenter", onboarding);
        Assert.Contains("WindowSizingService.ResizeAndCenter", addProfile);
        Assert.Contains("GetDpiForWindow", sizing);
        Assert.Contains("DisplayArea.GetFromWindowId", sizing);
    }

    [Fact]
    public void Layouts_PreventOverlapAndTreatFocusAsTemporary()
    {
        var timeline = ReadRepoFile("Views/MainWindow.Timeline.cs");
        var planner = ReadRepoFile("Services/LayoutPlanner.cs");
        Assert.Contains("LayoutPlanner.GetSafeMode", timeline);
        Assert.Contains("Grid2x2\" when visibleCount > 4", planner);
        Assert.Contains("Grid2x3\" when visibleCount > 6", planner);
        Assert.Contains("VerticalSplit\" when visibleCount > 2", planner);
        Assert.Contains("_layoutModeBeforeFocus", timeline);
        Assert.DoesNotContain("_appSettings.LayoutMode = \"Focus\"", timeline, StringComparison.Ordinal);
    }

    [Fact]
    public void TimelinePane_ExposesModeAwareMouseAndKeyboardResizeBoundaries()
    {
        var xaml = ReadRepoFile("Views/Controls/TimelinePane.xaml");
        var source = ReadRepoFile("Views/Controls/TimelinePane.xaml.cs");
        Assert.Contains("PaneWidthResizeGrip", xaml);
        Assert.Contains("PaneHeightResizeGrip", xaml);
        Assert.Contains("ConfigureResizeAffordances", source);
        Assert.Contains("ResizeGrip.KeyDown", source);
        Assert.Contains("VerticalResizeGrip.KeyDown", source);
    }
}
