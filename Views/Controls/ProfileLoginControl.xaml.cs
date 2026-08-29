using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XTimelineViewer.Models;
using XTimelineViewer.Services;

namespace XTimelineViewer.Views.Controls
{
    /// <summary>
    /// X にログインして ProfileConfig を生成する、再利用可能なログインフロー (#157)。
    /// 新規プロファイル作成（AddProfileWindow）とオンボーディング（#4）で共有する。
    /// ボタン類はホスト側に持たせ、本コントロールはログイン～名前入力のみを担う。
    /// </summary>
    public sealed partial class ProfileLoginControl : UserControl
    {
        private string _profileId = Guid.NewGuid().ToString("N");
        private string? _existingName;

        // ログイン時に検出した X のスクリーンネーム。Name はユーザーが編集できるため別に保持する。
        private string? _detectedScreenName;
        private bool _initialized;
        private CoreWebView2Environment? _environment;
        private readonly HashSet<Window> _signInWindows = [];

        /// <summary>このコントロールが扱う新規プロファイルの ID。</summary>
        public string ProfileId => _profileId;

        /// <summary>
        /// ログインが完了し /home に到達したときに発火する。引数は検出したスクリーンネーム（取得できなければ null）。
        /// ホストはこれを受けて「作成」ボタンを表示するなどの反応をする。
        /// </summary>
        public event Action<string?>? LoginDetected;

        public void UseExistingProfile(ProfileConfig profile)
        {
            if (_initialized) throw new InvalidOperationException("The profile must be selected before sign-in starts.");
            _profileId = profile.Id;
            _existingName = profile.Name;
            _detectedScreenName = profile.ScreenName;
        }

        public ProfileLoginControl()
        {
            this.InitializeComponent();
            LoginHintText.Text = R.Get("AddProfile_LoginHint");
            ProfileNameBox.PlaceholderText = R.Get("AddProfile_FallbackLabel");
            AutomationProperties.SetName(ProfileNameBox, R.Get("AddProfile_FallbackLabel"));
            AutomationProperties.SetName(LoginWebView, R.Get("AddProfile_LoginHint"));
            ManualCheckBtn.Content = R.Get("Onboarding_CheckLogin");
        }

