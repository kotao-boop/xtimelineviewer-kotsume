using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
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

            LoginWebView.Source = new Uri("https://x.com/i/flow/login");
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
