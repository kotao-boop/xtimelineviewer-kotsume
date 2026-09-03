using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;
using XTimelineViewer.Models;
using XTimelineViewer.Services;

namespace XTimelineViewer.Views.Controls
{
    /// <summary>
    /// タイムライン 1 本分のペイン（#345）。
    ///
    /// 以前は MainWindow.AddTimeline() の中でこの視覚ツリーをコードで組み立てており、
    /// ペイン単位の状態を MainWindow が複数の辞書で手持ちしていた。作る側と作り直す側が
    /// 別々に存在するせいで、同じ定数を片方だけ直す事故が繰り返し起きている
    /// （#337 / #341 のバッジ重複、#359 の番号ずれ、#362 の後始末漏れ）。
    ///
    /// 段階 2A では「視覚ツリーの宣言」だけをここへ移す。振る舞い（フォーカス、
    /// ⚙ ダイアログ、テーマ適用など）は MainWindow に残っており、段階 2B で移す。
    /// </summary>
    public sealed partial class TimelinePane : UserControl
    {
        /// <summary>このペインが表示しているタイムラインの設定。</summary>
        internal TimelineConfig Config { get; }

        internal TimelinePane(TimelineConfig config)
        {
            InitializeComponent();
            Config = config;
            _webView = WebViewHost;
            AttachFocusHandler(_webView);

            Width = config.Width;
            TypeIcon.Glyph = UrlHelper.GetTimelineGlyph(config.Url);
            HardReloadTooltip = new ToolTip();
            ToolTipService.SetToolTip(TypeIcon, HardReloadTooltip);
            AutoLoadTooltip = new ToolTip { Content = R.Get("AutoLoad_Off") };
            ToolTipService.SetToolTip(AutoLoadIcon, AutoLoadTooltip);
            RefreshLocalizedText();
            UpdateUrlHeader();
            InitializeResizeGrip();
            SizeChanged += (_, _) => UpdateHeaderDensity();

            // ヘッダーをクリックしたら自分をアクティブにする。
            // 以前は MainWindow が headerGrid に直接購読していた。
            HeaderGrid.Tapped += (_, _) => SetFocus();
            HeaderGrid.DoubleTapped += (_, _) =>
            {
                SetFocus();
                _webView.Source = new Uri(Config.Url);
            };
        }

        // ── ドラッグリサイズ ────────────────────────────────────
        private bool _isResizing = false;
        private int _unreadCount;
        private double _resizeStartWidth;
        private double _lastRequestedWidth;
        private bool _isResizingHeight;
        private double _resizeStartPointerY;
        private double _resizeStartHeight;
        private double _lastRequestedHeight;
        private bool _gridResizeMode;
        private bool _headerPointerOver;

        /// <summary>横幅がドラッグによって変更・確定された時のイベント</summary>
        internal event Action<TimelinePane, double>? WidthResizing;
        internal event Action<TimelinePane, double>? WidthResized;
        internal event Action<TimelinePane, double>? HeightResizing;
        internal event Action<TimelinePane, double>? HeightResized;
        internal event Action<TimelinePane>? RetryRequested;
        internal event Action<TimelinePane>? OpenInBrowserRequested;
        internal event Action<TimelinePane>? TemporaryHideRequested;
        internal event Action<TimelinePane>? ReloadRequested;
        internal event Action<TimelinePane>? TranslationToggleRequested;
        internal event Action<TimelinePane>? FocusModeRequested;
        internal event Action<TimelinePane>? JumpToNewestRequested;
        internal event Action<TimelinePane>? SettingsRequested;

        private void HeaderGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _headerPointerOver = true;
            UpdateHeaderDensity();
        }