        /// <summary>
        /// WebView2 環境を作成し、X のログインページを表示する。
        /// テーマは親から伝播した this.ActualTheme を使う（ホストが RequestedTheme を設定すれば追従）。
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                LoginWebView.Source = new Uri("https://x.com/i/flow/login");
                return;
            }
            var folder = Path.Combine(ProfileService.GetProfilesDataDir(), _profileId);
            Directory.CreateDirectory(folder);
            var options = new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true };
            var env = await CoreWebView2Environment.CreateWithOptionsAsync("", folder, options);
            _environment = env;
            await LoginWebView.EnsureCoreWebView2Async(env);
            _initialized = true;

            LoginWebView.CoreWebView2.Profile.PreferredColorScheme = this.ActualTheme switch
            {
                ElementTheme.Light => CoreWebView2PreferredColorScheme.Light,
                ElementTheme.Dark  => CoreWebView2PreferredColorScheme.Dark,
                _                  => CoreWebView2PreferredColorScheme.Auto,
            };

            LoginWebView.CoreWebView2.NavigationCompleted += async (s, e) =>
            {
                if (!e.IsSuccess) return;
                if (!Uri.TryCreate(LoginWebView.CoreWebView2.Source, UriKind.Absolute, out var uri)) return;
                if (!uri.AbsolutePath.TrimEnd('/').Equals("/home", StringComparison.OrdinalIgnoreCase)) return;

                var screenName = await TryGetScreenNameAsync();
                _detectedScreenName = screenName;
                ShowConfirmPhase(screenName);
                LoginDetected?.Invoke(screenName);
            };

            // Google / Apple サインインは window.open で認証画面を開く。既定動作に任せると
            // 外部ブラウザーへ移り、そちらの Cookie はこのプロファイルへ戻ってこない。
            // 同じ CoreWebView2Environment を使うアプリ内ウィンドウを NewWindow に渡し、
            // 認証結果を元の X ログイン画面と安全に共有する。
            ConfigureSignInWebView(LoginWebView.CoreWebView2);

            LoginWebView.Source = new Uri("https://x.com/i/flow/login");
        }

        private async void LoginWebView_NewWindowRequested(
            CoreWebView2 sender,
            CoreWebView2NewWindowRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();
            Window? popupWindow = null;
            WebView2? popupWebView = null;

            try
            {
                AppLog.Debug($"Sign-in popup requested: {UrlHelper.GetSafeUriForLog(args.Uri)}");

                // アドレスバーのない認証画面へ任意サイトを開かせない。
                if (!UrlHelper.IsTrustedSignInPopupUri(args.Uri))
                {
                    args.Handled = true;
                    AppLog.Debug("Blocked an untrusted sign-in popup.");
                    return;
                }

                if (_environment is null)
                {
                    args.Handled = true;
                    AppLog.Debug("Blocked a sign-in popup because the WebView2 environment was unavailable.");
                    return;
                }

                popupWebView = new WebView2();
                popupWindow = new Window
                {
                    Title = R.Get("Profile_SignInPopupTitle"),
                    Content = popupWebView,
                };

                popupWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(560, 760));
                popupWindow.Activate();
                await popupWebView.EnsureCoreWebView2Async(_environment);

                popupWebView.CoreWebView2.Profile.PreferredColorScheme = this.ActualTheme switch
                {
                    ElementTheme.Light => CoreWebView2PreferredColorScheme.Light,
                    ElementTheme.Dark  => CoreWebView2PreferredColorScheme.Dark,
                    _                  => CoreWebView2PreferredColorScheme.Auto,
                };

                // X → Google/Apple → 追加確認画面のように、認証画面がさらに別画面を
                // 開くことがある。最初の WebView2 だけにイベントを付けると、2段目は
                // 既定動作で外部 Edge へ出てしまい、web_message が元の X へ戻らない。
                // すべての子 WebView2 に同じ処理を再帰的に設定する。
                ConfigureSignInWebView(popupWebView.CoreWebView2);

                // 認証途中のトップレベル遷移も正規ホストだけに限定する。
                popupWebView.CoreWebView2.NavigationStarting += (_, navigationArgs) =>
                {
                    if (UrlHelper.IsTrustedSignInPopupUri(navigationArgs.Uri))
                    {
                        if (Uri.TryCreate(navigationArgs.Uri, UriKind.Absolute, out var uri))
                            popupWindow.Title = $"{R.Get("Profile_SignInPopupTitle")} — {uri.Host}";
                        return;
                    }

                    navigationArgs.Cancel = true;
                    AppLog.Debug($"Blocked an untrusted navigation in the sign-in popup: {UrlHelper.GetSafeUriForLog(navigationArgs.Uri)}");
                };

                popupWebView.CoreWebView2.NavigationCompleted += (core, navigationArgs) =>
                {
                    if (!navigationArgs.IsSuccess)
                        AppLog.Debug($"Sign-in popup navigation failed: {navigationArgs.WebErrorStatus}; {UrlHelper.GetSafeUriForLog(core.Source)}");
                };

                popupWebView.CoreWebView2.WindowCloseRequested += (_, _) => popupWindow.Close();
                popupWindow.Closed += (_, _) =>
                {
                    _signInWindows.Remove(popupWindow);
                    try { popupWebView.Close(); } catch { }
                };

                _signInWindows.Add(popupWindow);
                args.NewWindow = popupWebView.CoreWebView2;
                args.Handled = true;
            }
            catch (Exception ex)
            {
                args.Handled = true;
                AppLog.Error("Profile sign-in popup", ex);
                try { popupWebView?.Close(); } catch { }
                try { popupWindow?.Close(); } catch { }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void ConfigureSignInWebView(CoreWebView2 core)
        {
            core.NewWindowRequested -= LoginWebView_NewWindowRequested;
            core.NewWindowRequested += LoginWebView_NewWindowRequested;

            // HTTPS ではない外部アプリ起動要求（intent: や独自スキームなど）を
            // 認証中に許すと、ブラウザーへ出たまま結果が戻らない。ここでは止め、
            // 個人情報を含まない scheme/host/path だけを診断ログへ残す。
            core.LaunchingExternalUriScheme += (_, args) =>
            {
                args.Cancel = true;
                AppLog.Debug($"Blocked an external sign-in URI scheme: {UrlHelper.GetSafeUriForLog(args.Uri)}");
            };
        }

        private async Task<string?> TryGetScreenNameAsync()
        {
            var result = await LoginWebView.CoreWebView2.ExecuteScriptAsync(
                "document.querySelector('[data-testid=\"AppTabBar_Profile_Link\"]')?.href?.split('/').pop() ?? null");
            return result?.Trim('"') is { Length: > 0 } name && name != "null" ? name : null;
        }

        private void ShowConfirmPhase(string? screenName)
        {
            LoginPhase.Visibility = Visibility.Collapsed;
            ConfirmPhase.Visibility = Visibility.Visible;

            if (screenName is not null)
            {
                DetectedText.Text = string.Format(R.Get("AddProfile_Detected"), $"@{screenName}");
                ProfileNameBox.Text = _existingName ?? screenName;
            }
            else
            {
                DetectedText.Text = "";
            }
        }

        private void ManualCheckBtn_Click(object sender, RoutedEventArgs e)
        {
            if (LoginWebView.CoreWebView2 is null) return;
            LoginWebView.Source = new Uri("https://x.com/home");
        }

        /// <summary>WebView2 を明示的に閉じて環境を解放する。</summary>
        public void CloseWebView()
        {
            foreach (var window in new List<Window>(_signInWindows))
            {
                try { window.Close(); } catch { }
            }
            _signInWindows.Clear();
            try { LoginWebView.Close(); } catch { }
        }

        /// <summary>入力された名前で ProfileConfig を生成する。名前が空なら null を返す。</summary>
        public ProfileConfig? BuildProfile()
        {
            var name = ProfileNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return null;
            return new ProfileConfig { Id = _profileId, Name = name, ScreenName = _detectedScreenName };
        }
    }
}
