namespace XTimelineViewer.Services
{
    /// <summary>
    /// X のソーシャルログインを誤って外部ブラウザーへ出さないための共通定義。
    /// 入力欄、Cookie、メールアドレス、パスワードには触れず、Google / Apple と
    /// 明記されたボタン操作だけをアプリへ通知する。
    /// </summary>
    internal static class SignInFlowHelper
    {
        internal const string BlockedMessage = "xtv-social-signin-blocked";
        internal const string PasswordResetUrl = "https://x.com/account/begin_password_reset";

        internal const string GuardScript = """
            (() => {
              if (window.__xtvSocialSignInGuard) return;
              window.__xtvSocialSignInGuard = true;
              if (!['x.com', 'www.x.com', 'twitter.com', 'www.twitter.com'].includes(location.hostname)) return;
              addEventListener('click', event => {
                const element = event.target instanceof Element
                  ? event.target.closest('button, a, [role="button"]')
                  : null;
                if (!element) return;
                const label = `${element.innerText || ''} ${element.getAttribute('aria-label') || ''}`;
                if (!/(^|\s)(Google|Apple)(\s|$)/i.test(label)) return;
                event.preventDefault();
                event.stopImmediatePropagation();
                chrome.webview.postMessage('xtv-social-signin-blocked');
              }, true);
            })();
            """;
    }
}
