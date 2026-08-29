using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XTimelineViewer.Models;
using XTimelineViewer.Services;
using XTimelineViewer.Views.Controls;

namespace XTimelineViewer.Views
{
    public sealed partial class MainWindow : Window
    {
        private List<WorkspaceConfig> _workspaces = [];
        private bool _refreshingToolbarProfiles;

        private string? SelectedToolbarProfileId
            => (ToolbarProfileCombo.SelectedItem as ComboBoxItem)?.Tag as string;

        private void LoadWorkspaces() => _workspaces = WorkspaceStore.Load(WorkspacesFilePath);

        private void SaveWorkspaces() => WorkspaceStore.Save(WorkspacesFilePath, _workspaces);

        private void RefreshToolbarProfiles()
        {
            if (ToolbarProfileCombo is null) return;
            var preferred = SelectedToolbarProfileId
                ?? _appSettings.LastUsedProfileId
                ?? _profiles.FirstOrDefault(p => p.IsPrimary)?.Id;

            _refreshingToolbarProfiles = true;
            ToolbarProfileCombo.Items.Clear();
            foreach (var profile in _profiles.Where(p => p.Id != "default"))
            {
                var label = string.IsNullOrWhiteSpace(profile.ScreenName)
                    ? profile.Name
                    : $"{profile.Name}  @{profile.ScreenName}";
                ToolbarProfileCombo.Items.Add(new ComboBoxItem { Content = label, Tag = profile.Id });
            }

            ToolbarProfileCombo.SelectedItem = ToolbarProfileCombo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Tag as string, preferred, StringComparison.Ordinal))
                ?? ToolbarProfileCombo.Items.OfType<ComboBoxItem>().FirstOrDefault();
            ToolbarProfileCombo.IsEnabled = ToolbarProfileCombo.Items.Count > 0;
            ToolbarProfileCombo.Visibility = ToolbarProfileCombo.Items.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            _refreshingToolbarProfiles = false;
        }

        private void ToolbarProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshingToolbarProfiles || SelectedToolbarProfileId is not { } profileId) return;
            _appSettings.LastUsedProfileId = profileId;
            SaveSettings();
        }

        private void OpenTimelineManager_Click(object sender, RoutedEventArgs e)
            => ShowTimelineManagerAsync().FireAndForget(nameof(ShowTimelineManagerAsync));

        private async Task ShowTimelineManagerAsync()
        {
            var rows = new StackPanel { Spacing = 8 };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock
            {
                Text = R.Get("Timeline_ManagerDescription"),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75,
            });
            content.Children.Add(rows);

            var dialog = new ContentDialog
            {
                Title = R.Get("Timeline_ManagerTitle"),
                Content = new ScrollViewer
                {
                    Content = content,
                    MaxHeight = 560,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
                CloseButtonText = R.Get("Button_Close"),
                XamlRoot = Content.XamlRoot,
            };

            void RebuildRows()
            {
                rows.Children.Clear();
                if (_configs.Count == 0)
                {
                    rows.Children.Add(new TextBlock { Text = R.Get("DropHintSubtitle.Text"), Opacity = 0.65 });
                    return;
                }

                for (var index = 0; index < _configs.Count; index++)
                {
                    var config = _configs[index];
                    var pane = Panes.FirstOrDefault(p => ReferenceEquals(p.Config, config));
                    var row = new Grid
                    {
                        Padding = new Thickness(12),
                        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        CornerRadius = new CornerRadius(8),
                        ColumnSpacing = 8,
                    };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var info = new StackPanel { Spacing = 4 };
                    var nameBox = new TextBox
                    {
                        Text = TimelineLabelHelper.GetFriendlyName(config, R.Get),
                        Header = R.Get("Timeline_Name"),
                        MaxLength = 80,
                    };
                    nameBox.LostFocus += (_, _) =>
                    {
                        var automatic = TimelineLabelHelper.GetFriendlyName(new TimelineConfig
                        {
                            Url = config.Url,
                            IsListsIndex = config.IsListsIndex,
                        }, R.Get);
                        var entered = nameBox.Text.Trim();
                        config.Name = entered == automatic ? string.Empty : entered;
                        pane?.UpdateUrlHeader();
                        SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));
                    };
                    info.Children.Add(nameBox);
                    info.Children.Add(new TextBlock
                    {
                        Text = SearchQueryHelper.DecodeSearchPath(config.Url),
                        FontSize = 11,
                        Opacity = 0.55,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });

                    var actions = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 4,
                        VerticalAlignment = VerticalAlignment.Bottom,
                    };
                    Grid.SetColumn(actions, 1);
                    var visible = new ToggleButton
                    {
                        IsChecked = config.IsVisible,
                        Content = R.Get(config.IsVisible ? "Timeline_Visible" : "Timeline_Hidden"),
                    };
                    visible.Click += (_, _) =>
                    {
                        config.IsVisible = visible.IsChecked == true;
                        if (config.IsVisible) _temporarilyHiddenTimelines.Remove(config);
                        visible.Content = R.Get(config.IsVisible ? "Timeline_Visible" : "Timeline_Hidden");
                        if (pane is not null) pane.Visibility = config.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                        ApplyLayoutMode();
                        UpdateLayoutSuggestion();
                        SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));
                    };
                    actions.Children.Add(visible);

                    var up = new Button { Content = "↑", IsEnabled = index > 0 };
                    ToolTipService.SetToolTip(up, R.Get("Timeline_MoveUp"));
                    var capturedIndex = index;
                    up.Click += (_, _) =>
                    {
                        MoveConfig(capturedIndex, capturedIndex - 1);
                        RebuildRows();
                    };
                    actions.Children.Add(up);

                    var down = new Button { Content = "↓", IsEnabled = index < _configs.Count - 1 };
                    ToolTipService.SetToolTip(down, R.Get("Timeline_MoveDown"));
                    down.Click += (_, _) =>
                    {
                        MoveConfig(capturedIndex, capturedIndex + 1);
                        RebuildRows();
                    };
                    actions.Children.Add(down);

                    var duplicate = new Button { Content = R.Get("Timeline_Duplicate") };
                    duplicate.Click += (_, _) =>
                    {
                        var copy = config.Clone();
                        var originalName = TimelineLabelHelper.GetFriendlyName(config, R.Get);
                        copy.Name = string.Format(R.Get("Timeline_CopySuffix"), originalName);
                        AddTimeline(copy);
                        RebuildRows();
                    };
                    actions.Children.Add(duplicate);

                    var delete = new Button
                    {
                        Content = R.Get("Timeline_DeleteConfirm"),
                        Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                    };
                    var flyout = new Flyout
                    {
                        Content = BuildInlineDeleteConfirmation(config, pane, dialog, RebuildRows),
                    };
                    delete.Click += (_, _) => flyout.ShowAt(delete);
                    actions.Children.Add(delete);

                    row.Children.Add(info);
                    row.Children.Add(actions);
                    rows.Children.Add(row);
                }
            }

            RebuildRows();
            await ShowDialogAsync(dialog);
        }

        private UIElement BuildInlineDeleteConfirmation(
            TimelineConfig config,
            TimelinePane? pane,
            ContentDialog owner,
            Action afterDelete)
        {
            var panel = new StackPanel { Spacing = 10, MaxWidth = 320 };
            panel.Children.Add(new TextBlock
            {
                Text = string.Format(R.Get("Timeline_DeleteConfirmBody"), TimelineLabelHelper.GetFriendlyName(config, R.Get)),
                TextWrapping = TextWrapping.Wrap,
            });
            var confirm = new Button
            {
                Content = R.Get("Timeline_DeleteConfirm"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            };
            confirm.Click += async (_, _) =>
            {
                if (pane is not null) await RemoveTimelineAsync(pane);
                else _configs.Remove(config);
                afterDelete();
            };
            panel.Children.Add(confirm);
            return panel;
        }

        private void MoveConfig(int from, int to)
        {
            if (from < 0 || to < 0 || from >= _configs.Count || to >= _configs.Count || from == to) return;
            var config = _configs[from];
            _configs.RemoveAt(from);
            _configs.Insert(to, config);
            RebuildPaneOrderFromConfigs();
            SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));
        }

        private void RebuildPaneOrderFromConfigs()
        {
            var byConfig = Panes.ToDictionary(p => p.Config);
            TimelinePanel.Children.Clear();
            TimelineGrid.Children.Clear();
            foreach (var config in _configs)
                if (byConfig.TryGetValue(config, out var pane))
                    TimelinePanel.Children.Add(pane);
            ApplyLayoutMode();
        }

        private void OpenWorkspaces_Click(object sender, RoutedEventArgs e)
            => ShowWorkspacesAsync().FireAndForget(nameof(ShowWorkspacesAsync));

        private async Task ShowWorkspacesAsync()
        {
            var root = new StackPanel { Spacing = 12 };
            root.Children.Add(new TextBlock
            {
                Text = R.Get("Workspace_Description"),
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap,
            });
            var nameBox = new TextBox { Header = R.Get("Workspace_Name"), MaxLength = 60 };
            var save = new Button { Content = R.Get("Workspace_SaveCurrent"), HorizontalAlignment = HorizontalAlignment.Left };
            var list = new StackPanel { Spacing = 8 };
            root.Children.Add(nameBox);
            root.Children.Add(save);
            root.Children.Add(new NavigationViewItemSeparator());
            root.Children.Add(list);

            var dialog = new ContentDialog
            {
                Title = R.Get("Workspace_Title"),
                Content = new ScrollViewer { Content = root, MaxHeight = 560 },
                CloseButtonText = R.Get("Button_Close"),
                XamlRoot = Content.XamlRoot,
            };

            void Rebuild()
            {
                list.Children.Clear();
                if (_workspaces.Count == 0)
                {
                    list.Children.Add(new TextBlock { Text = R.Get("Workspace_Empty"), Opacity = 0.65 });
                    return;
                }
                foreach (var workspace in _workspaces.OrderBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    var row = new Grid { Padding = new Thickness(8), ColumnSpacing = 8 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.Children.Add(new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = workspace.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                            new TextBlock { Text = string.Format(R.Get("Workspace_Summary"), workspace.Timelines.Count, GetLayoutDisplayName(workspace.LayoutMode)), FontSize = 11, Opacity = 0.6 },
                        },
                    });
                    var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    Grid.SetColumn(buttons, 1);
                    var open = new Button { Content = R.Get("Workspace_Apply") };
                    open.Click += async (_, _) =>
                    {
                        dialog.Hide();
                        await ApplyWorkspaceAsync(workspace);
                    };
                    var delete = new Button { Content = R.Get("Workspace_Delete") };
                    delete.Click += (_, _) =>
                    {
                        _workspaces.Remove(workspace);
                        if (_appSettings.ActiveWorkspaceId == workspace.Id) _appSettings.ActiveWorkspaceId = null;
                        SaveWorkspaces();
                        SaveSettings();
                        Rebuild();
                    };
                    buttons.Children.Add(open);
                    buttons.Children.Add(delete);
                    row.Children.Add(buttons);
                    list.Children.Add(row);
                }
            }

            save.Click += (_, _) =>
            {
                var name = nameBox.Text.Trim();
                if (name.Length == 0) return;
                var workspace = _workspaces.FirstOrDefault(w => w.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
                if (workspace is null)
                {
                    workspace = new WorkspaceConfig { Name = name };
                    _workspaces.Add(workspace);
                }
                workspace.Name = name;
                workspace.LayoutMode = _appSettings.LayoutMode;
                workspace.Timelines = _configs.Select(c => c.Clone()).ToList();
                _appSettings.ActiveWorkspaceId = workspace.Id;
                SaveWorkspaces();
                SaveSettings();
                nameBox.Text = string.Empty;
                Rebuild();
            };

            Rebuild();
            await ShowDialogAsync(dialog);
        }

        private async Task ApplyWorkspaceAsync(WorkspaceConfig workspace)
        {
            foreach (var pane in Panes.ToList()) CleanupWebView(pane.WebView);
            TimelinePanel.Children.Clear();
            TimelineGrid.Children.Clear();
            _configs.Clear();

            _appSettings.LayoutMode = workspace.LayoutMode;
            _appSettings.ActiveWorkspaceId = workspace.Id;
            foreach (var config in workspace.Timelines.Select(t => t.Clone())) AddTimeline(config);
            ViewModel.HasTimelines = _configs.Count > 0;
            if (_configs.Count > 0) ApplyLayoutMode();
            await SaveTimelinesAsync();
            SaveSettings();
        }

        private void OpenCommandPalette_Click(object sender, RoutedEventArgs e)
            => ShowCommandPaletteAsync().FireAndForget(nameof(ShowCommandPaletteAsync));

        private void OpenCommandPalette_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ShowCommandPaletteAsync().FireAndForget(nameof(ShowCommandPaletteAsync));
        }

        private sealed record CommandEntry(string Title, string Keywords, Action Execute);

        private async Task ShowCommandPaletteAsync()
        {
            ContentDialog? dialog = null;
            var search = new TextBox { PlaceholderText = R.Get("Command_Placeholder") };
            var results = new StackPanel { Spacing = 4 };
            var root = new StackPanel { Spacing = 10, MinWidth = 480, Children = { search, results } };
            var commands = new List<CommandEntry>
            {
                new(R.Get("Toolbar_AddTimelineTooltip") + " - " + R.Get("Timeline_Home"), "timeline home add", () => AddTimeline(CreateDefaultConfig(HomeTimelineUrl))),
                new(R.Get("Menu_TimelineManager"), "timeline manage order hide duplicate", () => ShowTimelineManagerAsync().FireAndForget(nameof(ShowTimelineManagerAsync))),
                new(R.Get("Menu_Workspaces"), "workspace save switch", () => ShowWorkspacesAsync().FireAndForget(nameof(ShowWorkspacesAsync))),
                new(R.Get("Menu_Settings"), "settings preferences", () => OpenSettingsWindow()),
                new(R.Get("Menu_NewProfile"), "profile account login", () => NewProfileMenuItem_Click(this, new RoutedEventArgs())),
                new(R.Get("Layout_Auto"), "layout auto arrange reflow 自動 整列", () => SetLayoutFromCommand("Auto")),
                new(R.Get("Layout_Classic"), "layout classic", () => SetLayoutFromCommand("Classic")),
                new(R.Get("Layout_Grid2x2"), "layout grid 2x2", () => SetLayoutFromCommand("Grid2x2")),
                new(R.Get("Layout_Grid2x3"), "layout grid 2x3", () => SetLayoutFromCommand("Grid2x3")),
                new(R.Get("Layout_VerticalSplit"), "layout vertical split", () => SetLayoutFromCommand("VerticalSplit")),
                new(R.Get("Layout_Focus"), "layout focus", () => SetLayoutFromCommand("Focus")),
                new(R.Get("Menu_Shortcuts"), "keyboard shortcuts help", () => ShowShortcutsAsync().FireAndForget(nameof(ShowShortcutsAsync))),
            };

            void Populate(string query)
            {
                results.Children.Clear();
                foreach (var command in commands.Where(c =>
                    query.Length == 0
                    || c.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || c.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    var button = new Button
                    {
                        Content = command.Title,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                    };
                    button.Click += (_, _) =>
                    {
                        dialog?.Hide();
                        DispatcherQueue.TryEnqueue(() => command.Execute());
                    };
                    results.Children.Add(button);
                }
            }
            search.TextChanged += (_, _) => Populate(search.Text.Trim());
            Populate(string.Empty);
            dialog = new ContentDialog
            {
                Title = R.Get("Command_Title"),
                Content = root,
                CloseButtonText = R.Get("Button_Close"),
                XamlRoot = Content.XamlRoot,
            };
            await ShowDialogAsync(dialog);
        }

        private void SetLayoutFromCommand(string mode)
        {
            _appSettings.LayoutMode = mode;
            SaveSettings();
            ApplyLayoutMode(mode);
            UpdateLayoutSuggestion();
        }

        private void UpdateLayoutSuggestion()
        {
            if (_appSettings.LayoutMode != "Classic")
            {
                LayoutSuggestionBar.IsOpen = false;
                return;
            }
            var visibleCount = _configs.Count(c => c.IsVisible);
            if (visibleCount is not (4 or 6))
            {
                LayoutSuggestionBar.IsOpen = false;
                return;
            }
            LayoutSuggestionBar.Message = R.Get(visibleCount == 4 ? "Layout_Suggestion4" : "Layout_Suggestion6");
            LayoutSuggestionApplyBtn.Content = R.Get("Layout_UseSuggestion");
            LayoutSuggestionBar.IsOpen = true;
        }

        private void LayoutSuggestionApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            SetLayoutFromCommand(_configs.Count(c => c.IsVisible) >= 6 ? "Grid2x3" : "Grid2x2");
            LayoutSuggestionBar.IsOpen = false;
        }

        private static string GetLayoutDisplayName(string mode) => mode switch
        {
            "Auto" => R.Get("Layout_Auto"),
            "Grid2x2" => R.Get("Layout_Grid2x2"),
            "Grid2x3" => R.Get("Layout_Grid2x3"),
            "VerticalSplit" => R.Get("Layout_VerticalSplit"),
            "Focus" => R.Get("Layout_Focus"),
            _ => R.Get("Layout_Classic"),
        };

        private void OpenShortcuts_Click(object sender, RoutedEventArgs e)
            => ShowShortcutsAsync().FireAndForget(nameof(ShowShortcutsAsync));

        private void OpenShortcuts_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ShowShortcutsAsync().FireAndForget(nameof(ShowShortcutsAsync));
        }

        private async Task ShowShortcutsAsync()
        {
            var shortcuts = new (string Key, string Action)[]
            {
                ("Ctrl + 1～9", R.Get("Shortcut_FocusTimeline")),
                ("Ctrl + ← / →", R.Get("Shortcut_MoveBetween")),
                ("Ctrl + Shift + ← / →", R.Get("Shortcut_Reorder")),
                ("Ctrl + F / F3", R.Get("Search_Tooltip")),
                ("Ctrl + N", R.Get("PostBtn_Tooltip")),
                ("Ctrl + K", R.Get("Menu_CommandPalette")),
                ("F1", R.Get("Menu_Shortcuts")),
                ("F5", R.Get("Shortcut_Reload")),
                ("Home / End", R.Get("Shortcut_TopBottom")),
            };
            var grid = new Grid { ColumnSpacing = 24, RowSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < shortcuts.Length; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var key = new TextBlock { Text = shortcuts[i].Key, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                var action = new TextBlock { Text = shortcuts[i].Action, TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(key, i);
                Grid.SetRow(action, i);
                Grid.SetColumn(action, 1);
                grid.Children.Add(key);
                grid.Children.Add(action);
            }
            await ShowDialogAsync(new ContentDialog
            {
                Title = R.Get("Shortcuts_Title"),
                Content = grid,
                CloseButtonText = R.Get("Button_Close"),
                XamlRoot = Content.XamlRoot,
            });
        }
    }
}
