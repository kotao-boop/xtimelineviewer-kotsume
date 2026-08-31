using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI;
using XTimelineViewer.Models;

namespace XTimelineViewer.Views
{
    public sealed partial class SettingsWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        private readonly IntPtr _ownerHwnd;

        /// <summary>親ウィンドウから渡されたアプリ設定。ページが直接読み書きする。</summary>
        internal AppSettings Settings { get; }

        /// <summary>設定ページが x:Bind する ViewModel (#199)。</summary>
        internal ViewModels.SettingsViewModel ViewModel { get; }

        /// <summary>設定ファイルが格納されているフォルダーパス。</summary>
        internal string SettingsFolder { get; }

        /// <summary>設定が変更されたときに発火する。MainWindow が購読して保存・適用する。</summary>
        internal event Action? SettingsChanged;

        internal void NotifySettingsChanged() => SettingsChanged?.Invoke();

        /// <summary>ロード済み拡張機能の一覧。MainWindow が設定する。</summary>
        internal List<ExtensionInfo> Extensions { get; set; } = [];

        /// <summary>拡張機能の設定ダイアログを開くコールバック。MainWindow が提供する。</summary>
        internal Func<ExtensionInfo, Microsoft.UI.Xaml.XamlRoot, Task>? OpenExtensionSettingsAsync { get; set; }

        /// <summary>外部ブラウザー設定に従って URI を開くコールバック。MainWindow が提供する。</summary>
        internal Func<Uri, Task>? LaunchUriAsync { get; set; }

        /// <summary>プロファイル一覧。MainWindow が設定する。</summary>
        internal List<ProfileConfig> Profiles { get; set; } = [];

        /// <summary>バッジ色パレット。MainWindow が設定する。</summary>
        internal Color[] BadgeColors { get; set; } = [];

        /// <summary>プロファイル変更後の保存コールバック。</summary>
        internal Action? ProfilesModified { get; set; }

        /// <summary>同じプロファイルでサインインし直した後、該当タイムラインを再読み込みする。</summary>
        internal Action<string>? ProfileSessionRefreshed { get; set; }

        /// <summary>プロファイル削除コールバック。</summary>
        internal Func<string, Task>? DeleteProfileAsync { get; set; }

        /// <summary>プロファイル作成コールバック。</summary>
        internal Action<ProfileConfig>? OnProfileCreated { get; set; }

        /// <summary>指定プロファイルが使用しているタイムライン数を返す。</summary>
        internal Func<string, int>? GetTimelineCount { get; set; }

        /// <summary>WebView2 ランタイムバージョン文字列。MainWindow が設定する。</summary>
        internal string EdgeVersion { get; set; } = "";

        /// <summary>winget が利用可能かどうか（unpackaged のみ意味がある）。</summary>
        internal bool HasWinget { get; set; }

        /// <summary>最新バージョンを取得するコールバック（winget 版は winget、それ以外は GitHub Releases）。</summary>
        internal Func<Task<Version?>>? FetchLatestVersionAsync { get; set; }

        /// <summary>設定のみ保存する（テーマ適用等はしない）コールバック。</summary>
        internal Action? SaveSettingsOnly { get; set; }

        /// <summary>メニューの更新バッジを更新するコールバック。</summary>
        internal Action? UpdateMenuBadge { get; set; }

        /// <summary>設定バックアップの復元後、MainWindow の表示状態を読み直す。</summary>
        internal Func<Task>? BackupRestored { get; set; }

        /// <summary>この設定ウィンドウにファイル選択画面を関連付けるためのハンドル。</summary>
        internal IntPtr WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

        /// <summary>アプリを終了して winget でアップデートを開始するコールバック。</summary>
        internal Action? ExitAndRunWingetUpdate { get; set; }

