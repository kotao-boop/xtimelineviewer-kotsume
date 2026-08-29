using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public class UrlHelperTests
{
    [Theory]
    [InlineData("https://accounts.google.com/o/oauth2/auth?login_hint=user@example.com&code=secret#token", "https://accounts.google.com/o/oauth2/auth")]
    [InlineData("https://x.com/i/flow/login?state=secret", "https://x.com/i/flow/login")]
    [InlineData("intent://accounts.google.com/path#Intent;scheme=https;end", "intent:")]
    [InlineData("not a uri", "(invalid URI)")]
    public void GetSafeUriForLog_RemovesSensitiveQueryAndFragment(string input, string expected)
        => Assert.Equal(expected, UrlHelper.GetSafeUriForLog(input));

    // ── IsXUrl ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://x.com/home",           true)]
    [InlineData("https://twitter.com/home",     true)]
    [InlineData("https://X.COM/home",           true)]
    [InlineData("https://www.x.com/home",       true)]
    [InlineData("https://example.com/",         false)]
    [InlineData("https://nitter.net/home",      false)]
    [InlineData("https://example.com/?next=x.com", false)]
    [InlineData("https://x.com.example.com/home",  false)]
    [InlineData("https://notx.com/home",            false)]
    [InlineData("http://x.com/home",                false)]
    [InlineData("https://x.com:444/home",           false)]
    [InlineData("javascript:alert('x.com')",        false)]
    [InlineData("not-a-url",                        false)]
    public void IsXUrl_Works(string url, bool expected)
        => Assert.Equal(expected, UrlHelper.IsXUrl(url));

    [Theory]
    [InlineData("https://x.com/home", "https://x.com/search?q=a", true)]
    [InlineData("https://X.com/home", "https://x.com/search", true)]
    [InlineData("https://x.com/home", "https://twitter.com/home", false)]
    [InlineData("https://x.com/home", "http://x.com/home", false)]
    [InlineData("https://x.com/home", "https://x.com:444/home", false)]
    [InlineData("https://x.com/home", "not-a-url", false)]
    public void IsSameHttpsOrigin_Works(string trusted, string candidate, bool expected)
        => Assert.Equal(expected, UrlHelper.IsSameHttpsOrigin(candidate, trusted));

    [Theory]
    [InlineData("https://example.com/", true)]
    [InlineData("http://example.com/", true)]
    [InlineData("mailto:test@example.com", false)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("javascript:alert(1)", false)]
    public void IsSafeExternalUri_AllowsOnlyWebSchemes(string value, bool expected)
        => Assert.Equal(expected, UrlHelper.IsSafeExternalUri(new Uri(value)));

    [Theory]
    [InlineData("https://accounts.google.com/gsi/button?client_id=test", true)]
    [InlineData("https://accounts.google.com/gsi/select?client_id=test", true)]
    [InlineData("https://accounts.google.com/o/oauth2/auth", true)]
    [InlineData("https://ACCOUNTS.GOOGLE.COM/signin", true)]
    [InlineData("https://x.com/i/flow/login", true)]
    [InlineData("https://x.com/i/jf/onboarding/web?mode=login", true)]
    [InlineData("https://twitter.com/i/flow/login", true)]
    [InlineData("https://appleid.apple.com/auth/authorize?client_id=com.twitter.twitter.siwa&response_mode=web_message", true)]
    [InlineData("https://accounts.google.com.evil.example/signin", false)]
    [InlineData("https://evil.example/?next=accounts.google.com", false)]
    [InlineData("http://accounts.google.com/signin", false)]
    [InlineData("https://accounts.google.com:444/signin", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("not-a-url", false)]
    public void IsTrustedSignInPopupUri_AllowsOnlyExactAuthenticationHosts(string value, bool expected)
        => Assert.Equal(expected, UrlHelper.IsTrustedSignInPopupUri(value));

    [Theory]
    [InlineData("https://accounts.google.com/o/oauth2/auth", true)]
    [InlineData("https://ACCOUNTS.GOOGLE.COM/gsi/select", true)]
    [InlineData("https://appleid.apple.com/auth/authorize", true)]
    [InlineData("https://x.com/i/flow/login", false)]
    [InlineData("https://accounts.google.com.evil.example/signin", false)]
    [InlineData("http://accounts.google.com/signin", false)]
    [InlineData("intent://accounts.google.com/path", false)]
    public void IsExternalIdentityProviderUri_RequiresExactHttpsHost(string value, bool expected)
        => Assert.Equal(expected, UrlHelper.IsExternalIdentityProviderUri(value));

    // ── IsOnBaseUrl ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://x.com/home",          "https://x.com/home",  true)]
    [InlineData("https://x.com/home?foo=bar",  "https://x.com/home",  true)]  // クエリは無視
    [InlineData("https://X.com/HOME",          "https://x.com/home",  true)]  // 大文字小文字を無視
    [InlineData("https://x.com/notifications", "https://x.com/home",  false)]
    [InlineData("https://twitter.com/home",    "https://x.com/home",  false)] // ホストが異なる
    [InlineData("not-a-url",                   "https://x.com/home",  false)]
    [InlineData("https://x.com/home",          "not-a-url",           false)]
    public void IsOnBaseUrl_Works(string current, string baseUrl, bool expected)
        => Assert.Equal(expected, UrlHelper.IsOnBaseUrl(current, baseUrl));

    // ── GetTimelineGlyph ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://x.com/home",              "\uE80F")] // Home
    [InlineData("https://x.com/notifications",     "\uE7E7")] // Bell
    [InlineData("https://x.com/search?q=test",     "\uE71E")] // Search
    [InlineData("https://x.com/explore",           "\uE71E")] // Search
    [InlineData("https://x.com/i/bookmarks",       "\uE734")] // Bookmark
    [InlineData("https://x.com/i/history",         "\uE734")] // Bookmark (#329 ブックマークは /i/history へ再編)
    [InlineData("https://x.com/daruyanagi/lists",  "\uE71D")] // BulletedList (per-user lists index)
    [InlineData("https://x.com/i/lists/123",       "\uE71D")] // BulletedList (individual list)
    [InlineData("https://x.com/messages",          "\uE8BD")] // Chat
    [InlineData("https://x.com/daruyanagi",        "\uE77B")] // Contact
    [InlineData("https://x.com/i/grok",            "\uE774")] // Globe (fallback)
    [InlineData("not-a-url",                       "")]
    public void GetTimelineGlyph_Works(string url, string expected)
        => Assert.Equal(expected, UrlHelper.GetTimelineGlyph(url));

    // ── IsListHeaderApplicable ────────────────────────────────────────────────

    [Theory]
    [InlineData("https://x.com/notifications",  true)]
    [InlineData("https://x.com/search?q=test",  true)]
    [InlineData("https://x.com/explore",        true)]
    [InlineData("https://x.com/i/bookmarks",    true)]
    [InlineData("https://x.com/i/history",      true)]   // #329 ブックマークは /i/history へ再編
    [InlineData("https://x.com/i/lists/123",    true)]
    [InlineData("https://x.com/daruyanagi",     true)]  // プロファイルページ
    [InlineData("https://x.com/home",           false)]
    [InlineData("https://x.com/messages",       false)]
    [InlineData("https://x.com/i/grok",         false)]
    [InlineData("not-a-url",                    false)]
    public void IsListHeaderApplicable_Works(string url, bool expected)
        => Assert.Equal(expected, UrlHelper.IsListHeaderApplicable(url));

    // ── IsPerUserListsUrl ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://x.com/daruyanagi/lists", true)]
    [InlineData("https://x.com/i/lists",          true)]   // /<seg>/lists にマッチ
    [InlineData("https://x.com/i/lists/123",      false)]  // 個別リストは対象外
    [InlineData("https://x.com/daruyanagi",       false)]  // プロフィール
    [InlineData("https://x.com/home",             false)]
    [InlineData("not-a-url",                      false)]
    public void IsPerUserListsUrl_Works(string url, bool expected)
        => Assert.Equal(expected, UrlHelper.IsPerUserListsUrl(url));

    // ── IsMediaPhotoUrl ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://x.com/shibayan/status/2075804532337172744/photo/1",  true)]
    [InlineData("https://x.com/shibayan/status/2075804532337172744/photo/4/", true)]  // 末尾スラッシュ許容
    [InlineData("https://twitter.com/foo/status/123/photo/1",                 true)]
    [InlineData("https://x.com/shibayan/status/2075804532337172744",          false)] // 個別ツイート本体
    [InlineData("https://x.com/shibayan/status/2075804532337172744/video/1",  false)] // 動画は対象外（#287 段階1）
    [InlineData("https://x.com/home",                                        false)]
    [InlineData("not-a-url",                                                 false)]
    public void IsMediaPhotoUrl_Works(string url, bool expected)
        => Assert.Equal(expected, UrlHelper.IsMediaPhotoUrl(url));

    // ── ParseUrlShortcut ──────────────────────────────────────────────────────

    [Fact]
    public void ParseUrlShortcut_ExtractsUrl()
    {
        string[] lines = ["[InternetShortcut]", "URL=https://x.com/home", "IconIndex=0"];
        Assert.Equal("https://x.com/home", UrlHelper.ParseUrlShortcut(lines));
    }

    [Fact]
    public void ParseUrlShortcut_CaseInsensitiveAndTrimmed()
    {
        string[] lines = ["url=https://x.com/home  "];
        Assert.Equal("https://x.com/home", UrlHelper.ParseUrlShortcut(lines));
    }

        // 以前は同じ式が 3 か所に手書きされていた（#345）
        [Theory]
        [InlineData("https://x.com/home", true)]
        [InlineData("https://x.com/home/", true)]
        [InlineData("https://x.com/HOME", true)]
        [InlineData("https://x.com/notifications", false)]
        [InlineData("https://x.com/i/bookmarks", false)]
        [InlineData("https://x.com/search?q=test", false)]
        [InlineData("not a url", false)]
        public void IsHomeUrl_DetectsHomeTimeline(string url, bool expected)
            => Assert.Equal(expected, UrlHelper.IsHomeUrl(url));


    [Fact]
    public void ParseUrlShortcut_NoUrlLine_ReturnsNull()
    {
        string[] lines = ["[InternetShortcut]", "IconIndex=0"];
        Assert.Null(UrlHelper.ParseUrlShortcut(lines));
    }
}
