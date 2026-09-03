using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        private int _autoLayoutPage;
        private int _autoLayoutPageSize = 6;
        private readonly List<Microsoft.UI.Xaml.Shapes.Rectangle> _gridResizeBars = [];

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
                if (mode == "Focus") EnterFocusMode(_focusedPane);
                else if (mode == "Auto") NormalizeCurrentLayout();
                else SetLayoutFromCommand(mode);
            }
        }

        private void UpdateLayoutMenuState()
        {
            var mode = _focusModeActive ? "Focus" : _appSettings.LayoutMode ?? "Classic";
            LayoutAutoItem.IsChecked = mode == "Auto";
            LayoutClassicItem.IsChecked = mode == "Classic";
            LayoutGrid2x2Item.IsChecked = mode == "Grid2x2";
            LayoutGrid2x3Item.IsChecked = mode == "Grid2x3";
            LayoutVerticalSplitItem.IsChecked = mode == "VerticalSplit";
            LayoutFocusItem.IsChecked = _focusModeActive;
        }

        private void ApplyLayoutMode(string? mode = null)
        {
            mode ??= _focusModeActive ? "Focus" : _appSettings.LayoutMode ?? "Classic";
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

            // 旧版で Focus が永続化されていても、一時モードとして扱い直す。
            if (!_focusModeActive && mode == "Focus") mode = "Auto";
            var safeMode = _focusModeActive
                ? "Focus"
                : LayoutPlanner.GetSafeMode(mode, visiblePanes.Count);
            if (!_focusModeActive && safeMode != mode)
            {
                _appSettings.LayoutMode = safeMode;
                LayoutSafetyBar.Message = R.Get("Layout_CapacityFallback");
                LayoutSafetyBar.IsOpen = true;
                SaveSettings();
            }
            mode = safeMode;
            UpdateLayoutMenuState();

            if (mode == "Classic")
            {
                AutoPageNavigator.Visibility = Visibility.Collapsed;
                TimelineGrid.Visibility = Visibility.Collapsed;
                TimelineScroll.Visibility = Visibility.Visible;

                TimelineGrid.Children.Clear();
                _gridResizeBars.Clear();
                TimelineGrid.RowDefinitions.Clear();
                TimelineGrid.ColumnDefinitions.Clear();

                TimelinePanel.Children.Clear();
                foreach (var pane in panes)
                {
                    ResetGridPlacement(pane);
                    pane.Visibility = IsPaneEffectivelyVisible(pane) ? Visibility.Visible : Visibility.Collapsed;
                    pane.Width = double.IsNaN(pane.Config.Width) || pane.Config.Width <= 0 ? 350 : pane.Config.Width;
                    pane.Height = double.NaN;
                    pane.HorizontalAlignment = HorizontalAlignment.Left;
                    pane.VerticalAlignment = VerticalAlignment.Stretch;
                    pane.ConfigureResizeAffordances(horizontal: IsPaneEffectivelyVisible(pane), vertical: false, gridMode: false);
                    TimelinePanel.Children.Add(pane);
                }
            }
            else
            {
                TimelineScroll.Visibility = Visibility.Collapsed;
                TimelineGrid.Visibility = Visibility.Visible;

                TimelinePanel.Children.Clear();
                TimelineGrid.Children.Clear();
                _gridResizeBars.Clear();
                TimelineGrid.RowDefinitions.Clear();
                TimelineGrid.ColumnDefinitions.Clear();

                foreach (var pane in panes) ResetGridPlacement(pane);

                if (mode == "Focus")
                {
                    AutoPageNavigator.Visibility = Visibility.Collapsed;
                    _focusedPane = _focusedPane is not null && visiblePanes.Contains(_focusedPane)
                        ? _focusedPane
                        : visiblePanes.FirstOrDefault();
                    AddGridDefinitions("Focus", rows: 1, columns: 1, useSavedWeights: false);
                    foreach (var pane in panes)
                    {
                        pane.Width = double.NaN;
                        pane.Height = double.NaN;
                        pane.HorizontalAlignment = HorizontalAlignment.Stretch;
                        pane.VerticalAlignment = VerticalAlignment.Stretch;
                        pane.Visibility = pane == _focusedPane ? Visibility.Visible : Visibility.Collapsed;
                        pane.ConfigureResizeAffordances(horizontal: false, vertical: false, gridMode: true);
                        TimelineGrid.Children.Add(pane);
                    }
                }
                else
                {
                    var arrangedPanes = visiblePanes;
                    if (mode == "Auto")
                    {
                        _autoLayoutPageSize = LayoutPlanner.GetAutoPageCapacity(
                            TimelineGrid.ActualWidth,
                            TimelineGrid.ActualHeight);
                        var pageCount = Math.Max(1, (int)Math.Ceiling((double)visiblePanes.Count / _autoLayoutPageSize));
                        _autoLayoutPage = Math.Clamp(_autoLayoutPage, 0, pageCount - 1);
                        arrangedPanes = visiblePanes
                            .Skip(_autoLayoutPage * _autoLayoutPageSize)
                            .Take(_autoLayoutPageSize)
                            .ToList();
                        UpdateAutoPageNavigator(visiblePanes.Count, pageCount);
                    }
                    else
                    {
                        _autoLayoutPage = 0;
                        AutoPageNavigator.Visibility = Visibility.Collapsed;
                    }
                    var plan = mode switch
                    {
                        "Grid2x2" => new LayoutPlanner.GridPlan(2, 2),
                        "Grid2x3" => new LayoutPlanner.GridPlan(2, 3),
                        "VerticalSplit" => new LayoutPlanner.GridPlan(2, 1),
                        _ => LayoutPlanner.GetAutoGrid(arrangedPanes.Count),
                    };
                    AddGridDefinitions(mode, plan.Rows, plan.Columns, useSavedWeights: true);

                    foreach (var hidden in panes.Where(p => !arrangedPanes.Contains(p)))
                    {
                        hidden.Visibility = Visibility.Collapsed;
                        hidden.ConfigureResizeAffordances(false, false, gridMode: true);
                        TimelineGrid.Children.Add(hidden);
                    }
                    for (var i = 0; i < arrangedPanes.Count; i++)
                    {
                        var pane = arrangedPanes[i];
                        var row = i / plan.Columns;
                        var column = i % plan.Columns;
                        pane.Visibility = Visibility.Visible;
                        pane.Width = double.NaN;
                        pane.Height = double.NaN;
                        pane.HorizontalAlignment = HorizontalAlignment.Stretch;
                        pane.VerticalAlignment = VerticalAlignment.Stretch;
                        Grid.SetRow(pane, row);
                        Grid.SetColumn(pane, column);
                        pane.ConfigureResizeAffordances(
                            horizontal: false,
                            vertical: false,
                            gridMode: true);
                        TimelineGrid.Children.Add(pane);
                    }
                    AddGridResizeHandles(plan.Rows, plan.Columns);
                }
            }

            RefreshTimelineNumbers();
            RefreshTemporaryVisibilityUi();
            ExitFocusModeBtn.Visibility = _focusModeActive ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddGridDefinitions(string mode, int rows, int columns, bool useSavedWeights)
        {
            var rowWeights = useSavedWeights
                ? GetSavedWeights(_appSettings.LayoutRowWeights, mode, rows)
                : Enumerable.Repeat(1.0, rows).ToList();
            var columnWeights = useSavedWeights
                ? GetSavedWeights(_appSettings.LayoutColumnWeights, mode, columns)
                : Enumerable.Repeat(1.0, columns).ToList();
            foreach (var weight in rowWeights)
                TimelineGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(weight, GridUnitType.Star) });
            foreach (var weight in columnWeights)
                TimelineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
        }

        /// <summary>
        /// WebView2を含む各ペインの内側ではなく、TimelineGrid直下に境界線を置く。
        /// これにより隣のWebViewに覆われず、境界のどこをつかんでも行・列を変更できる。
        /// </summary>
        private void AddGridResizeHandles(int rows, int columns)
        {
            var brush = ThemePaletteService.GetResizeBrush(_appSettings.Theme, IsHighContrast());
            for (var column = 0; column < columns - 1; column++)
                AddGridResizeHandle(verticalBoundary: true, column, rows, columns, brush);
            for (var row = 0; row < rows - 1; row++)
                AddGridResizeHandle(verticalBoundary: false, row, rows, columns, brush);
        }

        private void AddGridResizeHandle(bool verticalBoundary, int boundaryIndex, int rows, int columns, Brush brush)
        {
            var bar = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Fill = brush,
                Opacity = 0.38,
                HorizontalAlignment = verticalBoundary ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                VerticalAlignment = verticalBoundary ? VerticalAlignment.Stretch : VerticalAlignment.Center,
                Width = verticalBoundary ? 2 : double.NaN,
                Height = verticalBoundary ? double.NaN : 2,
                IsHitTestVisible = false,
            };
            var handle = new GridResizeHandle(bar)
            {
                HorizontalAlignment = verticalBoundary ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
                VerticalAlignment = verticalBoundary ? VerticalAlignment.Stretch : VerticalAlignment.Bottom,
                Width = verticalBoundary ? 16 : double.NaN,
                Height = verticalBoundary ? double.NaN : 16,
            };
            Grid.SetColumn(handle, verticalBoundary ? boundaryIndex : 0);
            Grid.SetRow(handle, verticalBoundary ? 0 : boundaryIndex);
            Grid.SetColumnSpan(handle, verticalBoundary ? 1 : columns);
            Grid.SetRowSpan(handle, verticalBoundary ? rows : 1);
            Canvas.SetZIndex(handle, 1000);
            AutomationProperties.SetName(handle, R.Get(verticalBoundary ? "Pane_ResizeWidth" : "Pane_ResizeHeight"));

            var resizing = false;
            double startPointer = 0;
            double firstStart = 0;
            double secondStart = 0;
            handle.PointerEntered += (_, _) =>
            {
                if (!resizing) bar.Opacity = 1;
                try
                {
                    handle.SetCursor(Microsoft.UI.Input.InputSystemCursor.Create(
                        verticalBoundary
                            ? Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast
                            : Microsoft.UI.Input.InputSystemCursorShape.SizeNorthSouth));
                }
                catch { }
            };
            handle.PointerExited += (_, _) =>
            {
                if (!resizing) bar.Opacity = 0.38;
            };
            handle.PointerPressed += (_, e) =>
            {
                var point = e.GetCurrentPoint(TimelineGrid);
                if (!point.Properties.IsLeftButtonPressed) return;
                resizing = true;
                startPointer = verticalBoundary ? point.Position.X : point.Position.Y;
                firstStart = verticalBoundary
                    ? TimelineGrid.ColumnDefinitions[boundaryIndex].ActualWidth
                    : TimelineGrid.RowDefinitions[boundaryIndex].ActualHeight;
                secondStart = verticalBoundary
                    ? TimelineGrid.ColumnDefinitions[boundaryIndex + 1].ActualWidth
                    : TimelineGrid.RowDefinitions[boundaryIndex + 1].ActualHeight;
                handle.CapturePointer(e.Pointer);
                bar.Opacity = 1;
                e.Handled = true;
            };
            handle.PointerMoved += (_, e) =>
            {
                if (!resizing) return;
                var point = e.GetCurrentPoint(TimelineGrid);
                var current = verticalBoundary ? point.Position.X : point.Position.Y;
                var total = firstStart + secondStart;
                var minimum = verticalBoundary ? Math.Min(160, total / 3) : Math.Min(140, total / 3);
                var first = Math.Clamp(firstStart + current - startPointer, minimum, total - minimum);
                if (verticalBoundary)
                {
                    TimelineGrid.ColumnDefinitions[boundaryIndex].Width = new GridLength(first, GridUnitType.Star);
                    TimelineGrid.ColumnDefinitions[boundaryIndex + 1].Width = new GridLength(total - first, GridUnitType.Star);
                }
                else
                {
                    TimelineGrid.RowDefinitions[boundaryIndex].Height = new GridLength(first, GridUnitType.Star);
                    TimelineGrid.RowDefinitions[boundaryIndex + 1].Height = new GridLength(total - first, GridUnitType.Star);
                }
                e.Handled = true;
            };
            handle.PointerReleased += (_, e) => FinishResize(e);
            handle.PointerCaptureLost += (_, _) => FinishResize(null);

            void FinishResize(PointerRoutedEventArgs? args)
            {
                if (!resizing) return;
                resizing = false;
                if (args is not null) handle.ReleasePointerCapture(args.Pointer);
                bar.Opacity = 0.38;
                SaveCurrentGridWeights();
            }

            _gridResizeBars.Add(bar);
            TimelineGrid.Children.Add(handle);
        }

        private void RefreshGridResizeHandleBrushes()
        {
            var brush = ThemePaletteService.GetResizeBrush(_appSettings.Theme, IsHighContrast());
            foreach (var bar in _gridResizeBars) bar.Fill = brush;
        }

        private sealed class GridResizeHandle : UserControl
        {
            internal GridResizeHandle(UIElement child)
            {
                Content = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Child = child,
                };
            }

            internal void SetCursor(Microsoft.UI.Input.InputCursor cursor) => ProtectedCursor = cursor;
        }

        private static List<double> GetSavedWeights(
            Dictionary<string, List<double>> saved,
            string mode,
            int count)
        {
            if (saved.TryGetValue(mode, out var weights)
                && weights.Count == count
                && weights.All(w => double.IsFinite(w) && w > 0))
                return [.. weights];
            return Enumerable.Repeat(1.0, count).ToList();
        }

        private void OnPaneWidthResizing(TimelinePane pane, double desiredWidth)
        {
            if (TimelineGrid.Visibility != Visibility.Visible || _focusModeActive) return;
            var column = Grid.GetColumn(pane);
            if (column < 0 || column >= TimelineGrid.ColumnDefinitions.Count - 1) return;
            var current = TimelineGrid.ColumnDefinitions[column];
            var next = TimelineGrid.ColumnDefinitions[column + 1];
            var total = current.ActualWidth + next.ActualWidth;
            if (total <= 2) return;
            var minimum = Math.Min(160, total / 3);
            var width = Math.Clamp(desiredWidth, minimum, total - minimum);
            current.Width = new GridLength(width, GridUnitType.Star);
            next.Width = new GridLength(total - width, GridUnitType.Star);
        }

        private void OnPaneHeightResizing(TimelinePane pane, double desiredHeight)
        {
            if (TimelineGrid.Visibility != Visibility.Visible || _focusModeActive) return;
            var row = Grid.GetRow(pane);
            if (row < 0 || row >= TimelineGrid.RowDefinitions.Count - 1) return;
            var current = TimelineGrid.RowDefinitions[row];
            var next = TimelineGrid.RowDefinitions[row + 1];
            var total = current.ActualHeight + next.ActualHeight;
            if (total <= 2) return;
            var minimum = Math.Min(140, total / 3);
            var height = Math.Clamp(desiredHeight, minimum, total - minimum);
            current.Height = new GridLength(height, GridUnitType.Star);
            next.Height = new GridLength(total - height, GridUnitType.Star);
        }

        private void OnPaneResizeCompleted(TimelinePane pane, double value, bool horizontal)
        {
            if (TimelineGrid.Visibility == Visibility.Visible && !_focusModeActive)
            {
                if (horizontal) OnPaneWidthResizing(pane, value);
                else OnPaneHeightResizing(pane, value);
                SaveCurrentGridWeights();
                return;
            }

            SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));
        }

        private void SaveCurrentGridWeights()
        {
            var mode = _appSettings.LayoutMode;
            _appSettings.LayoutColumnWeights[mode] = TimelineGrid.ColumnDefinitions
                .Select(c => Math.Max(1, c.ActualWidth)).ToList();
            _appSettings.LayoutRowWeights[mode] = TimelineGrid.RowDefinitions
                .Select(r => Math.Max(1, r.ActualHeight)).ToList();
            SaveSettings();
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

        private bool IsPaneDisplayed(TimelinePane pane)
            => IsPaneEffectivelyVisible(pane)
               && pane.Visibility == Visibility.Visible
               && (!_focusModeActive || pane == _focusedPane);

        private void TemporaryHideTimeline(TimelinePane pane)
        {
            if (!IsPaneEffectivelyVisible(pane)) return;
            if (Panes.Count(IsPaneEffectivelyVisible) <= 1) return;

            if (_focusModeActive) ExitFocusMode();
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
        {
            NormalizeCurrentLayout();
        }

        /// <summary>
        /// レイアウトの種類を変えず、現在の配置内の大きさだけを均等に戻す。
        /// 「自動整列」ボタンが Classic から Auto（4本なら2x2）へ切り替えてしまうと、
        /// 利用者が選んだ配置を破壊するため、レイアウト切替とは明確に分離する。
        /// </summary>
        private void NormalizeCurrentLayout()
        {
            if (_focusModeActive) return;
            var mode = _appSettings.LayoutMode ?? "Classic";
            if (mode == "Classic")
            {
                var visible = Panes.Where(IsPaneEffectivelyVisible).ToList();
                if (visible.Count == 0) return;
                var available = TimelineScroll.ActualWidth > 0 ? TimelineScroll.ActualWidth : 1200;
                var equalWidth = Math.Clamp(available / visible.Count, 280, 600);
                foreach (var pane in visible)
                {
                    pane.Width = equalWidth;
                    pane.Config.Width = equalWidth;
                    // 直前にグリッド／集中表示を使っていた場合、ペイン内の幅変更境界が
                    // Collapsed のまま残ることがある。幅をそろえるだけでなく、Classic
                    // で必要なマウス・キーボード操作も必ず復元する。
                    pane.ConfigureResizeAffordances(horizontal: true, vertical: false, gridMode: false);
                }
                SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));
                UpdateLayoutMenuState();
                return;
            }

            var columnCount = TimelineGrid.ColumnDefinitions.Count;
            var rowCount = TimelineGrid.RowDefinitions.Count;
            if (columnCount == 0 || rowCount == 0) return;
            _appSettings.LayoutColumnWeights[mode] = Enumerable.Repeat(1.0, columnCount).ToList();
            _appSettings.LayoutRowWeights[mode] = Enumerable.Repeat(1.0, rowCount).ToList();
            SaveSettings();
            ApplyLayoutMode(mode);
            UpdateLayoutMenuState();
        }

        private void UpdateAutoPageNavigator(int total, int pageCount)
        {
            if (pageCount <= 1)
            {
                AutoPageNavigator.Visibility = Visibility.Collapsed;
                return;
            }
            var first = _autoLayoutPage * _autoLayoutPageSize + 1;
            var last = Math.Min(total, first + _autoLayoutPageSize - 1);
            AutoPageStatusText.Text = string.Format(CultureInfo.CurrentCulture, "{0}–{1} / {2}", first, last, total);
            AutoPagePreviousBtn.IsEnabled = _autoLayoutPage > 0;
            AutoPageNextBtn.IsEnabled = _autoLayoutPage < pageCount - 1;
            AutoPageNavigator.Visibility = Visibility.Visible;
        }

        private void AutoPagePreviousBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_autoLayoutPage <= 0) return;
            _autoLayoutPage--;
            ApplyLayoutMode("Auto");
        }

        private void AutoPageNextBtn_Click(object sender, RoutedEventArgs e)
        {
            _autoLayoutPage++;
            ApplyLayoutMode("Auto");
        }

        private void TimelineGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_focusModeActive || _appSettings.LayoutMode != "Auto") return;
            var capacity = LayoutPlanner.GetAutoPageCapacity(e.NewSize.Width, e.NewSize.Height);
            if (capacity == _autoLayoutPageSize) return;
            _autoLayoutPageSize = capacity;
            ApplyLayoutMode("Auto");
        }

        private void RestoreHiddenBtn_Click(object sender, RoutedEventArgs e)
            => RestoreTemporarilyHiddenTimelines();

        private void ExitFocusModeBtn_Click(object sender, RoutedEventArgs e) => ExitFocusMode();

        private void EnterFocusMode(TimelinePane? pane)
        {
            pane ??= _focusedPane ?? Panes.FirstOrDefault(IsPaneEffectivelyVisible);
            if (pane is null || !IsPaneEffectivelyVisible(pane)) return;
            if (!_focusModeActive)
                _layoutModeBeforeFocus = _appSettings.LayoutMode == "Focus" ? "Auto" : _appSettings.LayoutMode;
            _focusModeActive = true;
            _focusedPane = pane;
            LayoutSafetyBar.IsOpen = false;
            ExitFocusModeBtn.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(ExitFocusModeBtn,
                string.Format(R.Get("FocusMode_Active"), TimelineLabelHelper.GetFriendlyName(pane.Config, R.Get)));
            RefreshPaneThemes();
            ApplyLayoutMode("Focus");
            pane.SetFocus();
        }

        private void ExitFocusMode()
        {
            if (!_focusModeActive) return;
            _focusModeActive = false;
            ExitFocusModeBtn.Visibility = Visibility.Collapsed;
            _appSettings.LayoutMode = LayoutPlanner.GetSafeMode(_layoutModeBeforeFocus, Panes.Count(IsPaneEffectivelyVisible));
            SaveSettings();
            ApplyLayoutMode(_appSettings.LayoutMode);
            _focusedPane?.SetFocus();
        }

        // ── タイムライン番号バッジ / 番号フォーカス（#225）──────────────────────
        // 表示順に従って 1..9 を割り当てる。10 個目以降はバッジ非表示。
        private void RefreshTimelineNumbers()
        {
            int n = 1;
            foreach (var pane in Panes)
            {
                if (!IsPaneDisplayed(pane))
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
            var panes = Panes.Where(IsPaneDisplayed).ToList();
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
            var panes = Panes.Where(IsPaneDisplayed).ToList();
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

        private void ResizeActiveTimeline_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            var (dx, dy) = sender.Key switch
            {
                Windows.System.VirtualKey.Left => (-24, 0),
                Windows.System.VirtualKey.Right => (24, 0),
                Windows.System.VirtualKey.Up => (0, -24),
                Windows.System.VirtualKey.Down => (0, 24),
                _ => (0, 0),
            };
            args.Handled = ResizePaneByKeyboard(_focusedPane, dx, dy);
        }

        private bool ResizePaneByKeyboard(TimelinePane? pane, int deltaX, int deltaY)
        {
            if (pane is null || _focusModeActive || !IsPaneEffectivelyVisible(pane)) return false;
            _focusedPane = pane;

            if (TimelineGrid.Visibility != Visibility.Visible)
            {
                if (deltaX == 0) return false;
                pane.Width = Math.Clamp(pane.ActualWidth + deltaX, 220, 1600);
                pane.Config.Width = pane.Width;
                SaveTimelinesAsync().FireAndForget(nameof(SaveTimelinesAsync));
                return true;
            }

            var changed = false;
            if (deltaX != 0 && TimelineGrid.ColumnDefinitions.Count > 1)
            {
                var paneColumn = Grid.GetColumn(pane);
                var boundary = paneColumn < TimelineGrid.ColumnDefinitions.Count - 1
                    ? paneColumn
                    : paneColumn - 1;
                if (boundary >= 0)
                {
                    var boundaryPane = Panes.FirstOrDefault(p => IsPaneDisplayed(p) && Grid.GetColumn(p) == boundary);
                    if (boundaryPane is not null)
                    {
                        var desired = TimelineGrid.ColumnDefinitions[boundary].ActualWidth
                            + (paneColumn == boundary ? deltaX : -deltaX);
                        OnPaneWidthResizing(boundaryPane, desired);
                        changed = true;
                    }
                }
            }

            if (deltaY != 0 && TimelineGrid.RowDefinitions.Count > 1)
            {
                var paneRow = Grid.GetRow(pane);
                var boundary = paneRow < TimelineGrid.RowDefinitions.Count - 1
                    ? paneRow
                    : paneRow - 1;
                if (boundary >= 0)
                {
                    var boundaryPane = Panes.FirstOrDefault(p => IsPaneDisplayed(p) && Grid.GetRow(p) == boundary);
                    if (boundaryPane is not null)
                    {
                        var desired = TimelineGrid.RowDefinitions[boundary].ActualHeight
                            + (paneRow == boundary ? deltaY : -deltaY);
                        OnPaneHeightResizing(boundaryPane, desired);
                        changed = true;
                    }
                }
            }

            if (changed) SaveCurrentGridWeights();
            return changed;
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
            pane.SetTranslationButtonPlacement(_appSettings.TranslationButtonPlacement);
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
            pane.WidthResizing += OnPaneWidthResizing;
            pane.HeightResizing += OnPaneHeightResizing;
            pane.WidthResized += (p, value) => OnPaneResizeCompleted(p, value, horizontal: true);
            pane.HeightResized += (p, value) => OnPaneResizeCompleted(p, value, horizontal: false);

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

            pane.SettingsRequested += p =>
                ShowPaneSettingsDialogAsync(p).FireAndForget(nameof(ShowPaneSettingsDialogAsync));

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
                EnterFocusMode(p);
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
