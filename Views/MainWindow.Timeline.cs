using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.UI;

using XTimelineViewer.Models;
using XTimelineViewer.Services;

using XTimelineViewer.Views.Controls;

namespace XTimelineViewer.Views
{
    public sealed partial class MainWindow : Window
    {
        // 一時非表示は「しばらく2本だけに集中したい」用途のため、保存しない。
        // アプリを再起動すれば必ず元の表示状態に戻る。
        private readonly HashSet<TimelineConfig> _temporarilyHiddenTimelines = [];

        // ── Persistence ───────────────────────────────────────────
        // 実体は Services/TimelineStore.cs（#368）。ここは例外を握って記録するだけ。

        /// <summary>
        /// タイムライン一覧を保存する。fire-and-forget で呼ばれるので、
        /// 例外は呼び出し元で観測されない。ここで必ず記録する。
        /// </summary>
        private async Task SaveTimelinesAsync()
        {
            try
            {
                await TimelineStore.SaveAsync(SaveFilePath, _configs);
            }
            catch (Exception ex)
            {
                LogError("SaveTimelinesAsync", ex);
            }
        }

        /// <summary>保存されているタイムラインを復元する。</summary>
        private void RestoreTimelines()
        {
            foreach (var cfg in TimelineStore.Load(SaveFilePath))
                AddTimeline(cfg);

            if (_appSettings.LayoutMode != "Classic")
                ApplyLayoutMode(_appSettings.LayoutMode);
        }


        // ── Drag & Drop ───────────────────────────────────────────────────────

