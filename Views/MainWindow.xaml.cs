using Microsoft.UI.Windowing;
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
using System.Security.Cryptography;
using System.Text;
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
using XTimelineViewer.ViewModels;

using XTimelineViewer.Views.Controls;

namespace XTimelineViewer.Views
{
    public sealed partial class MainWindow : Window
    {
        /// <summary>x:Bind のバインディングソース。XAML から参照される。</summary>
        public MainWindowViewModel ViewModel { get; } = new();

        private static readonly string SaveFilePath      = GetDataFilePath("timelines.json");
        private static readonly string SettingsFilePath  = GetDataFilePath("settings.json");
        private static readonly string ProfilesFilePath  = GetDataFilePath("profiles.json");
        private static readonly string WorkspacesFilePath = GetDataFilePath("workspaces.json");

        // 終了時に一度だけ保存してから閉じ直すためのフラグ（#338）
        private bool _closeHandled;
        private bool _socialSignInDialogOpen;

        // MSIX パッケージ環境では ApplicationData.Current.LocalFolder を使用する。
        // 旧バージョン（アンパッケージド）からの移行のため、旧パスにファイルが存在すれば自動コピーする。
        private static string GetDataFilePath(string filename)
        {
            if (!PackageContext.IsPackaged)
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XTimelineViewer", filename);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                return path;
            }

            var newPath = Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path, filename);

