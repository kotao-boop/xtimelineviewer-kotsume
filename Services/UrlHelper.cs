using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XTimelineViewer.Services
{
    /// <summary>URL 判定・解析の純粋ロジック（UI 非依存）。</summary>
    internal static class UrlHelper
    {
        /// <summary>
        /// HTTPS の X / Twitter 本体 URL だけを許可する。
        /// 文字列の部分一致にすると、たとえば https://example.test/?next=x.com まで
        /// 信頼してしまうため、必ず Uri が解析したホスト名を比較する。
        /// </summary>
        internal static bool IsXUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsXUri(uri);

        internal static bool IsXUri(Uri? uri) =>
            uri is not null &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            uri.IsDefaultPort &&
            (string.Equals(uri.Host, "x.com", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "www.x.com", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "twitter.com", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "www.twitter.com", StringComparison.OrdinalIgnoreCase));

        /// <summary>外部ブラウザーへ渡してよい通常の Web URL。</summary>
        internal static bool IsSafeExternalUri(Uri? uri) =>
            uri is not null &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// X のサインイン画面が別ウィンドウで開くことを許可する認証先。
        ///
        /// ログイン用 WebView2 にはアドレスバーがないため、任意の HTTPS ページを
        /// ポップアップ内へ開くとフィッシング画面を見分けにくい。X 本体と、X が
        /// 提供する Google / Apple サインインの正規ホストだけを完全一致で許可する。
        /// </summary>
        internal static bool IsTrustedSignInPopupUri(string? value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort)
                return false;

            return uri.Host.ToLowerInvariant() switch
            {
                "x.com"                 => true,
                "www.x.com"             => true,
                "twitter.com"           => true,
                "www.twitter.com"       => true,
                "accounts.google.com"   => true,
                "appleid.apple.com"     => true,
                _                       => false,
            };
        }

        /// <summary>同じ HTTPS オリジン（scheme / host / port）かを確認する。</summary>
        internal static bool IsSameHttpsOrigin(string? candidateUrl, string? trustedUrl)
        {
            if (!Uri.TryCreate(candidateUrl, UriKind.Absolute, out var candidate) ||
                !Uri.TryCreate(trustedUrl, UriKind.Absolute, out var trusted))
                return false;

            return string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(trusted.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                   candidate.Port == trusted.Port &&
                   string.Equals(candidate.Host, trusted.Host, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOnBaseUrl(string currentUrl, string baseUrl)
        {
            if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out var cur))  return false;
            if (!Uri.TryCreate(baseUrl,    UriKind.Absolute, out var @base)) return false;
            return string.Equals(cur.Host,         @base.Host,         StringComparison.OrdinalIgnoreCase)
                && string.Equals(cur.AbsolutePath, @base.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// アカウント依存のリスト一覧 URL（https://x.com/&lt;handle&gt;/lists）かどうか。
        /// プロファイル切り替え時にハンドルを差し替える対象の判定に使う。
        /// </summary>
        internal static bool IsPerUserListsUrl(string url)
            => Uri.TryCreate(url, UriKind.Absolute, out var u)
               && Regex.IsMatch(u.AbsolutePath, @"^/[^/]+/lists$");

        /// <summary>
        /// ホームタイムラインかどうか。ホーム自動更新（#207）の対象判定に使う。
        /// 以前は MainWindow.IsHomeConfig / ヘッダー更新子 / アイコンの初期表示の
        /// 3 か所に同じ式が書かれていた（#345）。
        /// </summary>
        internal static bool IsHomeUrl(string url)
            => Uri.TryCreate(url, UriKind.Absolute, out var u)
               && u.AbsolutePath.StartsWith("/home", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 個別ツイートの画像表示 URL（https://x.com/&lt;handle&gt;/status/&lt;id&gt;/photo/&lt;n&gt;）かどうか。
        /// クリックでライトボックスが開いた状態を検知し、ペインの一時拡大（#287）のトリガーに使う。
        /// </summary>
        internal static bool IsMediaPhotoUrl(string url)
            => Uri.TryCreate(url, UriKind.Absolute, out var u)
               && Regex.IsMatch(u.AbsolutePath, @"^/[^/]+/status/\d+/photo/\d+/?$");

        // グリフは Segoe Fluent Icons の私用領域(PUA)コードポイント。
        // 生の PUA 文字を直書きするとエンコーディング事故で欠落するため (#122)、
        // 必ず "\uXXXX" エスケープ表記で記述すること。
        internal static string GetTimelineGlyph(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "";
            var p = uri.AbsolutePath;
            if (p.StartsWith("/home"))                                return "\uE80F"; // Home
            if (p.StartsWith("/notifications"))                       return "\uE7E7"; // Bell
            if (p.StartsWith("/search") || p.StartsWith("/explore")) return "\uE71E"; // Search
            // X の再編でブックマークは「履歴」ページ配下のタブになり、/i/bookmarks は
            // /i/history にリダイレクトされる (#329)。両方をブックマーク扱いにする。
            if (p == "/bookmarks" || p.StartsWith("/bookmarks/") ||
                p == "/i/bookmarks" || p.StartsWith("/i/bookmarks/") ||
                p == "/i/history" || p.StartsWith("/i/history/")) return "\uE734"; // Bookmark
            if (p == "/i/lists" || p.StartsWith("/i/lists/") ||
                Regex.IsMatch(p, @"^/[^/]+/lists$"))                  return "\uE71D"; // BulletedList (lists index / individual)
            if (p.StartsWith("/messages"))                            return "\uE8BD"; // Chat
            if (Regex.IsMatch(p, @"^/[^/]+$"))                        return "\uE77B"; // Contact
            return "\uE774"; // Globe
        }

        private static bool IsProfilePath(string p) =>
            Regex.IsMatch(p, @"^/[A-Za-z0-9_]+$") &&
            !p.StartsWith("/home") && !p.StartsWith("/notifications") &&
            !p.StartsWith("/search") && !p.StartsWith("/explore") &&
            !p.StartsWith("/bookmarks") && !p.StartsWith("/messages") &&
            !p.StartsWith("/i/");

        internal static bool IsListHeaderApplicable(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            var p = uri.AbsolutePath;
            return p.StartsWith("/notifications") ||
                   p.StartsWith("/search")        ||
                   p.StartsWith("/explore")       ||
                   p == "/bookmarks" || p.StartsWith("/bookmarks/") ||
                   p == "/i/bookmarks" || p.StartsWith("/i/bookmarks/") ||
                   p == "/i/history" || p.StartsWith("/i/history/") ||
                   p.StartsWith("/i/lists/")      ||
                   IsProfilePath(p);
        }

        /// <summary>.url ショートカットファイルの行から URL を抽出する。</summary>
        internal static string? ParseUrlShortcut(IEnumerable<string> lines)
        {
            foreach (var line in lines)
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    return line[4..].Trim();
            return null;
        }
    }
}
