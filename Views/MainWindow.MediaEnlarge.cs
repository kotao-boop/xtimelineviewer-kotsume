using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using System;

using XTimelineViewer.Views.Controls;

namespace XTimelineViewer.Views
{
    public sealed partial class MainWindow : Window
    {
        // 画像表示中のペインの一時拡大（試験機能 #287）。
        //
        // WebView2 の親要素（ペイン）は変更しない。visual tree を移し替えると
        // CoreWebView2 が作り直され、開いていたライトボックスや再生中の動画が
        // リセットされてしまう（#32 の失敗と同じ問題）。代わりに、対象ペインが
        // 属する横並び StackPanel（TimelinePanel）内で、他のペインを一時的に
        // 隠し、対象ペインの幅だけ表示領域いっぱいに広げる。

        /// <summary>対象ペインを一時拡大する。他のペインは一時的に非表示になる。</summary>
        private void EnlargePane(TimelinePane pane)
        {
            if (_enlargedPane == pane || !IsPaneDisplayed(pane)) return;
            if (_enlargedPane is not null) RestorePaneSize();
            _mediaRow = Grid.GetRow(pane);
            _mediaColumn = Grid.GetColumn(pane);
            _mediaRowSpan = Grid.GetRowSpan(pane);
            _mediaColumnSpan = Grid.GetColumnSpan(pane);
            _enlargedPane = pane;
            ApplyMediaPresentation();
        }

        private int _mediaRow, _mediaColumn, _mediaRowSpan = 1, _mediaColumnSpan = 1;

        private void ApplyMediaPresentation()
        {
            if (_enlargedPane is not { } pane) return;
            foreach (var p in Panes)
            {
                p.Visibility = p == pane ? Visibility.Visible : Visibility.Collapsed;
                p.ConfigureResizeAffordances(false, false, TimelineGrid.Visibility == Visibility.Visible);
            }
            foreach (var handle in TimelineGrid.Children.OfType<GridResizeHandle>()) handle.Visibility = Visibility.Collapsed;
            if (TimelineGrid.Visibility == Visibility.Visible)
            {
                Grid.SetRow(pane, 0);
                Grid.SetColumn(pane, 0);
                Grid.SetRowSpan(pane, Math.Max(1, TimelineGrid.RowDefinitions.Count));
                Grid.SetColumnSpan(pane, Math.Max(1, TimelineGrid.ColumnDefinitions.Count));
                pane.Width = double.NaN;
            }
            else UpdateEnlargedPaneWidth();
        }

        /// <summary>拡大表示を解除し、全ペインの表示状態と幅を元に戻す。</summary>
        private void RestorePaneSize()
        {
            if (_enlargedPane is null) return;

            var pane = _enlargedPane;
            _enlargedPane = null;

            Grid.SetRow(pane, _mediaRow);
            Grid.SetColumn(pane, _mediaColumn);
            Grid.SetRowSpan(pane, _mediaRowSpan);
            Grid.SetColumnSpan(pane, _mediaColumnSpan);
            var grid = TimelineGrid.Visibility == Visibility.Visible;
            foreach (var p in Panes)
            {
                p.Visibility = _presentation.IsDisplayed(p, p.Config) ? Visibility.Visible : Visibility.Collapsed;
                p.ConfigureResizeAffordances(!grid && IsPaneEffectivelyVisible(p), false, grid);
            }
            pane.Width = grid ? double.NaN : pane.Config.Width;
            foreach (var handle in TimelineGrid.Children.OfType<GridResizeHandle>()) handle.Visibility = Visibility.Visible;
        }

        /// <summary>拡大中のペイン幅を、表示領域（TimelineScroll のビューポート）いっぱいに合わせる。</summary>
        private void UpdateEnlargedPaneWidth()
        {
            if (_enlargedPane is null || TimelineGrid.Visibility == Visibility.Visible) return;

            var target = TimelineScroll.ActualWidth
                         - TimelinePanel.Padding.Left - TimelinePanel.Padding.Right
                         - _enlargedPane.Margin.Left - _enlargedPane.Margin.Right;
            if (target > 0)
                _enlargedPane.Width = target;
        }

        private void TimelineScroll_SizeChanged(object sender, SizeChangedEventArgs e)
            => UpdateEnlargedPaneWidth();
    }
}