        private void HeaderGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _headerPointerOver = false;
            UpdateHeaderDensity();
        }

        private void UpdateHeaderDensity()
        {
            var compact = ActualWidth > 0 && ActualWidth < 520;
            UrlLabel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            RefreshBtn.Visibility = _headerPointerOver && ActualWidth >= 420
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void InitializeResizeGrip()
        {
            ResizeGrip.PointerEntered += (s, e) =>
            {
                if (!_isResizing)
                {
                    ResizeGripBar.Opacity = 0.9;
                    try { this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast); } catch { }
                }
            };

            ResizeGrip.PointerExited += (s, e) =>
            {
                if (!_isResizing)
                {
                    ResizeGripBar.Opacity = 0.28;
                    try { this.ProtectedCursor = null; } catch { }
                }
            };

            ResizeGrip.DragStarted += (s, e) =>
            {
                _isResizing = true;
                _resizeStartWidth = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? Config.Width : Width);
                _lastRequestedWidth = _resizeStartWidth;
                ResizeGripBar.Opacity = 1.0;
            };

            ResizeGrip.DragDelta += (s, e) =>
            {
                if (!_isResizing) return;
                var minimumWidth = _gridResizeMode ? 160 : 220;
                var newWidth = Math.Clamp(_lastRequestedWidth + e.HorizontalChange, minimumWidth, 1600);
                _lastRequestedWidth = newWidth;
                if (_gridResizeMode) WidthResizing?.Invoke(this, newWidth);
                else
                {
                    Width = newWidth;
                    Config.Width = newWidth;
                }
            };

            ResizeGrip.DragCompleted += (s, e) =>
            {
                if (!_isResizing) return;
                _isResizing = false;
                ResizeGripBar.Opacity = 0.28;
                try { ProtectedCursor = null; } catch { }
                WidthResized?.Invoke(this, _gridResizeMode ? _lastRequestedWidth : Width);
            };

            VerticalResizeGrip.PointerEntered += (s, e) =>
            {
                if (!_isResizingHeight)
                {
                    VerticalResizeGripBar.Opacity = 0.9;
                    try { ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNorthSouth); } catch { }
                }
            };
            VerticalResizeGrip.PointerExited += (s, e) =>
            {
                if (!_isResizingHeight)
                {
                    VerticalResizeGripBar.Opacity = 0.28;
                    try { ProtectedCursor = null; } catch { }
                }
            };
            VerticalResizeGrip.PointerPressed += (s, e) =>
            {
                var pt = e.GetCurrentPoint(Parent as UIElement ?? this);
                if (!pt.Properties.IsLeftButtonPressed) return;
                _isResizingHeight = true;
                _resizeStartPointerY = pt.Position.Y;
                _resizeStartHeight = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 600 : Height);
                _lastRequestedHeight = _resizeStartHeight;
                VerticalResizeGrip.CapturePointer(e.Pointer);
                VerticalResizeGripBar.Opacity = 1.0;
                e.Handled = true;
            };
            VerticalResizeGrip.PointerMoved += (s, e) =>
            {
                if (!_isResizingHeight) return;
                var pt = e.GetCurrentPoint(Parent as UIElement ?? this);
                var deltaY = pt.Position.Y - _resizeStartPointerY;
                var minimumHeight = _gridResizeMode ? 140 : 180;
                var newHeight = Math.Clamp(_resizeStartHeight + deltaY, minimumHeight, 1600);
                _lastRequestedHeight = newHeight;
                if (_gridResizeMode) HeightResizing?.Invoke(this, newHeight);
                else
                {
                    Height = newHeight;
                    Config.Height = newHeight;
                }
                e.Handled = true;
            };
            VerticalResizeGrip.PointerReleased += (s, e) =>
            {
                if (!_isResizingHeight) return;
                _isResizingHeight = false;
                VerticalResizeGrip.ReleasePointerCapture(e.Pointer);
                VerticalResizeGripBar.Opacity = 0.28;
                try { ProtectedCursor = null; } catch { }
                HeightResized?.Invoke(this, _gridResizeMode
                    ? _lastRequestedHeight
                    : Height);
                e.Handled = true;
            };
            VerticalResizeGrip.PointerCaptureLost += (s, e) =>
            {
                if (!_isResizingHeight) return;
                _isResizingHeight = false;
                VerticalResizeGripBar.Opacity = 0.28;
                try { ProtectedCursor = null; } catch { }
                HeightResized?.Invoke(this, _gridResizeMode ? _lastRequestedHeight : Height);
            };

            ResizeGrip.KeyDown += ResizeGrip_KeyDown;
            VerticalResizeGrip.KeyDown += VerticalResizeGrip_KeyDown;
        }

        private void ResizeGrip_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var delta = e.Key switch
            {
                Windows.System.VirtualKey.Left => -24,
                Windows.System.VirtualKey.Right => 24,
                _ => 0,
            };
            if (delta == 0) return;

            var current = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? Config.Width : Width);
            var desired = Math.Clamp(current + delta, _gridResizeMode ? 160 : 220, 1600);
            if (_gridResizeMode) WidthResizing?.Invoke(this, desired);
            else
            {
                Width = desired;
                Config.Width = desired;
            }
            WidthResized?.Invoke(this, desired);
            e.Handled = true;
        }

        private void VerticalResizeGrip_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var delta = e.Key switch
            {
                Windows.System.VirtualKey.Up => -24,
                Windows.System.VirtualKey.Down => 24,
                _ => 0,
            };
            if (delta == 0) return;

            var current = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 600 : Height);
            var desired = Math.Clamp(current + delta, _gridResizeMode ? 140 : 180, 1600);
            if (_gridResizeMode) HeightResizing?.Invoke(this, desired);
            else
            {
                Height = desired;
                Config.Height = desired;
            }
            HeightResized?.Invoke(this, desired);
            e.Handled = true;
        }

        /// <summary>現在の配置で実際に働く境界だけを表示する。</summary>
        internal void ConfigureResizeAffordances(bool horizontal, bool vertical, bool gridMode)
        {
            _gridResizeMode = gridMode;
            ResizeGrip.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
            VerticalResizeGrip.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
            ResizeGrip.IsHitTestVisible = horizontal;
            VerticalResizeGrip.IsHitTestVisible = vertical;
            ResizeGrip.IsTabStop = horizontal;
            VerticalResizeGrip.IsTabStop = vertical;
            ResizeGripBar.Opacity = horizontal ? 0.28 : 0;
            VerticalResizeGripBar.Opacity = vertical ? 0.28 : 0;
        }

        // ── フォーカス ───────────────────────────────────────

        /// <summary>
        /// このペインがアクティブになった。MainWindow が現在のペインを
        /// 覚え直し、全ペインのヘッダー配色を当て直す。
        /// </summary>
        internal event Action<TimelinePane>? FocusRequested;

        /// <summary>このペインをアクティブにし、WebView2 へキーボードフォーカスを移す。</summary>
        internal void SetFocus()
        {
            FocusRequested?.Invoke(this);
            _webView.Focus(FocusState.Programmatic);
        }

        // ── テーマ ──────────────────────────────────────────

        /// <summary>
        /// ペインの配色を適用する。
        ///
        /// Application.Current.Resources はアプリレベルのテーマを参照するため、
        /// 要素単位で RequestedTheme を設定していると正しい辞書が返らない。
        /// 解決済みの ActualTheme を使って ThemeDictionaries を直接引く。
        /// コントラストテーマの判定は MainWindow 側の責任（引数で受け取る）。
        /// </summary>
        internal void ApplyTheme(ElementTheme theme, string? appTheme, bool focused, bool highContrast)
        {
            var themeKey  = highContrast ? "HighContrast"
                          : theme == ElementTheme.Light ? "Light" : "Default";
            var themeDict = (ResourceDictionary)Application.Current.Resources.ThemeDictionaries[themeKey];

            PaneRoot.Background = highContrast
                ? (Brush)themeDict["TimelinePaneBackgroundBrush"]
                : ThemePaletteService.GetPaneBrush(appTheme, "TimelinePaneBackgroundBrush", themeDict);

            // コントラストテーマではフォーカスを「塗り」ではなく「枠」で示す（#341）。
            // ヘッダーを Highlight 色で塗ると、中の文字色までこちらで揃えない限り
            // 地と衝突する。枠なら子要素の配色に一切干渉せずに済む。
            bool outlineFocus = focused && (highContrast || ThemePaletteService.UsesOutlineFocus(appTheme));
            var borderRole = outlineFocus
                ? "TimelineHeaderFocusedBackgroundBrush"
                : "TimelinePaneBorderBrush";
            PaneRoot.BorderBrush = highContrast
                ? (Brush)themeDict[borderRole]
                : ThemePaletteService.GetPaneBrush(appTheme, borderRole, themeDict);
            PaneRoot.BorderThickness = new Thickness(outlineFocus ? 2 : 1);

            var headerRole = focused && !outlineFocus
                ? "TimelineHeaderFocusedBackgroundBrush"
                : "TimelineHeaderBackgroundBrush";
            HeaderGrid.Background = highContrast
                ? (Brush)themeDict[headerRole]
                : ThemePaletteService.GetPaneBrush(appTheme, headerRole, themeDict);

            var resizeBrush = highContrast
                ? (Brush)themeDict["TimelinePaneBorderBrush"]
                : ThemePaletteService.GetPaneBrush(appTheme, "TimelinePaneBorderBrush", themeDict);
            ResizeGripBar.Fill = resizeBrush;
            VerticalResizeGripBar.Fill = resizeBrush;
        }

        // ── 外から触る要素 ────────────────────────────────────────────────────
        // 段階 2B でこの多くは private になり、代わりにメソッドを生やす想定。

        /// <summary>ヘッダー。クリックでのフォーカス、ドラッグでの並べ替えに使う。</summary>
        public Grid Header => HeaderGrid;

        /// <summary>枠・背景を塗る対象。UserControl 自身ではなく内側の Grid。</summary>
        public Grid Root => PaneRoot;

        // プロファイルを切り替えると、別の user data folder の環境で
        // WebView2 を作り直す必要がある。XAML の WebViewHost は初回の実体で、
        // 以降は ReplaceWebView() が差し替える。
        private WebView2 _webView;

        public WebView2 WebView => _webView;

        /// <summary>
        /// WebView2 を新しいインスタンスへ差し替えて返す。
        /// 差し替えの手順をここに集めることで、呼び出し側の手順漏れを防ぐ（#361）。
        /// イベントの再購読は呼び出し元の責任（段階 2B でこちらへ移す）。
        /// </summary>
        internal WebView2 ReplaceWebView()
        {
            PaneRoot.Children.Remove(_webView);
            _webView = new WebView2
            {
                VerticalAlignment   = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            Grid.SetRow(_webView, 1);
            PaneRoot.Children.Add(_webView);
            AutomationProperties.SetName(_webView, TitleLabel.Text);
            AttachFocusHandler(_webView);
            return _webView;
        }
        public TextBlock NumberLabel => NumberLabelText;
        public FontIcon AutoLoadIndicatorIcon => AutoLoadIcon;

        /// <summary>種別アイコンに付くツールチップ。ハードリロードの残り時間を出す。</summary>
        public ToolTip HardReloadTooltip { get; }

        /// <summary>自動更新インジケーターのツールチップ。</summary>
        public ToolTip AutoLoadTooltip { get; }

        /// <summary>ホームタイムラインかどうか。自動更新（#207）の対象判定に使う。</summary>
        public bool IsHome => UrlHelper.IsHomeUrl(Config.Url);

        // WebView2 内をクリックしたときもアクティブにする。
        // 以前は MainWindow が購読しており、プロファイル切替で作り直すと
        // 再購読を忘れてフォーカス表示が死んでいた（#361）。
        // 差し替えを行う側で購読すれば、原理的に漏れない。
        private void AttachFocusHandler(WebView2 wv)
            => wv.GotFocus += (_, _) => FocusRequested?.Invoke(this);

        // ── 表示の更新 ────────────────────────────────────────────────────────

        /// <summary>
        /// Config.Url の変更をヘッダーへ反映する。ベース URL の変更やリスト URL の
        /// ライブ解決（#211）から呼ばれる。
        /// </summary>
        public void UpdateUrlHeader()
        {
            TitleLabel.Text = TimelineLabelHelper.GetFriendlyName(Config, R.Get);
            UrlLabel.Text = Uri.TryCreate(Config.Url, UriKind.Absolute, out var u)
                ? SearchQueryHelper.DecodeSearchPath(u.Host + u.PathAndQuery)
                : Config.Url;
            TypeIcon.Glyph = UrlHelper.GetTimelineGlyph(Config.Url);
            AutomationProperties.SetName(_webView, TitleLabel.Text);
            ToolTipService.SetToolTip(TitleLabel, UrlLabel.Text);
            AutoLoadIcon.Visibility = IsHome ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>言語切り替え（#117）で呼び直す。</summary>
        public void RefreshLocalizedText()
        {
            var actionsTip = R.Get("Pane_Actions_Tooltip");
            ToolTipService.SetToolTip(ActionsBtn, actionsTip);
            AutomationProperties.SetName(ActionsBtn, actionsTip);
            var widthResize = R.Get("Pane_ResizeWidth");
            var heightResize = R.Get("Pane_ResizeHeight");
            ToolTipService.SetToolTip(ResizeGrip, widthResize);
            ToolTipService.SetToolTip(VerticalResizeGrip, heightResize);
            AutomationProperties.SetName(ResizeGrip, widthResize);
            AutomationProperties.SetName(VerticalResizeGrip, heightResize);

            TranslationMenuItem.Text = R.Get("Pane_Translation_Off");
            FocusMenuItem.Text = R.Get("Pane_Focus_Tooltip");
            TemporaryHideMenuItem.Text = R.Get("Pane_TemporaryHide_Tooltip");
            SettingsMenuItem.Text = R.Get("Pane_Settings_Tooltip");

            var refreshTip = R.Get("Pane_Refresh_Tooltip");
            ToolTipService.SetToolTip(RefreshBtn, refreshTip);
            AutomationProperties.SetName(RefreshBtn, refreshTip);
            RefreshMenuItem.Text = refreshTip;
            AutomationProperties.SetName(RefreshMenuItem, refreshTip);
            UpdateTranslationButtonVisual();
            var newItemsTip = R.Get("Pane_NewItems_Tooltip");
            ToolTipService.SetToolTip(NewItemsBtn, newItemsTip);
            AutomationProperties.SetName(NewItemsBtn, newItemsTip);
            SetUnreadCount(_unreadCount);
            StatusRetryBtn.Content = R.Get("Button_Retry");
            StatusBrowserBtn.Content = R.Get("Button_OpenBrowser");
            UpdateUrlHeader();
        }

        public void ShowLoadingState()
        {
            IsSignInRequired = false;
            NavigationStateOverlay.Visibility = Visibility.Visible;
            NavigationProgress.IsActive = true;
            NavigationProgress.Visibility = Visibility.Visible;
            NavigationStateTitle.Text = R.Get("Pane_Loading");
            NavigationStateHint.Text = string.Empty;
            NavigationActions.Visibility = Visibility.Collapsed;
        }

        public void ShowErrorState(bool signInRequired = false)
        {
            IsSignInRequired = signInRequired;
            NavigationStateOverlay.Visibility = Visibility.Visible;
            NavigationProgress.IsActive = false;
            NavigationProgress.Visibility = Visibility.Collapsed;
            NavigationStateTitle.Text = R.Get(signInRequired ? "Pane_SignInRequired" : "Pane_LoadError");
            NavigationStateHint.Text = signInRequired ? R.Get("Pane_SignInHint") : R.Get("Pane_LoadErrorHint");
            StatusRetryBtn.Content = R.Get(signInRequired ? "Button_SignIn" : "Button_Retry");
            StatusBrowserBtn.Content = R.Get("Button_OpenBrowser");
            NavigationActions.Visibility = Visibility.Visible;
        }

        public void HideNavigationState()
        {
            IsSignInRequired = false;
            NavigationProgress.IsActive = false;
            NavigationStateOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>現在のエラー表示がXへの再サインイン要求か。</summary>
        public bool IsSignInRequired { get; private set; }

        private void StatusRetryBtn_Click(object sender, RoutedEventArgs e)
            => RetryRequested?.Invoke(this);

        private void StatusBrowserBtn_Click(object sender, RoutedEventArgs e)
            => OpenInBrowserRequested?.Invoke(this);

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
            => ReloadRequested?.Invoke(this);

        private void TranslationBtn_Click(object sender, RoutedEventArgs e)
            => TranslationToggleRequested?.Invoke(this);

        private void TranslationMenuItem_Click(object sender, RoutedEventArgs e)
            => TranslationToggleRequested?.Invoke(this);

        private void FocusMenuItem_Click(object sender, RoutedEventArgs e)
            => FocusModeRequested?.Invoke(this);

        private void TemporaryHideMenuItem_Click(object sender, RoutedEventArgs e)
            => TemporaryHideRequested?.Invoke(this);

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
            => SettingsRequested?.Invoke(this);

        private void NewItemsBtn_Click(object sender, RoutedEventArgs e)
            => JumpToNewestRequested?.Invoke(this);

        public void SetUnreadCount(int count)
        {
            count = Math.Clamp(count, 0, 999);
            _unreadCount = count;
            NewItemsText.Text = string.Format(R.Get("Pane_NewItems"), count);
            NewItemsBtn.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(NewItemsBtn,
                count > 0 ? string.Format(R.Get("Pane_NewItems"), count) : R.Get("Pane_NewItems_Tooltip"));
        }

        private bool _translationEnabled;

        public void SetTranslationState(bool enabled)
        {
            _translationEnabled = enabled;
            UpdateTranslationButtonVisual();
        }

        private void UpdateTranslationButtonVisual()
        {
            var key = _translationEnabled ? "Pane_Translation_On" : "Pane_Translation_Off";
            var tip = R.Get(key);
            if (TranslationBtn is not null)
            {
                ToolTipService.SetToolTip(TranslationBtn, tip);
                AutomationProperties.SetName(TranslationBtn, tip);
            }
            TranslationMenuItem.Text = tip;
            AutomationProperties.SetName(TranslationMenuItem, tip);

            var opacity = _translationEnabled ? 1.0 : 0.55;
            var foreground = _translationEnabled
                ? (Brush)Application.Current.Resources["SystemAccentColor"]
                : null;
            TranslationIcon.Opacity = opacity;
            TranslationIcon.Foreground = foreground;
            TranslationMenuIcon.Opacity = opacity;
            TranslationMenuIcon.Foreground = foreground;
        }

        public void SetTemporaryHideAvailable(bool available)
            => TemporaryHideMenuItem.IsEnabled = available;

        /// <summary>
        /// 自動翻訳ボタンの表示場所を反映する。既定は列ヘッダーの三点メニュー内。
        /// </summary>
        public void SetTranslationButtonPlacement(string? placement)
        {
            var normalized = placement?.ToLowerInvariant() switch
            {
                "header" => "header",
                "hidden" => "hidden",
                _ => "menu",
            };

            TranslationBtn.Visibility = normalized == "header"
                ? Visibility.Visible
                : Visibility.Collapsed;
            TranslationMenuItem.Visibility = normalized == "menu"
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// 番号バッジ（#225）。表示順の 1..9 を受け取る。9 を超えるペインは null。
        /// AutomationId も振り直す（ui-smoke.ps1 が連番を検査するため）。
        /// </summary>
        public void SetNumber(int? oneBased)
        {
            if (oneBased is int n)
            {
                NumberLabelText.Text       = $"{n}.";
                NumberLabelText.Visibility = Visibility.Visible;
                ToolTipService.SetToolTip(NumberLabelText, string.Format(R.Get("Tooltip_ActivateHotkey"), n));
                AutomationProperties.SetAutomationId(NumberLabelText, $"PaneNumber{n}");
            }
            else
            {
                NumberLabelText.Text       = string.Empty;
                NumberLabelText.Visibility = Visibility.Collapsed;
                ToolTipService.SetToolTip(NumberLabelText, null);
                AutomationProperties.SetAutomationId(NumberLabelText, string.Empty);
            }
        }

        /// <summary>
        /// プロファイルバッジ。以前は Border ごと作り直して差し替えていたため、
        /// 列番号が生成側と再生成側の 2 か所に手書きされていた（#337）。
        /// 器を固定して中身だけ差し替える。
        /// </summary>
        public void SetProfileBadge(string text, Brush background, Brush foreground, bool bordered, bool visible)
        {
            ProfileBadge.Background      = background;
            ProfileBadge.BorderBrush     = bordered ? foreground : null;
            ProfileBadge.BorderThickness = new Thickness(bordered ? 1 : 0);
            ProfileBadge.Visibility      = visible ? Visibility.Visible : Visibility.Collapsed;
            ProfileBadgeText.Text        = text;
            ProfileBadgeText.Foreground  = foreground;
        }

        /// <summary>自動更新インジケーター（#207）の見た目を更新する。</summary>
        public void SetAutoLoadIndicator(string glyph, double opacity, string tooltip)
        {
            AutoLoadIcon.Glyph   = glyph;
            AutoLoadIcon.Opacity = opacity;
            AutoLoadTooltip.Content = tooltip;
        }

        /// <summary>自動更新インジケーターの表示・非表示。ホームペインのみ表示する。</summary>
        public void SetAutoLoadIndicatorVisible(bool visible)
            => AutoLoadIcon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