        private void MainArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation          = DataPackageOperation.Link;
                e.DragUIOverride.Caption     = R.Get("DragCaption");
                e.DragUIOverride.IsGlyphVisible = true;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void MainArea_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var deferral = e.GetDeferral();
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    if (item is StorageFile file &&
                        file.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase))
                    {
                        var url = await ParseUrlShortcutAsync(file);
                        if (url is not null && UrlHelper.IsXUrl(url))
                            AddTimeline(CreateDefaultConfig(url));
                    }
                }
            }
            finally { deferral.Complete(); }
        }

        private static async Task<string?> ParseUrlShortcutAsync(StorageFile file)
        {
            try
            {
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > 64 * 1024) return null;
                var lines = await FileIO.ReadLinesAsync(file);
                return UrlHelper.ParseUrlShortcut(lines);
            }
            catch { }
            return null;
        }

        // ── Quick add from menu (#120) ────────────────────────────────────────

        internal const string HomeTimelineUrl          = "https://x.com/home";
        internal const string NotificationsTimelineUrl = "https://x.com/notifications";
        internal const string BookmarksTimelineUrl     = "https://x.com/i/bookmarks";

        // リスト一覧はアカウント依存の URL（https://x.com/&lt;handle&gt;/lists）。
        // 実際に追加する URL はクリック時にプロファイルのハンドルから組み立てる。
        internal static string BuildListsUrl(string handle) => $"https://x.com/{handle}/lists";

        // ハンドル依存 URL の組み立てに使うスクリーンネームを解決する。
        // Name はユーザーが編集できるため、検出済みの ScreenName を優先する。
        private static string ResolveProfileHandle(ProfileConfig profile)
            => profile.ScreenName is { Length: > 0 } sn ? sn : profile.Name;

        private void AddHomeTimelineItem_Click(object _, RoutedEventArgs __)
            => AddTimeline(CreateDefaultConfig(HomeTimelineUrl));

        private void AddNotificationsTimelineItem_Click(object _, RoutedEventArgs __)
            => AddTimeline(CreateDefaultConfig(NotificationsTimelineUrl));

        private void AddBookmarksTimelineItem_Click(object _, RoutedEventArgs __)
            => AddTimeline(CreateDefaultConfig(BookmarksTimelineUrl));

        private void AddListsTimelineItem_Click(object _, RoutedEventArgs __)
        {
            // URL を組み立てる名前付きプロファイルを決める（AddTimeline の既定割り当てと同じ）
            var profile = _profiles.FirstOrDefault(p => p.Id == SelectedToolbarProfileId)
                ?? _profiles.FirstOrDefault(p => p.Id != "default");
            if (profile is null) return;

            // 初期 URL はキャッシュ済みハンドルの推測。実ハンドルはペイン読み込み時に
            // EnsureListsUrlAsync がアクティブアカウントからライブ解決する (#211)。
            var cfg = CreateDefaultConfig(BuildListsUrl(ResolveProfileHandle(profile)));
            cfg.ProfileId    = profile.Id;
            cfg.IsListsIndex = true;
            AddTimeline(cfg);
        }

        // ── CreateDefaultConfig ───────────────────────────────────────────────

        /// <summary>
        /// AppSettings の既定値を適用した新規 TimelineConfig を生成する。
        /// 復元時（RestoreTimelinesAsync）は保存済み値をそのまま使うため、このヘルパーを経由しない。
        /// </summary>
        private TimelineConfig CreateDefaultConfig(string url) => new()
        {
            Url            = url,
            ProfileId      = SelectedToolbarProfileId ?? "default",
            HideSidebar    = _appSettings.DefaultHideSidebar,
            HideCompose    = _appSettings.DefaultHideCompose,
            HideListHeader = _appSettings.DefaultHideListHeader,
        };

        // ── ホーム自動更新インジケーター（#207）─────────────────────────────────
        // JS から postMessage('homeAutoLoad:STATUS') された状態をアイコン＋ツールチップへ反映する。
        // 状態を唯一の真実とし、設定値とアイコンがズレないようにする。
        private void UpdateAutoLoadIndicator(TimelinePane pane, string status)
        {
            string glyph, tipKey;
            double opacity;
            switch (status)
            {
                case "running":       glyph = ""; tipKey = "AutoLoad_Running";       opacity = 0.8;  break; // Refresh
                case "paused-scroll": glyph = ""; tipKey = "AutoLoad_Paused_Scroll"; opacity = 0.5;  break; // Pause
                case "paused-search": glyph = ""; tipKey = "AutoLoad_Paused_Search"; opacity = 0.5;  break;
                case "off":           glyph = ""; tipKey = "AutoLoad_Off";           opacity = 0.35; break; // Cancel
                case "idle":         glyph = ""; tipKey = "AutoLoad_Paused_Away";   opacity = 0.5;  break; // ホーム以外
                case "paused-elsewhere": glyph = ""; tipKey = "AutoLoad_Paused_Elsewhere"; opacity = 0.5;  break; // 他ペイン編集中
                default:              glyph = ""; tipKey = "AutoLoad_Paused";         opacity = 0.5;  break; // idle 等
            }
            pane.SetAutoLoadIndicator(glyph, opacity, R.Get(tipKey));
        }

        private List<TimelinePane> GetAllPanes()
        {
            var list = new List<TimelinePane>();
            list.AddRange(TimelinePanel.Children.OfType<TimelinePane>());
            list.AddRange(TimelineGrid.Children.OfType<TimelinePane>());
            return list;
        }

        // ── レイアウトテンプレート切替 ─────────────────────────────────

        private void LayoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string mode)
            {
                _appSettings.LayoutMode = mode;
                SaveSettings();
                ApplyLayoutMode(mode);
                UpdateLayoutSuggestion();
            }
        }

        private void UpdateLayoutMenuState()
        {
            var mode = _appSettings.LayoutMode ?? "Classic";
            LayoutAutoItem.IsChecked = mode == "Auto";
            LayoutClassicItem.IsChecked = mode == "Classic";
            LayoutGrid2x2Item.IsChecked = mode == "Grid2x2";
            LayoutGrid2x3Item.IsChecked = mode == "Grid2x3";
            LayoutVerticalSplitItem.IsChecked = mode == "VerticalSplit";
            LayoutFocusItem.IsChecked = mode == "Focus";
        }

        private void ApplyLayoutMode(string? mode = null)
        {
            mode ??= _appSettings.LayoutMode ?? "Classic";

            var panes = Panes.ToList();
            if (panes.Count == 0) return;
            _temporarilyHiddenTimelines.RemoveWhere(c => !_configs.Contains(c));
            var visiblePanes = panes.Where(IsPaneEffectivelyVisible).ToList();
            if (visiblePanes.Count == 0 && panes.Any(p => p.Config.IsVisible) && _temporarilyHiddenTimelines.Count > 0)
            {
                // 表示中の最後の1本が削除された場合なども、真っ暗な画面にはしない。
                _temporarilyHiddenTimelines.Clear();
                visiblePanes = panes.Where(IsPaneEffectivelyVisible).ToList();
            }
            UpdateLayoutMenuState();

            if (mode == "Classic")
            {
                TimelineGrid.Visibility = Visibility.Collapsed;
                TimelineScroll.Visibility = Visibility.Visible;

                TimelineGrid.Children.Clear();
                TimelineGrid.RowDefinitions.Clear();
                TimelineGrid.ColumnDefinitions.Clear();

                TimelinePanel.Children.Clear();
                foreach (var pane in panes)
                {
                    ResetGridPlacement(pane);
                    pane.Visibility = IsPaneEffectivelyVisible(pane) ? Visibility.Visible : Visibility.Collapsed;
                    pane.Width = double.IsNaN(pane.Config.Width) || pane.Config.Width <= 0 ? 350 : pane.Config.Width;
                    pane.HorizontalAlignment = HorizontalAlignment.Left;
                    pane.VerticalAlignment = VerticalAlignment.Stretch;
                    TimelinePanel.Children.Add(pane);
                }
            }
            else
            {
                TimelineScroll.Visibility = Visibility.Collapsed;
                TimelineGrid.Visibility = Visibility.Visible;

                TimelinePanel.Children.Clear();
                TimelineGrid.Children.Clear();
                TimelineGrid.RowDefinitions.Clear();
                TimelineGrid.ColumnDefinitions.Clear();

                foreach (var pane in panes) ResetGridPlacement(pane);

                if (mode == "Auto")
                {
                    var plan = LayoutPlanner.GetAutoGrid(visiblePanes.Count);
                    for (var row = 0; row < plan.Rows; row++)
                        TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    for (var column = 0; column < plan.Columns; column++)
                        TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    foreach (var hidden in panes.Where(p => !IsPaneEffectivelyVisible(p)))
                    {
                        hidden.Visibility = Visibility.Collapsed;
                        TimelineGrid.Children.Add(hidden);
                    }
                    for (var i = 0; i < visiblePanes.Count; i++)
                    {
                        var pane = visiblePanes[i];
                        pane.Visibility = Visibility.Visible;
                        pane.Width = double.NaN;
                        pane.HorizontalAlignment = HorizontalAlignment.Stretch;
                        pane.VerticalAlignment = VerticalAlignment.Stretch;
                        Grid.SetRow(pane, i / plan.Columns);
                        Grid.SetColumn(pane, i % plan.Columns);
                        TimelineGrid.Children.Add(pane);
                    }
                }
                else if (mode == "Grid2x2")
                {
                    TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    foreach (var hidden in panes.Where(p => !IsPaneEffectivelyVisible(p)))
                    {
                        hidden.Visibility = Visibility.Collapsed;
                        TimelineGrid.Children.Add(hidden);
                    }
                    for (int i = 0; i < visiblePanes.Count; i++)
                    {
                        var p = visiblePanes[i];
                        p.Visibility = Visibility.Visible;
                        p.Width = double.NaN;
                        p.HorizontalAlignment = HorizontalAlignment.Stretch;
                        p.VerticalAlignment = VerticalAlignment.Stretch;
                        int r = (i / 2) % 2;
                        int c = i % 2;
                        Grid.SetRow(p, r);
                        Grid.SetColumn(p, c);
                        TimelineGrid.Children.Add(p);
                    }
                }
                else if (mode == "Grid2x3")
                {
                    TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    foreach (var hidden in panes.Where(p => !IsPaneEffectivelyVisible(p)))
                    {
                        hidden.Visibility = Visibility.Collapsed;
                        TimelineGrid.Children.Add(hidden);
                    }
                    for (int i = 0; i < visiblePanes.Count; i++)
                    {
                        var p = visiblePanes[i];
                        p.Visibility = Visibility.Visible;
                        p.Width = double.NaN;
                        p.HorizontalAlignment = HorizontalAlignment.Stretch;
                        p.VerticalAlignment = VerticalAlignment.Stretch;
                        int r = (i / 3) % 2;
                        int c = i % 3;
                        Grid.SetRow(p, r);
                        Grid.SetColumn(p, c);
                        TimelineGrid.Children.Add(p);
                    }
                }
                else if (mode == "VerticalSplit")
                {
                    TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    foreach (var hidden in panes.Where(p => !IsPaneEffectivelyVisible(p)))
                    {
                        hidden.Visibility = Visibility.Collapsed;
                        TimelineGrid.Children.Add(hidden);
                    }
                    for (int i = 0; i < visiblePanes.Count; i++)
                    {
                        var p = visiblePanes[i];
                        p.Visibility = Visibility.Visible;
                        p.Width = double.NaN;
                        p.HorizontalAlignment = HorizontalAlignment.Stretch;
                        p.VerticalAlignment = VerticalAlignment.Stretch;
                        int r = i % 2;
                        Grid.SetRow(p, r);
                        Grid.SetColumn(p, 0);
                        TimelineGrid.Children.Add(p);
                    }
                }
                else if (mode == "Focus")
                {
                    TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
                    TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    foreach (var hidden in panes.Where(p => !IsPaneEffectivelyVisible(p)))
                    {
                        hidden.Visibility = Visibility.Collapsed;
                        TimelineGrid.Children.Add(hidden);
                    }
                    for (int i = 0; i < visiblePanes.Count; i++)
                    {
                        var p = visiblePanes[i];
                        p.Visibility = Visibility.Visible;
                        p.Width = double.NaN;
                        p.HorizontalAlignment = HorizontalAlignment.Stretch;
                        p.VerticalAlignment = VerticalAlignment.Stretch;
                        if (i == 0)
                        {
                            Grid.SetRow(p, 0);
                            Grid.SetRowSpan(p, 2);
                            Grid.SetColumn(p, 0);
                        }
                        else
                        {
                            int r = (i - 1) % 2;
                            Grid.SetRow(p, r);
                            Grid.SetRowSpan(p, 1);
                            Grid.SetColumn(p, 1);
                        }
                        TimelineGrid.Children.Add(p);
                    }
                }
            }

            RefreshTimelineNumbers();
            RefreshTemporaryVisibilityUi();

            if (_appSettings.LayoutMode != "Classic")
            {
                foreach (var row in TimelineGrid.RowDefinitions)
                    row.Height = new GridLength(1, GridUnitType.Star);
                foreach (var pane in Panes.Where(IsPaneEffectivelyVisible))
                {
                    var row = Grid.GetRow(pane);
                    if (Grid.GetRowSpan(pane) == 1 && pane.Config.Height >= 180 && row < TimelineGrid.RowDefinitions.Count)
                        TimelineGrid.RowDefinitions[row].Height = new GridLength(pane.Config.Height, GridUnitType.Pixel);
                }
            }
        }

        private void OnPaneHeightResized(TimelinePane pane, double height)
        {
            pane.Config.Height = Math.Clamp(height, 180, 1600);
            if (_appSettings.LayoutMode != "Classic")
            {
                var row = Grid.GetRow(pane);
                if (Grid.GetRowSpan(pane) == 1 && row < TimelineGrid.RowDefinitions.Count)
                    TimelineGrid.RowDefinitions[row].Height = new GridLength(pane.Config.Height, GridUnitType.Pixel);
            }
            SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));
        }

        private static void ResetGridPlacement(TimelinePane pane)
        {
            Grid.SetRow(pane, 0);
            Grid.SetColumn(pane, 0);
            Grid.SetRowSpan(pane, 1);
            Grid.SetColumnSpan(pane, 1);
        }

        private bool IsPaneEffectivelyVisible(TimelinePane pane)
            => pane.Config.IsVisible && !_temporarilyHiddenTimelines.Contains(pane.Config);

        private void TemporaryHideTimeline(TimelinePane pane)
        {
            if (!IsPaneEffectivelyVisible(pane)) return;
            if (Panes.Count(IsPaneEffectivelyVisible) <= 1) return;

            _temporarilyHiddenTimelines.Add(pane.Config);
            ApplyLayoutMode();
        }

        private void RestoreTemporarilyHiddenTimelines()
        {
            if (_temporarilyHiddenTimelines.Count == 0) return;
            _temporarilyHiddenTimelines.Clear();
            ApplyLayoutMode();
        }

        private void RefreshTemporaryVisibilityUi()
        {
            var count = _temporarilyHiddenTimelines.Count(c => _configs.Contains(c));
            RestoreHiddenBtn.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            HiddenTimelineCountText.Text = count.ToString(CultureInfo.CurrentCulture);
            var restoreTip = string.Format(R.Get("Timeline_RestoreHidden"), count);
            ToolTipService.SetToolTip(RestoreHiddenBtn, restoreTip);
            AutomationProperties.SetName(RestoreHiddenBtn, restoreTip);

            var canHide = Panes.Count(IsPaneEffectivelyVisible) > 1;
            foreach (var pane in Panes) pane.SetTemporaryHideAvailable(canHide && IsPaneEffectivelyVisible(pane));
        }

        private void AutoArrangeBtn_Click(object sender, RoutedEventArgs e)
            => SetLayoutFromCommand("Auto");

        private void RestoreHiddenBtn_Click(object sender, RoutedEventArgs e)
            => RestoreTemporarilyHiddenTimelines();

        // ── タイムライン番号バッジ / 番号フォーカス（#225）──────────────────────
        // 表示順に従って 1..9 を割り当てる。10 個目以降はバッジ非表示。
        private void RefreshTimelineNumbers()
        {
            int n = 1;
            foreach (var pane in Panes)
            {
                if (!IsPaneEffectivelyVisible(pane))
                {
                    pane.SetNumber(null);
                    continue;
                }
                pane.SetNumber(n <= 9 ? n : null);
                n++;
            }
        }

        // Ctrl+数字 で、表示順 oneBased 番目のタイムラインをアクティブ化する。
        private void FocusTimelineByIndex(int oneBased)
        {
            var panes = Panes.Where(IsPaneEffectivelyVisible).ToList();
            int i = oneBased - 1;
            if (i < 0 || i >= panes.Count) return;
            {
                panes[i].SetFocus();
                panes[i].StartBringIntoView();
            }
        }

        // Ctrl+←/→（WebView2 非フォーカス時）。現在アクティブなペインを基準に隣へ移動する。
        private void FocusAdjacentFromActive(int direction)
        {
            var panes = Panes.Where(IsPaneEffectivelyVisible).ToList();
            if (panes.Count == 0) return;

            int cur = _focusedPane is null ? -1 : panes.IndexOf(_focusedPane);

            int next = cur < 0 ? (direction > 0 ? 0 : panes.Count - 1) : cur + direction;
            if (next < 0 || next >= panes.Count) return;
            panes[next].SetFocus();
            panes[next].StartBringIntoView();
        }

        // ── ペインの並べ替え（ドラッグ / キーボード #344）─────────────────
        // TimelinePanel の子と _configs を同じ順序で入れ替え、番号バッジを振り直して保存する。
        // to には「取り除く前のインデックス」を渡す（ドラッグ先ペインの位置）。
        private void MovePaneTo(TimelinePane pane, int to)
        {
            int from = _configs.IndexOf(pane.Config);
            if (from < 0 || to < 0 || from == to) return;
            MoveConfig(from, to);

            // 視覚ツリーへの再挿入後、WebView2 の Win32 HWND を再アンカーさせる
            pane.Visibility = Visibility.Collapsed;
            pane.UpdateLayout();
            pane.Visibility = Visibility.Visible;
        }

        // 隣のペインと入れ替える（#344）。端では止まる（ラップしない）。
        // 再挿入でフォーカスが外れるので、連続して押せるよう戻しておく。
        private void MovePaneAdjacent(TimelinePane pane, int direction)
        {
            var visible = Panes.Where(IsPaneEffectivelyVisible).ToList();
            int visibleFrom = visible.IndexOf(pane);
            if (visibleFrom < 0) return;
            int visibleTo = visibleFrom + direction;
            if (visibleTo < 0 || visibleTo >= visible.Count) return;
            MovePaneTo(pane, _configs.IndexOf(visible[visibleTo].Config));

            pane.SetFocus();
            pane.StartBringIntoView();  // 視界外なら横スクロールして表示（#231）
        }

        // Ctrl+Shift+←/→（WebView2 非フォーカス時）。アクティブなペインを動かす。
        private void MovePaneFromActive(int direction)
        {
            if (_focusedPane is null) return;
            MovePaneAdjacent(_focusedPane, direction);
        }

        // ── AddTimeline ───────────────────────────────────────────────────────

        private void AddTimeline(TimelineConfig cfg)
        {
            // ProfileId が未指定または default の場合、最初の名前付きプロファイルを割り当てる
            if (cfg.ProfileId == "default")
            {
                var named = _profiles.FirstOrDefault(p => p.Id != "default");
                if (named is not null) cfg.ProfileId = named.Id;
            }

            _configs.Add(cfg);
            SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));

            ViewModel.HasTimelines = true;

            // Pane
            // 以前はここでペインの視覚ツリーをコードで組み立てていた（約 150 行）。
            // 列番号などの定数が複数箇所に手書きされ、片方だけ直す事故が
            // 繰り返し起きていたので TimelinePane.xaml へ移した（#345）。
            var pane       = new TimelinePane(cfg);
            pane.Visibility = cfg.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            var headerGrid = pane.Header;
            ApplyProfileBadge(pane);

            // Theme
            // 配色の適用は TimelinePane 側へ移した。ここは「いつ呼ぶか」だけを決める。
            pane.ActualThemeChanged += (s, _) => ApplyPaneTheme(pane);

            // ヘッダークリックや WebView2 の GotFocus は TimelinePane が拾い、ここへ上がってくる。
            pane.FocusRequested += p =>
            {
                _focusedPane = p;
                RefreshPaneThemes();
            };

            // 手動ドラッグによる幅変更を保存する
            pane.WidthResized += (p, w) => SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));
            pane.HeightResized += OnPaneHeightResized;

            TimelinePanel.Children.Add(pane);
            ApplyPaneTheme(pane);
            RefreshTimelineNumbers();  // 番号バッジを振り直す（#225）
            UpdateAutoLoadIndicator(pane, _appSettings.HomeAutoLoadEnabled ? "running" : "off");


            AttachWebViewHandlers(pane, pane.WebView);

            // ── Drag & Drop reorder ───────────────────────────────────────────

            headerGrid.CanDrag = true;
            headerGrid.DragStarting += (s, args) =>
            {
                _draggingPane = pane;
                args.Data.SetText("xtv-pane");
            };

            pane.AllowDrop = true;
            pane.DragOver  += (s, args) =>
            {
                if (_draggingPane is not null && _draggingPane != pane)
                {
                    args.AcceptedOperation = DataPackageOperation.Move;
                    args.Handled = true;
                }
            };
            pane.Drop += (s, args) =>
            {
                if (_draggingPane is null || _draggingPane == pane) return;
                args.Handled = true;

                var dragging = _draggingPane;

                MovePaneTo(dragging, TimelinePanel.Children.IndexOf(pane));

                dragging.Opacity = 1.0;
                _draggingPane = null;
            };
            pane.DragLeave += (s, args) => pane.Opacity = 1.0;
            headerGrid.DragStarting += (s, args) => pane.Opacity = 0.5;

            // ── Settings dialog ───────────────────────────────────────────────

            pane.SettingsButton.Click += async (s, e2) => await ShowPaneSettingsDialogAsync(pane);

            pane.RetryRequested += p =>
            {
                if (p.IsSignInRequired &&
                    _profiles.FirstOrDefault(profile => profile.Id == p.Config.ProfileId) is { } profile)
                {
                    AddProfileWindow.ShowReloginModal(this, profile, updated =>
                    {
                        profile.Name = updated.Name;
                        profile.ScreenName = updated.ScreenName;
                        SaveProfiles();
                        RefreshAllProfileBadges();
                        foreach (var target in Panes.Where(x => x.Config.ProfileId == profile.Id))
                        {
                            target.ShowLoadingState();
                            target.WebView.Source = new Uri(target.Config.Url);
                        }
                    });
                    return;
                }
                p.ShowLoadingState();
                p.WebView.Source = new Uri(p.Config.Url);
            };
            pane.OpenInBrowserRequested += p =>
                LaunchUriByEdgeProfileAsync(new Uri(p.Config.Url)).FireAndForget(nameof(LaunchUriByEdgeProfileAsync));
            pane.TemporaryHideRequested += TemporaryHideTimeline;
            pane.ReloadRequested += p =>
            {
                p.SetUnreadCount(0);
                try { p.WebView.Reload(); }
                catch { p.WebView.Source = new Uri(p.Config.Url); }
            };
            pane.TranslationToggleRequested += p =>
                SendTranslationCommandAsync(p, "toggle").FireAndForget(nameof(SendTranslationCommandAsync));
            pane.FocusModeRequested += p =>
            {
                _focusedPane = p;
                RefreshPaneThemes();
                _appSettings.LayoutMode = "Focus";
                SaveSettings();
                ApplyLayoutMode("Focus");
            };
            pane.JumpToNewestRequested += p =>
            {
                p.SetUnreadCount(0);
                p.WebView.CoreWebView2?.ExecuteScriptAsync(
                    "window.scrollTo({top:0,behavior:'smooth'});window._xtvUnreadReset&&window._xtvUnreadReset();")
                    .AsTask().FireAndForget("JumpToNewest");
            };

            pane.ShowLoadingState();

            InitWebViewAsync(pane.WebView, cfg).FireAndForget(nameof(InitWebViewAsync));

            if (_appSettings.LayoutMode != "Classic")
            {
                ApplyLayoutMode(_appSettings.LayoutMode);
            }
            UpdateLayoutSuggestion();
        }

        // ⚙ でプロファイルを切り替えると WebView2 を作り直すので、
        // 購読をここに集めて両方から呼ぶ（#361）。
        // フォーカス（GotFocus）は TimelinePane が自分で拾うのでここには無い。
        // 残るのは MainWindow の状態（ハードリロード）に依存するものだけ。
        private void AttachWebViewHandlers(TimelinePane pane, WebView2 wv)
        {
            wv.PointerEntered += (s, e) => { _pointerOverWebViews.Add(wv);    EvaluateHardReloadPause(wv); };
            wv.PointerExited  += (s, e) =>
            {
                _pointerOverWebViews.Remove(wv);
                EvaluateHardReloadPause(wv);
            };

            _hardReloadUiUpdaters[wv] = () => UpdateHardReloadTooltip(wv, pane.HardReloadTooltip);
            EnsureHardReloadUiTimer();
        }

        /// <summary>
        /// ペインの ⚙ 設定ダイアログ（#370）。
        /// 以前は AddTimeline の中に約 235 行のラムダとして埋まっていた。
        /// XAML 化はしていない。中身の大半は UI 構築ではなく適用・削除の
        /// ロジックで、XAML へ移しても減らないため（判断の経緯は #370）。
        /// </summary>
        private async Task ShowPaneSettingsDialogAsync(TimelinePane pane)
        {
            var cfg = pane.Config;

            var widthBox = new NumberBox
            {
                Header                  = R.Get("Timeline_Width"),
                Value                   = cfg.Width,
                Minimum                 = 100,
                Maximum                 = 2000,
                SmallChange             = 10,
                LargeChange             = 50,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Width                   = 160,
                HorizontalAlignment     = HorizontalAlignment.Left,
            };

            var nameBox = new TextBox
            {
                Header = R.Get("Timeline_Name"),
                Text = cfg.Name,
                MaxLength = 80,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var hideSidebarToggle = new ToggleSwitch
            {
                Header     = R.Get("Timeline_Sidebar"),
                IsOn       = cfg.HideSidebar,
                OnContent  = R.Get("Toggle_Hide"),
                OffContent = R.Get("Toggle_Show")
            };

            var hideComposeToggle = new ToggleSwitch
            {
                Header     = R.Get("Timeline_Compose"),
                IsOn       = cfg.HideCompose,
                OnContent  = R.Get("Toggle_Hide"),
                OffContent = R.Get("Toggle_Show")
            };

            var listHeaderApplicable = UrlHelper.IsListHeaderApplicable(cfg.Url);
            var hideListHeaderToggle = new ToggleSwitch
            {
                Header     = R.Get("Timeline_ListHeader"),
                IsOn       = cfg.HideListHeader,
                IsEnabled  = listHeaderApplicable,
                OnContent  = R.Get("Toggle_Hide"),
                OffContent = R.Get("Toggle_Show")
            };

            var hardReloadToggle = new ToggleSwitch
            {
                Header     = R.Get("Timeline_ReloadInterval"),
                IsOn       = cfg.HardReloadEnabled,
                OnContent  = R.Get("Toggle_On"),
                OffContent = R.Get("Toggle_Off"),
            };
            var hardReloadIntervalBox = new NumberBox
            {
                Value                   = cfg.HardReloadInterval,
                Minimum                 = 1,
                Maximum                 = 60,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Width                   = 160,
                HorizontalAlignment     = HorizontalAlignment.Left,
                IsEnabled               = cfg.HardReloadEnabled,
            };
            hardReloadToggle.Toggled += (_, _) =>
                hardReloadIntervalBox.IsEnabled = hardReloadToggle.IsOn;
            // トグルと同じラベルのグループなので、見た目は変えず名前だけ UIA に与える（#344）
            AutomationProperties.SetName(hardReloadIntervalBox, R.Get("Timeline_ReloadInterval"));

            var deleteBtn = new Button
            {
                Content             = R.Get("Pane_Delete"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin              = new Thickness(0, 16, 0, 0),
                Foreground          = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            };

            var translationConsentBtn = new Button
            {
                Content = R.Get("Pane_Translation_Consent"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var profileBox = new ComboBox
            {
                Header              = R.Get("Timeline_Profile"),
                MinWidth            = 200,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            foreach (var p in _profiles.Where(p => p.Id != "default"))
                profileBox.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });
            profileBox.SelectedItem = profileBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Tag == cfg.ProfileId)
                ?? profileBox.Items.OfType<ComboBoxItem>().FirstOrDefault();

            // ── ベース URL (#189) ──
            // WebView2 の現在の表示 URL がベース URL と異なる（別ページを閲覧中）かつ
            // X の URL のときだけ「現在のページをベース URL にする」を有効化する。
            var currentSource = pane.WebView.CoreWebView2?.Source ?? cfg.Url;
            string? stagedBaseUrl = null;  // ［適用］で確定する新しいベース URL

            var baseUrlText = new TextBlock
            {
                Text                   = SearchQueryHelper.DecodeSearchPath(cfg.Url),
                FontSize               = 12,
                Opacity                = 0.8,
                TextWrapping           = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };

            var setBaseUrlBtn = new Button
            {
                Content             = R.Get("Timeline_SetBaseUrl"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled           = UrlHelper.IsXUrl(currentSource)
                                   && !UrlHelper.IsOnBaseUrl(currentSource, cfg.Url),
            };
            setBaseUrlBtn.Click += (_, _) =>
            {
                stagedBaseUrl        = currentSource;
                baseUrlText.Text     = SearchQueryHelper.DecodeSearchPath(stagedBaseUrl);
                setBaseUrlBtn.IsEnabled = false;
            };

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = R.Get("Timeline_BaseUrl") });
            panel.Children.Add(baseUrlText);
            panel.Children.Add(setBaseUrlBtn);
            panel.Children.Add(new NavigationViewItemSeparator { Margin = new Thickness(0, 8, 0, 0) });
            // ラベルは別立ての TextBlock ではなく各コントロールの Header に持たせる。
            // コード生成でも UI Automation 上の関連付けが成立する（#344）。
            panel.Children.Add(profileBox);
            panel.Children.Add(nameBox);
            panel.Children.Add(new TextBlock
            {
                Text = R.Get("Timeline_NameDescription"),
                FontSize = 12,
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(widthBox);
            panel.Children.Add(hideSidebarToggle);
            panel.Children.Add(hideComposeToggle);
            panel.Children.Add(hideListHeaderToggle);
            panel.Children.Add(hardReloadToggle);
            panel.Children.Add(hardReloadIntervalBox);
            panel.Children.Add(new NavigationViewItemSeparator { Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(translationConsentBtn);
            panel.Children.Add(new NavigationViewItemSeparator { Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(deleteBtn);

            var dlg = new ContentDialog
            {
                Title             = R.Get("Timeline_Settings_Title"),
                Content           = new ScrollViewer
                {
                    Content = panel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
                PrimaryButtonText = R.Get("Button_Apply"),
                CloseButtonText   = R.Get("Button_Cancel"),
                DefaultButton     = ContentDialogButton.Primary,
                XamlRoot          = Content.XamlRoot
            };

            bool shouldDelete = false;
            bool shouldOpenTranslationConsent = false;
            deleteBtn.Click += (_, _) => { shouldDelete = true; dlg.Hide(); };
            translationConsentBtn.Click += (_, _) => { shouldOpenTranslationConsent = true; dlg.Hide(); };

            var result = await ShowDialogAsync(dlg);

            if (shouldDelete)
            {
                await ConfirmAndRemoveTimelineAsync(pane);
            }
            else if (shouldOpenTranslationConsent)
            {
                await SendTranslationCommandAsync(pane, "settings");
            }
            else if (result == ContentDialogResult.Primary)
            {
                // ベース URL の変更を反映 (#189)。プロファイル再生成より前に cfg.Url を
                // 更新しておき、再生成時は新しいベース URL へ遷移させる。
                bool baseUrlChanged = stagedBaseUrl is not null && stagedBaseUrl != cfg.Url;
                cfg.Name = nameBox.Text.Trim();
                pane.UpdateUrlHeader();
                if (baseUrlChanged)
                {
                    cfg.Url = stagedBaseUrl!;
                    // 具体ページを明示的に固定したので、リスト一覧の自動追従は解除する (#211)
                    cfg.IsListsIndex = false;
                    pane.UpdateUrlHeader();
                }

                var prevProfileId = cfg.ProfileId;
                cfg.ProfileId = (profileBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "default";

                // リスト一覧（IsListsIndex）はプロファイル切り替え後、再生成したペインの
                // 読み込み時に EnsureListsUrlAsync がアクティブアカウントのハンドルで
                // ライブ解決するため、ここでは URL を組み立てない (#211)

                cfg.Width  = Math.Clamp(widthBox.Value, 100, 2000);
                pane.Width = cfg.Width;

                cfg.HideSidebar = hideSidebarToggle.IsOn;
                cfg.HideCompose = hideComposeToggle.IsOn;
                cfg.HideListHeader = hideListHeaderToggle.IsOn;
                cfg.HardReloadEnabled  = hardReloadToggle.IsOn;
                cfg.HardReloadInterval = (int)Math.Clamp(hardReloadIntervalBox.Value, 1, 60);

                if (prevProfileId != cfg.ProfileId)
                {
                    CleanupWebView(pane.WebView);
                    // 差し替え手順は TimelinePane 側に集めてある（行番号の手書きを残さない）
                    pane.ReplaceWebView();
                    AttachWebViewHandlers(pane, pane.WebView);  // 再購読しないとフォーカス表示とホバー制御が死ぬ（#361）

                    ApplyProfileBadge(pane);

                    Debug.WriteLine($"[Profile] WebView2 recreated for profile switch: {prevProfileId} -> {cfg.ProfileId}");
                    InitWebViewAsync(pane.WebView, cfg).FireAndForget(nameof(InitWebViewAsync));
                }
                else
                {
                    if (pane.WebView.CoreWebView2 is not null)
                    {
                        await ApplyHideSidebarAsync(pane.WebView, cfg.HideSidebar);
                        await ApplyHideComposeAsync(pane.WebView, cfg.HideCompose);
                        await ApplyHideListHeaderAsync(pane.WebView, cfg.HideListHeader);
                    }

                    // ベース URL を現在のページに合わせたので乖離状態を解消し、
                    // 定期ハードリロードの一時停止を再評価する
                    if (baseUrlChanged)
                    {
                        _urlDivergedWebViews.Remove(pane.WebView);
                        EvaluateHardReloadPause(pane.WebView);
                    }
                }

                StartHardReloadTimer(pane.WebView, cfg);
                await SaveTimelinesAsync();
            }
        }

        private async Task<bool> ConfirmAndRemoveTimelineAsync(TimelinePane pane)
        {
            var name = TimelineLabelHelper.GetFriendlyName(pane.Config, R.Get);
            var confirm = new ContentDialog
            {
                Title = R.Get("Timeline_DeleteConfirmTitle"),
                Content = string.Format(R.Get("Timeline_DeleteConfirmBody"), name),
                PrimaryButtonText = R.Get("Timeline_DeleteConfirm"),
                CloseButtonText = R.Get("Button_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await ShowDialogAsync(confirm) != ContentDialogResult.Primary) return false;

            await RemoveTimelineAsync(pane);
            return true;
        }

        private async Task RemoveTimelineAsync(TimelinePane pane)
        {
            if (_enlargedPane == pane) RestorePaneSize();
            CleanupWebView(pane.WebView);
            _configs.Remove(pane.Config);
            if (_focusedPane == pane) _focusedPane = null;

            TimelinePanel.Children.Remove(pane);
            TimelineGrid.Children.Remove(pane);

            if (_hardReloadUiUpdaters.Count == 0)
            {
                _hardReloadUiTimer?.Stop();
                _hardReloadUiTimer = null;
            }

            await SaveTimelinesAsync();
            ViewModel.HasTimelines = _configs.Count > 0;
            RefreshTimelineNumbers();
            RefreshPaneThemes();
            if (_configs.Count > 0)
            {
                ApplyLayoutMode();
            }
            else
            {
                _temporarilyHiddenTimelines.Clear();
                RefreshTemporaryVisibilityUi();
            }
            UpdateLayoutSuggestion();
        }

    }
}