            if (!File.Exists(newPath))
            {
                var oldPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XTimelineViewer", filename);
                if (File.Exists(oldPath))
                    File.Copy(oldPath, newPath);
            }
            return newPath;
        }

        internal static string GetExtensionsDir()
        {
            var sourceDir = Path.Combine(AppContext.BaseDirectory, "extensions");
            if (!PackageContext.IsPackaged) return sourceDir;

            var localDir = Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path, "extensions");
            if (!Directory.Exists(sourceDir)) return localDir;

            // WebView2 は拡張機能の JavaScript/CSS を読み続けることがあるため、
            // 毎起動・毎ペインで localDir を削除すると、別の WebView2 が使用中の
            // ファイル（特に content.css）を消せず IOException になる。
            // パッケージの内容を指紋付きのマーカーで一度だけ同期し、同じ内容なら
            // 既存ファイルへ触れない。更新時は一時フォルダーを作り、現在のミラーを
            // 退避してから入れ替える。使用中のフォルダーを再帰削除しないので、
            // WebView2のファイルロックで途中まで消えることもない。
            lock (PackagedExtensionsGate)
            {
                PreparePackagedExtensions(sourceDir, localDir);
            }
            return localDir;
        }

        private const string PackagedExtensionsMarker = ".xtv-bundle-fingerprint";
        private static readonly object PackagedExtensionsGate = new();
        private static string? _preparedPackagedExtensionsFingerprint;

        private static void PreparePackagedExtensions(string sourceDir, string localDir)
        {
            string fingerprint;
            try
            {
                fingerprint = ComputeExtensionsFingerprint(sourceDir);
            }
            catch (Exception ex)
            {
                // パッケージ内のファイルを読めない場合でも、既存のミラーがあれば
                // それを使って起動を続ける。次回起動時にもう一度同期を試す。
                Debug.WriteLine($"[Extensions] Could not fingerprint packaged extensions: {ex.Message}");
                AppLog.Debug($"Packaged extension fingerprint failed: {ex.Message}");
                return;
            }

            if (string.Equals(_preparedPackagedExtensionsFingerprint, fingerprint,
                              StringComparison.OrdinalIgnoreCase))
                return;

            var markerPath = Path.Combine(localDir, PackagedExtensionsMarker);
            if (IsPackagedExtensionsReady(sourceDir, localDir, markerPath, fingerprint))
            {
                _preparedPackagedExtensionsFingerprint = fingerprint;
                return;
            }

            var stagingDir = localDir + ".staging-" + Guid.NewGuid().ToString("N");
            string? previousDir = null;
            try
            {
                Directory.CreateDirectory(stagingDir);
                foreach (var src in Directory.GetDirectories(sourceDir))
                {
                    if ((File.GetAttributes(src) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
                    CopyDirectory(src, Path.Combine(stagingDir, Path.GetFileName(src)));
                }

                // マーカーを先に書く。入れ替えが完了したフォルダーだけが「準備済み」と
                // 判定されるので、途中で終了しても次回に再構築できる。
                File.WriteAllText(
                    Path.Combine(stagingDir, PackagedExtensionsMarker),
                    fingerprint,
                    Encoding.UTF8);

                if (Directory.Exists(localDir))
                {
                    previousDir = localDir + ".previous-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        // Directory.Move は失敗しても元のミラーを変更しない。
                        // 再帰削除のように、ロックされたファイルの手前まで消すことがない。
                        Directory.Move(localDir, previousDir);
                    }
                    catch (IOException ex)
                    {
                        // 前の WebView2 がまだ拡張機能を使っている場合がある。
                        // そのときは古いミラーを残して起動し、次回起動で更新を再試行する。
                        Debug.WriteLine($"[Extensions] Existing packaged mirror is in use: {ex.Message}");
                        AppLog.Debug($"Packaged extension mirror update deferred because it is in use: {ex.Message}");
                        previousDir = null;
                        return;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Debug.WriteLine($"[Extensions] Existing packaged mirror is not writable: {ex.Message}");
                        AppLog.Debug($"Packaged extension mirror update deferred because it is not writable: {ex.Message}");
                        previousDir = null;
                        return;
                    }
                }

                try
                {
                    Directory.Move(stagingDir, localDir);
                }
                catch
                {
                    // 新しいミラーの配置に失敗した場合は、退避した古いミラーを元の
                    // 名前へ戻して、次回起動でも拡張機能を使えるようにする。
                    if (previousDir is not null && Directory.Exists(previousDir) && !Directory.Exists(localDir))
                    {
                        try { Directory.Move(previousDir, localDir); }
                        catch { }
                    }
                    throw;
                }
                _preparedPackagedExtensionsFingerprint = fingerprint;
            }
            catch (Exception ex)
            {
                // 拡張機能の更新失敗でタイムライン全体を止めない。既存ミラーが有効なら
                // LoadExtensionsAsync がそれを読み込み、無ければ拡張なしで続行する。
                Debug.WriteLine($"[Extensions] Could not prepare packaged extensions: {ex}");
                AppLog.Debug($"Packaged extension preparation failed: {ex.Message}");
            }
            finally
            {
                // 入れ替えに成功した場合は stagingDir がもう存在しない。
                try
                {
                    if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true);
                }
                catch { }
            }
        }

        private static bool IsPackagedExtensionsReady(
            string sourceDir,
            string localDir,
            string markerPath,
            string fingerprint)
        {
            try
            {
                if (!Directory.Exists(localDir) || !File.Exists(markerPath)) return false;
                if (!string.Equals(File.ReadAllText(markerPath).Trim(), fingerprint,
                                   StringComparison.OrdinalIgnoreCase)) return false;

                // マーカーだけが残った壊れたミラーを「準備済み」と扱わない。
                foreach (var sourceExtension in Directory.GetDirectories(sourceDir))
                {
                    if ((File.GetAttributes(sourceExtension) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
                    var manifest = Path.Combine(localDir, Path.GetFileName(sourceExtension), "manifest.json");
                    if (!File.Exists(manifest)) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeExtensionsFingerprint(string sourceDir)
        {
            using var data = new MemoryStream();
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                         .Where(path => (File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) == 0)
                         .OrderBy(path => Path.GetRelativePath(sourceDir, path), StringComparer.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
                var nameBytes = Encoding.UTF8.GetBytes(relative);
                data.Write(nameBytes, 0, nameBytes.Length);
                data.WriteByte(0);
                using var input = File.OpenRead(file);
                input.CopyTo(data);
                data.WriteByte(0);
            }
            return Convert.ToHexString(SHA256.HashData(data.ToArray()));
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
            {
                if ((File.GetAttributes(file) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var dir in Directory.GetDirectories(src))
            {
                if ((File.GetAttributes(dir) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
            }
        }

        private AppSettings _appSettings = new();
        private bool _bossModeActive;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private readonly List<TimelineConfig> _configs = [];
        /// <summary>
        /// 表示順のペイン一覧。以前は_webViews や _autoLoadIndicators を
        /// 「全ペインの代用」にして列挙していた（#345）。
        /// </summary>
        private IEnumerable<TimelinePane> Panes
        {
            get
            {
                var existing = GetAllPanes();
                var ordered = _configs
                    .Select(config => existing.FirstOrDefault(p => ReferenceEquals(p.Config, config)))
                    .Where(p => p is not null)
                    .Cast<TimelinePane>()
                    .ToList();
                ordered.AddRange(existing.Where(p => !ordered.Contains(p)));
                return ordered;
            }
        }

        /// <summary>WebView2 からそのペインを引く。ペインは多くても数枚なので線形探索で十分。</summary>
        private TimelinePane? PaneOf(WebView2 webView) => Panes.FirstOrDefault(p => p.WebView == webView);

        private TimelinePane? _draggingPane;
        private readonly TimelinePresentationState<TimelinePane> _presentation = new();
        private TimelinePane? _focusedPane { get => _presentation.FocusedPane; set => _presentation.FocusedPane = value; }
        private bool _focusModeActive { get => _presentation.FocusActive; set => _presentation.FocusActive = value; }
        private string _layoutModeBeforeFocus { get => _presentation.ModeBeforeFocus ?? "Classic"; set => _presentation.ModeBeforeFocus = value; }
        // ペイン → ヘッダーの配色を再適用する処理。
        // 以前は List<Action> だったが、除去が参照一致になるため
        // デリゲート実体を持たない削除経路からは掃除できなかった（#362）。
        // WebView2 の拡張機能はプロファイルごとに保存されるため、
        // 「アプリ全体で一度だけ」ではなく、各プロファイルで一度だけ初期化する。
        private readonly HashSet<string> _extensionsLoadedProfiles = [];
        private readonly Dictionary<string, Task> _extensionLoadTasks = [];
        private readonly List<ExtensionInfo> _loadedExtensions = [];
        // 環境そのものではなく「生成中の Task」をキャッシュする（#339）。
        // TryGetValue と await の間に隙間があると、同一プロファイルのペインを並行復元した
        // ときに同じ user data folder に対して CreateWithOptionsAsync が重複しうるため。
        private readonly Dictionary<string, Task<CoreWebView2Environment>> _profileEnvs = [];
        private List<ProfileConfig> _profiles = [];
        // cfg.Url の変更をヘッダー（URL ラベル・種別アイコン・ホーム判定）へ反映する更新子 (#211)
        private readonly Dictionary<WebView2, DispatcherTimer>  _hardReloadTimers    = [];
        private readonly Dictionary<WebView2, DateTimeOffset>   _hardReloadStartTimes = [];
        private readonly Dictionary<WebView2, Action>           _hardReloadUiUpdaters = [];
        private readonly HashSet<WebView2>                       _pointerOverWebViews  = [];
        private readonly HashSet<WebView2>                       _urlDivergedWebViews  = [];
        private DispatcherTimer?  _hardReloadUiTimer;

        // ホーム自動更新（#207）のヘッダーインジケーター（ペイン → アイコン/ツールチップ）


        // タイムライン番号バッジ（#225）。ペイン → 番号 TextBlock。表示順で 1..9 を振り直す。


        // 編集中（リプライ/引用）の WebView 集合（#258）。いずれかが編集中ならホーム自動更新を止める。
        private readonly HashSet<WebView2> _composingWebViews = [];

        // headerGrid → pane の対応（#227）。アクティブな headerGrid からペインを引くのに使う。

        // 画像表示中のペインの一時拡大（試験機能 #287）。ペイン → 元の TimelineConfig（幅の復元用）。

        private TimelinePane? _enlargedPane { get => _presentation.EnlargedPane; set => _presentation.EnlargedPane = value; }

        // キーボードショートカット処理スクリプト（各 WebView2 に注入）
        private static readonly string KeyboardShortcutScript = """
            (function() {
                if (window._xtvKb) return;
                window._xtvKb = true;

                function addStyle() {
                    if (document.getElementById('xtv-kb-style')) return;
                    var s = document.createElement('style');
                    s.id = 'xtv-kb-style';
                    s.textContent = '.xtv-focused-post{outline:2px solid #0078D4!important;outline-offset:-2px!important;border-radius:4px!important;}';
                    (document.head || document.documentElement).appendChild(s);
                }
                document.readyState === 'loading'
                    ? document.addEventListener('DOMContentLoaded', addStyle)
                    : addStyle();

                var fi = -1;
                var getPosts = () => [...document.querySelectorAll('article[data-testid="tweet"]')];
                var isEdit   = () => { var el = document.activeElement; return el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable); };

                function navigatePosts(d) {
                    var ps = getPosts();
                    if (!ps.length) return;
                    ps.forEach(a => a.classList.remove('xtv-focused-post'));
                    fi = fi < 0 ? (d > 0 ? 0 : ps.length - 1)
                                : Math.max(0, Math.min(ps.length - 1, fi + d));
                    ps[fi]?.classList.add('xtv-focused-post');
                    ps[fi]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }

                function actOnPost(id, alt) {
                    var ps = getPosts();
                    if (!ps.length) return;
                    var idx = fi;
                    if (idx < 0 || idx >= ps.length) {
                        // タイムラインでは Ctrl+↑/↓ での選択が必要（案C）。
                        // ただし個別ツイート（/status/）ページでは未選択でも主ツイート（先頭）に作用する（#254）。
                        if (/\/status\/\d+/.test(location.pathname)) idx = 0;
                        else return;
                    }
                    var b = ps[idx].querySelector('[data-testid="' + id + '"]' + (alt ? ',[data-testid="' + alt + '"]' : ''));
                    b?.click();
                }

                document.addEventListener('keydown', e => {
                    var c = e.ctrlKey, s = e.shiftKey, a = e.altKey, k = e.key, ni = !isEdit();
                    if (c && !s && !a) {
                        if (k === 'ArrowRight') { e.preventDefault(); window.chrome.webview.postMessage('focusNext'); return; }
                        if (k === 'ArrowLeft')  { e.preventDefault(); window.chrome.webview.postMessage('focusPrev'); return; }
                        if (k === 'n')          { e.preventDefault(); window.chrome.webview.postMessage('newPost');   return; }
                        if (k === 'ArrowUp')    { e.preventDefault(); navigatePosts(-1); return; }
                        if (k === 'ArrowDown')  { e.preventDefault(); navigatePosts(1);  return; }
                        if (k === 'f')          { e.preventDefault(); window.chrome.webview.postMessage('focusSearch'); return; }
                        if (k === 'k')          { e.preventDefault(); window.chrome.webview.postMessage('commandPalette'); return; }
                        if (k >= '1' && k <= '9') { e.preventDefault(); window.chrome.webview.postMessage('focusIndex:' + k); return; } // #225

                        if (k === 'r' && ni)    { e.preventDefault(); actOnPost('retweet',  'unretweet');      return; }
                        if (k === 'b' && ni)    { e.preventDefault(); actOnPost('bookmark', 'removeBookmark'); return; }
                        if (k === 'l' && ni)    { e.preventDefault(); actOnPost('like',     'unlike');         return; }
                    }
                    // Ctrl+Shift+←/→ でペインを左右へ並べ替え（#344）。
                    // 入力中は単語単位の選択を奪わないよう ni で除外する。
                    if (c && s && !a && ni) {
                        if (k === 'ArrowRight') { e.preventDefault(); window.chrome.webview.postMessage('movePaneNext'); return; }
                        if (k === 'ArrowLeft')  { e.preventDefault(); window.chrome.webview.postMessage('movePanePrev'); return; }
                    }
                    // Alt+Shift+矢印で、WebView2 にフォーカスがある時も列・行の境界を調整する。
                    if (!c && s && a && ni) {
                        if (k === 'ArrowLeft')  { e.preventDefault(); window.chrome.webview.postMessage('resizePaneLeft'); return; }
                        if (k === 'ArrowRight') { e.preventDefault(); window.chrome.webview.postMessage('resizePaneRight'); return; }
                        if (k === 'ArrowUp')    { e.preventDefault(); window.chrome.webview.postMessage('resizePaneUp'); return; }
                        if (k === 'ArrowDown')  { e.preventDefault(); window.chrome.webview.postMessage('resizePaneDown'); return; }
                    }
                    if (!c && !s && !a) {
                        if (k === 'F3')              { e.preventDefault(); window.chrome.webview.postMessage('focusSearch'); return; } // #228
                        if (k === 'F1')              { e.preventDefault(); window.chrome.webview.postMessage('showShortcuts'); return; }
                        if (k === 'Home'      && ni) { window.scrollTo({ top: 0, behavior: 'smooth' }); return; }
                        if (k === 'End'       && ni) { window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'smooth' }); return; }
                        if (k === 'F5')              { e.preventDefault(); location.reload(); return; }
                        if (k === 'Backspace' && ni) { e.preventDefault(); history.back(); return; }
                    }
                }, true);

                // マウスホイールでスクロールしたら、そのペインをアクティブ化する (#221)。
                // ホイールはキーフォーカスを移さないため、Home/End 等が別ペインに効いてしまうのを防ぐ。
                // 既にフォーカスがある（hasFocus）ときは何もしない。連打防止に 200ms スロットル。
                var lastAct = 0;
                document.addEventListener('wheel', function () {
                    var now = Date.now();
                    if (now - lastAct < 200) return;
                    lastAct = now;
                    if (!document.hasFocus()) window.chrome.webview.postMessage('activate');
                }, { passive: true, capture: true });

                // Shift+ホイールでペインを横スクロールする（#371）。
                // ヘッダーや余白の上では WinUI の ScrollViewer が縦ホイールを
                // 自動で横へ回すが、ここは WebView2 なので届かない。
                //
                // 上のリスナーは passive なので preventDefault が効かない。別に登録する。
                // 非 passive はブラウザーのスクロール高速パスを外すので、
                // Shift が無いときは最初の 1 行で抜けること。
                document.addEventListener('wheel', function (e) {
                    if (!e.shiftKey || e.ctrlKey || e.altKey) return;
                    // X 側に横方向のオーバーフローがある画面で、
                    // ページが横に動いてしまうのを防ぐ。
                    e.preventDefault();
                    var d = e.deltaY || e.deltaX;
                    if (d) window.chrome.webview.postMessage('scrollPanes:' + d);
                }, { passive: false, capture: true });
            })();
            """;

        private static readonly string TimestampInterceptScript = """
            (function() {
                if (window._xtvTimestamp) return;
                window._xtvTimestamp = true;
                document.addEventListener('click', function(e) {
                    if (!window._xtvOpenTimestampInBrowser) return;
                    var a = e.target.closest('a[href]');
                    if (!a || !a.querySelector('time')) return;
                    try {
                        var url = new URL(a.href);
                        if (/\/status\/\d+/.test(url.pathname)) {
                            e.preventDefault();
                            e.stopImmediatePropagation();
                            window.chrome.webview.postMessage('openTimestamp:' + url.href);
                        }
                    } catch(ex) {}
                }, true);
            })();
            """;

        // プロファイルデータの保存先は ProfileService に共通化済み (#157)
        private static string GetProfilesDataDir() => ProfileService.GetProfilesDataDir();

        private Task<CoreWebView2Environment> GetOrCreateProfileEnvAsync(string profileId)
        {
            // 生成 Task を先に登録してから await させることで、並行呼び出しでも生成は 1 回になる。
            // UI スレッドから呼ばれる前提なので Dictionary のままでよい。
            if (_profileEnvs.TryGetValue(profileId, out var cached)) return cached;

            var task = CreateProfileEnvAsync(profileId);
            _profileEnvs[profileId] = task;

            // 失敗した Task を残すと以後ずっと同じ例外を返すため、キャッシュから外して再試行できるようにする。
            _ = task.ContinueWith(
                t =>
                {
                    if (_profileEnvs.TryGetValue(profileId, out var current) && current == t)
                        _profileEnvs.Remove(profileId);
                    LogError($"CreateProfileEnv (profileId={profileId})", t.Exception!);
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.FromCurrentSynchronizationContext());

            return task;
        }

        private static async Task<CoreWebView2Environment> CreateProfileEnvAsync(string profileId)
        {
            var userDataFolder = profileId == "default"
                ? ""
                : Path.Combine(GetProfilesDataDir(), profileId);
            if (userDataFolder.Length > 0)
                Directory.CreateDirectory(userDataFolder);
            var options = new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true };
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                "", userDataFolder, options);
            Debug.WriteLine($"[Profile] Env created: profileId={profileId}, UserDataFolder={env.UserDataFolder}");
            return env;
        }

        public MainWindow()
        {
            this.InitializeComponent();
            InitializeBossMode();
            WindowSizingService.ResizeAndCenter(this, 1400, 900, 480, 400);
            // ツールバーが重なるほど狭くできないよう下限を引く（#342）
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth  = (int)Math.Round(480 * WindowSizingService.GetScale(this));
                presenter.PreferredMinimumHeight = (int)Math.Round(400 * WindowSizingService.GetScale(this));
            }
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
            Title = R.Get("App_Title");
            RefreshUIText();
            HookContrastThemeChanges();  // コントラストテーマの切り替えに追従（#341）
            // 終了時の保存は Closing 側で行う（#338）。
            // Closed は async void 相当で、await の後を待たずにプロセスが終了しうるため、
            // 直前の変更（ペインの追加・並べ替えなど）が取りこぼされることがあった。
            // ここでは一度クローズをキャンセルして保存を待ち、完了後に閉じ直す。
            AppWindow.Closing += async (sender, args) =>
            {
                if (_closeHandled) return;   // 保存後の閉じ直しでは素通しする
                args.Cancel = true;

                _closeHandled = true;
                try
                {
                    await SaveTimelinesAsync();
                }
                catch (Exception ex)
                {
                    LogError("AppWindow.Closing (save)", ex);
                }
                Close();
            };

            Closed += (s, e) =>
            {
                _hardReloadUiTimer?.Stop();
                DisposeComposeWarm();  // 投稿プリロードの後始末（#244 案B）
                foreach (var wv in Panes.Select(p => p.WebView).ToList())
                    CleanupWebView(wv);
            };
            ((FrameworkElement)Content).ActualThemeChanged += (s, e) =>
            {
                ApplySavedTheme();
                ApplyThemeToWebViews();
            };
            LoadSettings();
            InitializeBossMode();
            LoadProfiles();
            LoadWorkspaces();
            // バックアップ復元で再び必要になるログイン用フォルダーを起動時に削除しない。
            ApplySavedTheme();
            UpdateMenuUpdateBadge();
            InitializeAsync().FireAndForget(nameof(InitializeAsync));
            CheckForUpdatesInBackgroundAsync().FireAndForget(nameof(CheckForUpdatesInBackgroundAsync));
        }

        // ツールバー・メニューなど常駐 UI の静的テキストを現在の言語で再適用する。
        // 起動時のほか、言語切り替え後（#117）にも呼ばれる。
        private void RefreshUIText()
        {
            PostLabel.Text        = R.Get("PostLabel.Text");
            Title                 = R.Get("App_Title");
            DropHintTitle.Text    = R.Get("Empty_WelcomeTitle");
            DropHintSubtitle.Text = R.Get("Empty_WelcomeBody");
            EmptyAddProfileBtn.Content = R.Get("Empty_AddProfile");
            EmptyAddTimelineBtn.Content = R.Get("Empty_AddTimeline");
            EmptyDisabledHint.Text = R.Get("Empty_DisabledHint");
            EmptyDisabledHint.Visibility = ViewModel.HasNamedProfiles ? Visibility.Collapsed : Visibility.Visible;
            EmptyQuickAddLabel.Text = R.Get("Empty_QuickAdd");
            EmptyDropHintText.Text = R.Get("Empty_DropHint");
            EmptyQuickHomeBtn.Content = R.Get("Timeline_Home");
            EmptyQuickNotificationsBtn.Content = R.Get("Timeline_Notifications");
            EmptyQuickBookmarksBtn.Content = R.Get("Timeline_Bookmarks");
            EmptyQuickListsBtn.Content = R.Get("Timeline_Lists");
            AddTimelineToolbarLabel.Text = R.Get("Toolbar_AddTimeline");
            ToolTipService.SetToolTip(AddTimelineToolbarBtn, R.Get("Toolbar_AddTimelineTooltip"));
            AutomationProperties.SetName(AddTimelineToolbarBtn, R.Get("Toolbar_AddTimelineTooltip"));
            ToolTipService.SetToolTip(ToolbarProfileCombo, R.Get("Toolbar_Profile"));
            AutomationProperties.SetName(ToolbarProfileCombo, R.Get("Toolbar_Profile"));
            ToolTipService.SetToolTip(PostBtn,    R.Get("PostBtn_Tooltip"));
            ToolTipService.SetToolTip(AppMenuBtn, R.Get("AppMenu_Tooltip"));
            AutomationProperties.SetName(PostBtn,    R.Get("PostBtn_Tooltip"));
            AutomationProperties.SetName(AppMenuBtn, R.Get("AppMenu_Tooltip"));
            LayoutClassicItem.Text = R.Get("Layout_Classic");
            LayoutAutoItem.Text = R.Get("Layout_Auto");
            LayoutGrid2x2Item.Text = R.Get("Layout_Grid2x2");
            LayoutGrid2x3Item.Text = R.Get("Layout_Grid2x3");
            LayoutVerticalSplitItem.Text = R.Get("Layout_VerticalSplit");
            LayoutFocusItem.Text = R.Get("Layout_Focus");
            ToolTipService.SetToolTip(LayoutBtn, R.Get("Layout_Tooltip"));
            AutomationProperties.SetName(LayoutBtn, R.Get("Layout_Tooltip"));
            ToolTipService.SetToolTip(AutoArrangeBtn, R.Get("Layout_Auto_Tooltip"));
            AutomationProperties.SetName(AutoArrangeBtn, R.Get("Layout_Auto_Tooltip"));
            ToolTipService.SetToolTip(AutoPagePreviousBtn, R.Get("Layout_PagePrevious"));
            ToolTipService.SetToolTip(AutoPageNextBtn, R.Get("Layout_PageNext"));
            AutomationProperties.SetName(AutoPagePreviousBtn, R.Get("Layout_PagePrevious"));
            AutomationProperties.SetName(AutoPageNextBtn, R.Get("Layout_PageNext"));
            var bossModeTip = R.Get("BossMode_Tooltip");
            ToolTipService.SetToolTip(BossModeBtn, bossModeTip);
            AutomationProperties.SetName(BossModeBtn, bossModeTip);
            BossModeCloseBtn.Content = R.Get("BossMode_Close");
            AutomationProperties.SetName(BossModeCloseBtn, R.Get("BossMode_Close"));
            BossModeEmptyText.Text = R.Get("BossMode_NoImage");
            ExitFocusModeLabel.Text = R.Get("FocusMode_Exit");
            ToolTipService.SetToolTip(ExitFocusModeBtn, R.Get("FocusMode_Exit"));
            AutomationProperties.SetName(ExitFocusModeBtn, R.Get("FocusMode_Exit"));
            LayoutSafetyBar.Message = R.Get("Layout_CapacityFallback");
            UpdateLayoutMenuState();
            RefreshTemporaryVisibilityUi();
            ThemeSubMenu.Text       = R.Get("Menu_Theme");
            ThemeSystemItem.Text    = R.Get("Theme_System");
            ThemeLightItem.Text     = R.Get("Theme_Light");
            ThemeDarkItem.Text      = R.Get("Theme_Dark");
            ThemeCyberpunkItem.Text = R.Get("Theme_Cyberpunk");
            ThemeNeonContrastItem.Text = R.Get("Theme_NeonContrast");
            ThemeOceanItem.Text     = R.Get("Theme_Ocean");
            ThemeForestItem.Text    = R.Get("Theme_Forest");
            ThemeSakuraItem.Text    = R.Get("Theme_Sakura");
            UpdateThemeRadioState();
            AppSettingsMenuItem.Text = R.Get("Menu_Settings");

            NewProfileMenuItem.Text           = R.Get("Menu_NewProfile");
            AddTimelineSubMenu.Text           = R.Get("Menu_AddTimeline");
            AddHomeTimelineItem.Text          = R.Get("Timeline_Home");
            AddNotificationsTimelineItem.Text = R.Get("Timeline_Notifications");
            AddBookmarksTimelineItem.Text     = R.Get("Timeline_Bookmarks");
            AddListsTimelineItem.Text         = R.Get("Timeline_Lists");
            TimelineManagerMenuItem.Text      = R.Get("Menu_TimelineManager");
            WorkspacesMenuItem.Text           = R.Get("Menu_Workspaces");
            CommandPaletteMenuItem.Text       = R.Get("Menu_CommandPalette");
            ShortcutsMenuItem.Text            = R.Get("Menu_Shortcuts");
            // アイコンは既存ペインと同じく URL 種別から導出して一貫性を保つ
            AddHomeIcon.Glyph          = UrlHelper.GetTimelineGlyph(HomeTimelineUrl);
            AddNotificationsIcon.Glyph = UrlHelper.GetTimelineGlyph(NotificationsTimelineUrl);
            AddBookmarksIcon.Glyph     = UrlHelper.GetTimelineGlyph(BookmarksTimelineUrl);
            // リスト URL はハンドル依存のため、アイコン導出には代表的な一覧パスを使う
            AddListsIcon.Glyph         = UrlHelper.GetTimelineGlyph(BuildListsUrl("_"));

            SearchBox.PlaceholderText = R.Get("Search_Placeholder");
            ToolTipService.SetToolTip(SearchBox, R.Get("Search_Tooltip"));
            AutomationProperties.SetName(SearchBox, R.Get("Search_Tooltip"));
            SearchPanelTitle.Text = R.Get("Search_PanelTitle");
            SearchPanelHint.Text = R.Get("Search_PanelHint");
            SearchPinBtn.Content = R.Get("Search_AddBtn");
            ToolTipService.SetToolTip(SearchCloseBtn, R.Get("Button_Close"));
            RefreshToolbarProfiles();
            RefreshWorkspaceTabs();
            foreach (var pane in Panes) pane.RefreshLocalizedText();
        }

        private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dlg)
        {
            // すべてのダイアログに現在のテーマを自動適用して設定漏れを防ぐ (#126)
            dlg.RequestedTheme = ((FrameworkElement)Content).ActualTheme;
            return await dlg.ShowAsync();
        }

        /// <summary>
        /// 外部ブラウザー設定に応じて URI を開く。
        /// Edge プロファイル指定が有効かつ http/https の場合は Edge で開き、
        /// それ以外はシステム既定に委ねる。
        /// </summary>
        private async Task LaunchUriByEdgeProfileAsync(Uri uri)
        {
            // WebView から渡される URI で file: / javascript: / 独自プロトコルを起動しない。
            if (!UrlHelper.IsSafeExternalUri(uri))
            {
                LogDebug($"Blocked non-web external URI: {uri.Scheme}");
                return;
            }

            if (_appSettings.ExternalBrowser == "edge" &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                var edgePath = EdgeService.FindEdgePath();
                if (edgePath is not null)
                {
                    EdgeService.LaunchInProfile(edgePath, _appSettings.EdgeProfileDirectory, uri);
                    return;
                }
                // Edge が見つからない場合はシステム既定にフォールバック
                Debug.WriteLine("[Edge] Falling back to system default — Edge not found");
            }

            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }
}
