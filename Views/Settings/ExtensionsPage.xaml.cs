using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using XTimelineViewer.Models;

namespace XTimelineViewer.Views.Settings
{
    public sealed partial class ExtensionsPage : Page
    {
        private SettingsWindow? _parent;

        public ExtensionsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _parent = e.Parameter as SettingsWindow;
            PopulateUI();
        }

        internal void Refresh() => PopulateUI();

        private void PopulateUI()
        {
            PageTitle.Text = R.Get("Nav_Extensions");

            while (RootPanel.Children.Count > 2)
                RootPanel.Children.RemoveAt(RootPanel.Children.Count - 1);

            var extensions = _parent?.Extensions ?? [];

            // GitHub版とStore版で同じ拡張機能追加手順を使う。Store版でも
            // 利用者追加分はパッケージ外の user フォルダーへ保存する。
            ExtensionsInfoBar.Message = extensions.Count == 0
                ? R.Get("Extensions_InfoBar_Empty")
                : R.Get("Extensions_InfoBar_Installed");
            ExtensionsInfoBar.Severity = InfoBarSeverity.Informational;
            OpenExtensionsFolderBtn.Content = R.Get("Extensions_OpenFolder");
            OpenExtensionsFolderBtn.Visibility = Visibility.Visible;

            foreach (var ext in extensions)
            {
                AddExtensionCard(ext);
            }
        }

        // extensions フォルダーを開く（#241）。無ければ作成してから開く。
        private async void OpenExtensionsFolder_Click(object sender, RoutedEventArgs e)
        {
            var dir = MainWindow.GetExtensionsDir();
            try { Directory.CreateDirectory(dir); } catch { /* 作成失敗は無視して開くを試みる */ }
            await Windows.System.Launcher.LaunchFolderPathAsync(dir);
        }

        private void AddExtensionCard(ExtensionInfo ext)
        {
            var enabled = _parent?.IsExtensionEnabled?.Invoke(ext) ?? ext.IsEnabled;
            var stateKey = ext.IsSuppressed
                ? "Extensions_Duplicate"
                : !enabled
                ? "Extensions_Disabled"
                : ext.LoadError is null ? "Extensions_Loaded" : "Extensions_LoadFailed";
            var card = new CommunityToolkit.WinUI.Controls.SettingsCard
            {
                Header      = ext.Name,
                Description = $"{(ext.IsUserAdded ? R.Get("Extensions_UserAdded") : R.Get("Extensions_Bundled"))}\n{R.Get(stateKey)}",
                // 右端のリンクアイコンと「設定を開く」ボタンの機能が重複していたため、
                // 明示的なボタンを残してカード自体のクリック化は廃止
            };

            if (ext.IconPath is not null)
            {
                card.HeaderIcon = new ImageIcon
                {
                    Source = new BitmapImage(new Uri(ext.IconPath)),
                    Width  = 24,
                    Height = 24,
                };
            }
            else
            {
                card.HeaderIcon = new FontIcon
                {
                    Glyph      = "",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons")
                };
            }

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing     = 8,
            };

            var enabledToggle = new ToggleSwitch
            {
                IsOn = enabled,
                OnContent = R.Get("Extensions_On"),
                OffContent = R.Get("Extensions_Off"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(
                enabledToggle,
                string.Format(R.Get("Extensions_ToggleName"), ext.Name));
            ToolTipService.SetToolTip(
                enabledToggle,
                R.Get(ext.IsSuppressed
                    ? "Extensions_DuplicateDescription"
                    : "Extensions_ToggleDescription"));
            enabledToggle.Toggled += async (_, _) =>
            {
                var setter = _parent?.SetExtensionEnabledAsync;
                if (setter is null) return;

                enabledToggle.IsEnabled = false;
                try
                {
                    await setter(ext, enabledToggle.IsOn);
                    _parent?.RefreshExtensionsPage();
                }
                catch
                {
                    // 保存済みの値を読み直して、画面と実際の状態を一致させる。
                    _parent?.RefreshExtensionsPage();
                    ExtensionsInfoBar.Severity = InfoBarSeverity.Warning;
                    ExtensionsInfoBar.Message = R.Get("Extensions_ToggleFailed");
                }
                finally
                {
                    enabledToggle.IsEnabled = true;
                }
            };
            buttonsPanel.Children.Add(enabledToggle);

            if (ext.LoadError is not null)
            {
                var details = new Expander
                {
                    Header = R.Get("Extensions_Details"),
                    Content = new TextBlock { Text = ext.LoadError, TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true, MaxWidth = 480 },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                var copy = new Button { Content = R.Get("Extensions_CopyDiagnostics") };
                copy.Click += (_, _) =>
                {
                    var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    data.SetText($"{ext.Name}\n{ext.DirectoryPath}\n{ext.LoadError}");
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
                    copy.Content = R.Get("Extensions_Copied");
                };
                card.Description += "\n" + R.Get("Extensions_Recovery");
                buttonsPanel.Children.Add(copy);
                card.Content = buttonsPanel;
                RootPanel.Children.Add(card);
                RootPanel.Children.Add(details);
                return;
            }

            if (enabled && !ext.IsSuppressed && ext.OptionsPage is not null && ext.ExtensionId is not null)
            {
                var settingsBtn = new Button
                {
                    Content = R.Get("ExtSettings_OpenSettings"),
                };
                settingsBtn.Click += async (_, _) =>
                {
                    if (_parent?.OpenExtensionSettingsAsync is not null && XamlRoot is not null)
                        await _parent.OpenExtensionSettingsAsync(ext, XamlRoot);
                };
                buttonsPanel.Children.Add(settingsBtn);
            }

            // 拡張機能カードのリンクボタン（Chrome Web Store 等）は場所をとるため削除

            if (buttonsPanel.Children.Count > 0)
                card.Content = buttonsPanel;

            RootPanel.Children.Add(card);
        }
    }
}
