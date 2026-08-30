using Microsoft.Web.WebView2.Core;
using System;

namespace XTimelineViewer.Views
{
    public sealed partial class MainWindow
    {
        /// <summary>
        /// X の DOM 内で、現在位置より上に追加された投稿IDだけを数える。
        /// 本文・ユーザー名・URLは C# 側へ送らず、整数だけを通知する。
        /// </summary>
        private static readonly string UnreadCounterScript = """
            (function () {
                if (window._xtvUnreadWatch) return;
                window._xtvUnreadWatch = true;
                var known = new Set();
                var unread = 0;
                var lastReported = -1;
                var initialized = false;
                var timer = 0;

                function nearTop() {
                    return (window.scrollY || document.documentElement.scrollTop || 0) < 80;
                }
                function report() {
                    if (unread === lastReported) return;
                    lastReported = unread;
                    try { window.chrome.webview.postMessage('unread:' + unread); } catch (_) {}
                }
                function statusId(article) {
                    var links = article.querySelectorAll('a[href*="/status/"]');
                    for (var i = 0; i < links.length; i++) {
                        var m = links[i].getAttribute('href').match(/\/status\/(\d+)/);
                        if (m) return m[1];
                    }
                    return null;
                }
                function scan() {
                    timer = 0;
                    var articles = document.querySelectorAll('article[data-testid="tweet"]');
                    var addedAbove = 0;
                    for (var i = 0; i < articles.length; i++) {
                        var id = statusId(articles[i]);
                        if (!id || known.has(id)) continue;
                        known.add(id);
                        if (initialized && !nearTop() && articles[i].getBoundingClientRect().bottom < 180)
                            addedAbove++;
                    }
                    if (!initialized) initialized = true;
                    if (known.size > 5000) {
                        var current = [];
                        for (var j = 0; j < articles.length; j++) {
                            var currentId = statusId(articles[j]);
                            if (currentId) current.push(currentId);
                        }
                        known = new Set(current);
                    }
                    if (nearTop()) unread = 0;
                    else unread = Math.min(999, unread + addedAbove);
                    report();
                }
                function queueScan() {
                    if (timer) clearTimeout(timer);
                    timer = setTimeout(scan, 250);
                }
                window._xtvUnreadReset = function () { unread = 0; report(); };
                new MutationObserver(queueScan).observe(document.documentElement, { childList: true, subtree: true });
                window.addEventListener('scroll', function () {
                    if (nearTop() && unread) window._xtvUnreadReset();
                }, { passive: true });
                setTimeout(scan, 1200);
            })();
            """;

        private bool TryHandleUnreadMessage(Microsoft.UI.Xaml.Controls.WebView2 webView, string message)
        {
            if (!message.StartsWith("unread:", StringComparison.Ordinal)) return false;
            if (int.TryParse(message["unread:".Length..], out var count))
                PaneOf(webView)?.SetUnreadCount(count);
            return true;
        }
    }
}