        public SettingsWindow(IntPtr ownerHwnd, AppSettings settings, string settingsFolder)
        {
            _ownerHwnd = ownerHwnd;
            Settings = settings;
            SettingsFolder = settingsFolder;
            ViewModel = new ViewModels.SettingsViewModel(settings, NotifySettingsChanged);

            // テーマ変更は設定ウィンドウ自身にも即時反映する
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ViewModels.SettingsViewModel.ThemeIndex)) return;
                ApplyTheme(Settings.Theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark"  => ElementTheme.Dark,
                    _       => ElementTheme.Default,
                });
            };

            this.InitializeComponent();

            // 設定項目が増えても窮屈にならない大きさで開く。
            // 小さい画面では作業領域からはみ出さないよう余白を残して縮小する。
            ResizeAndCenterWindow();
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);

            // ナビゲーション項目のテキストを設定
            RefreshNavText();

            // モーダル化: 親ウィンドウを無効化
            EnableWindow(_ownerHwnd, false);
            Closed += (_, _) => EnableWindow(_ownerHwnd, true);

            // 初期ページを選択
            NavView.SelectedItem = NavGeneral;
        }

        private void ResizeAndCenterWindow()
        {
            const int preferredWidth = 1100;
            const int preferredHeight = 760;
            const int screenMargin = 48;

            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea is null)
            {
                AppWindow.Resize(new SizeInt32(preferredWidth, preferredHeight));
                return;
            }

            var workArea = displayArea.WorkArea;
            var width = Math.Min(preferredWidth, Math.Max(1, workArea.Width - screenMargin * 2));
            var height = Math.Min(preferredHeight, Math.Max(1, workArea.Height - screenMargin * 2));
            var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);

            AppWindow.Resize(new SizeInt32(width, height));
            AppWindow.Move(new PointInt32(x, y));
        }

        /// <summary>
        /// 親ウィンドウのテーマを設定ウィンドウにも適用する。
        /// </summary>
        public void ApplyTheme(ElementTheme theme)
        {
            ((FrameworkElement)Content).RequestedTheme = theme;
            MainWindow.ApplyTitleBarTheme(this, theme);
        }

        /// <summary>ナビゲーション項目と各ページのテキストを再設定する。</summary>
        internal void RefreshNavText()
        {
            Title                  = R.Get("AppSettings_Title");
            NavGeneral.Content     = R.Get("Nav_General");
            NavUserInterface.Content = R.Get("Nav_UserInterface");
            NavData.Content        = R.Get("Nav_Data");
            NavExperimental.Content = R.Get("Nav_Experimental");
            NavExtensions.Content  = R.Get("Nav_Extensions");
            NavProfiles.Content    = R.Get("Nav_Profiles");
            NavAbout.Content       = R.Get("Nav_About");
            SettingsSearchBox.PlaceholderText = R.Get("Settings_SearchPlaceholder");
        }

        /// <summary>指定タグのナビゲーション項目を選択する。</summary>
        internal void SelectPage(string tag)
        {
            foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>()
                         .Concat(NavView.FooterMenuItems.OfType<NavigationViewItem>()))
            {
                if (item.Tag?.ToString() == tag)
                {
                    NavView.SelectedItem = item;
                    break;
                }
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem item) return;
            var tag = item.Tag?.ToString();

            var pageType = tag switch
            {
                "General"      => typeof(Settings.GeneralPage),
                "UserInterface" => typeof(Settings.UserInterfacePage),
                "Data"         => typeof(Settings.UserDataPage),
                "Experimental" => typeof(Settings.ExperimentalPage),
                "Extensions"   => typeof(Settings.ExtensionsPage),
                "Profiles"     => typeof(Settings.ProfilesPage),
                "About"        => typeof(Settings.AboutPage),
                _              => null,
            };

            if (pageType is not null)
                ContentFrame.Navigate(pageType, this);
        }

        private sealed record SettingsSearchResult(string Display, string PageTag, string Keywords)
        {
            public override string ToString() => Display;
        }

        private List<SettingsSearchResult> BuildSettingsSearchIndex() =>
        [
            new(R.Get("Settings_DefaultTimeline"), "General", "timeline sidebar compose list default タイムライン サイドバー 投稿 リスト 既定"),
            new(R.Get("Settings_HomeAutoLoad"), "General", "home auto refresh interval ホーム 自動更新 間隔"),
            new(R.Get("Settings_ExternalBrowser"), "General", "browser edge link external ブラウザー Edge リンク 外部"),
            new(R.Get("Settings_Theme"), "UserInterface", "theme light dark appearance テーマ ライト ダーク 表示"),
            new(R.Get("Settings_Language"), "UserInterface", "language japanese english 言語 日本語 英語"),
            new(R.Get("Settings_ExportFolder"), "Data", "data folder export backup データ フォルダー エクスポート バックアップ"),
            new(R.Get("Settings_SavedQueries"), "Data", "search query history 検索 クエリ 履歴"),
            new(R.Get("Nav_Profiles"), "Profiles", "profile account login badge primary プロファイル アカウント ログイン バッジ"),
            new(R.Get("Nav_Extensions"), "Extensions", "extension permission site 拡張機能 権限 サイト"),
            new(R.Get("Nav_Experimental"), "Experimental", "experimental media preload 試験 メディア プリロード"),
            new(R.Get("Nav_About"), "About", "version update license privacy about バージョン 更新 ライセンス プライバシー"),
        ];

        private void SettingsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var query = sender.Text.Trim();
            sender.ItemsSource = query.Length == 0
                ? null
                : BuildSettingsSearchIndex().Where(r =>
                    r.Display.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || r.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void SettingsSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is SettingsSearchResult result) sender.Text = result.Display;
        }

        private void SettingsSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var selected = args.ChosenSuggestion as SettingsSearchResult;
            selected ??= BuildSettingsSearchIndex().FirstOrDefault(r =>
                r.Display.Contains(args.QueryText ?? sender.Text, StringComparison.CurrentCultureIgnoreCase)
                || r.Keywords.Contains(args.QueryText ?? sender.Text, StringComparison.OrdinalIgnoreCase));
            if (selected is not null) SelectPage(selected.PageTag);
        }
    }
}
