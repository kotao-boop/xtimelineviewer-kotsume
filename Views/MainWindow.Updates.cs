using Microsoft.UI.Xaml;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace XTimelineViewer.Views
{
    public sealed partial class MainWindow : Window
    {
        // ── Update check ─────────────────────────────────────────────────────

        /// <summary>
        /// アプリ起動後にアイドルで更新チェックを行う。
        /// GitHub Releasesで確認し、MSIX (Store) 版ではスキップする。
        /// </summary>
        private async Task CheckForUpdatesInBackgroundAsync()
        {
            await Task.Delay(5000);

            // MSIX 版は Store / Windows Update の自動更新に任せる
            if (PackageContext.IsPackaged) return;

            // PowerToys にならい、24 時間ごとに確認する。失敗した場合は 2 時間後に再試行する。
            // xTV は起動しっぱなしで使うため、起動時 1 回だけでは数日間更新に気づけない (#328)。
            while (true)
            {
                var ok = await TryRefreshLatestVersionAsync();
                await Task.Delay(ok ? UpdateCheckInterval : UpdateRetryInterval);
            }
        }

        private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
        private static readonly TimeSpan UpdateRetryInterval = TimeSpan.FromHours(2);

        /// <summary>
        /// 最新バージョンを取得してバッジ表示へ反映する。取得できたら true。
        /// </summary>
        private async Task<bool> TryRefreshLatestVersionAsync()
        {
            try
            {
                var latest = await FetchLatestVersionAsync();
                if (latest is null) return false;

                var current = Assembly.GetExecutingAssembly().GetName().Version!;
                _appSettings.CachedLatestVersion = latest > current
                    ? $"v{latest.ToString(3)}"
                    : null;
                SaveSettings();
                UpdateMenuUpdateBadge();
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 最新バージョンをKotsume EditionのGitHub Releasesから取得する。
        /// Kotsume Editionのwingetパッケージはまだ存在しないため、派生元の
        /// daruyanagi.XTimelineViewerを誤って更新しないようwingetは呼び出さない。
        /// </summary>
        internal static async Task<Version?> FetchLatestVersionAsync()
        {
            return await FetchGitHubLatestVersionAsync();
        }

        /// <summary>
        /// GitHub Releases の最新タグ（v2.0.0 形式）からバージョンを取得する。
        /// 失敗時は null（ネットワーク不通、レート制限、パース失敗など）。
        /// </summary>
        private static async Task<Version?> FetchGitHubLatestVersionAsync()
        {
            try
            {
                using var req = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get,
                    "https://api.github.com/repos/kotao-boop/xtimelineviewer-kotsume/releases/latest");
                // GitHub API は User-Agent 必須
                req.Headers.TryAddWithoutValidation("User-Agent", "XTimelineViewer");
                req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");

                using var resp = await _updateHttp.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                using var doc = System.Text.Json.JsonDocument.Parse(
                    await resp.Content.ReadAsStreamAsync());
                if (!doc.RootElement.TryGetProperty("tag_name", out var tag)) return null;

                var text = tag.GetString()?.TrimStart('v', 'V');
                return Version.TryParse(text, out var v) ? v : null;
            }
            catch { return null; }
        }

        private static readonly System.Net.Http.HttpClient _updateHttp =
            new() { Timeout = TimeSpan.FromSeconds(15) };

        private void UpdateMenuUpdateBadge()
        {
            UpdateBadgeDot.Visibility = _appSettings.CachedLatestVersion is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
