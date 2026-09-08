using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using XTimelineViewer.Views.Controls;

namespace XTimelineViewer.Views
{
    public sealed partial class MainWindow
    {
        // 拡張機能の isolated world と WebView2 のページ world は変数を共有しないため、
        // DOM 属性とイベントを小さな指令用ブリッジとして使う。
        private static string TranslationCommandScript(string command) =>
            TranslationLanguageScript +
            $"document.documentElement.setAttribute('data-xtv-translator-command','{command}');" +
            "document.dispatchEvent(new Event('xtv-translator-command'));";

        private static async Task SendTranslationCommandAsync(TimelinePane pane, string command)
        {
            if (pane.WebView.CoreWebView2 is null) return;
            await pane.WebView.CoreWebView2.ExecuteScriptAsync(TranslationCommandScript(command));
        }

        private static string TranslationLanguageScript =>
            "(function(){var language='" +
            (Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja" : "en") +
            "';function update(){if(document.documentElement.getAttribute('data-xtv-translation-language')!==language){" +
            "document.documentElement.setAttribute('data-xtv-translation-language',language);" +
            "document.dispatchEvent(new Event('xtv-translator-language'));}}" +
            "if(document.documentElement)update();else document.addEventListener('DOMContentLoaded',update,{once:true});})();";

        private static string TranslationStateBridgeScript => TranslationLanguageScript + """
            (function () {
                if (window._xtvTranslationStateBridge) return;
                window._xtvTranslationStateBridge = true;
                function initialize() {
                var last = '';
                function report() {
                    var state = (document.documentElement.getAttribute('data-xtv-translation-state') || 'off') + ':' +
                        (document.documentElement.getAttribute('data-xtv-translation-health') || 'unavailable');
                    if (state === last) return;
                    last = state;
                    try { window.chrome.webview.postMessage('translationState:' + state); } catch (_) {}
                }
                new MutationObserver(report).observe(document.documentElement, {
                    attributes: true,
                    attributeFilter: ['data-xtv-translation-state', 'data-xtv-translation-health']
                });
                report();
                }
                if (document.documentElement) initialize();
                else document.addEventListener('DOMContentLoaded', initialize, { once: true });
            })();
            """;

        private void ExtensionErrorSettings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
            => OpenSettingsWindow("Extensions");

        private bool TryHandleTranslationStateMessage(WebView2 webView, string message)
        {
            if (!message.StartsWith("translationState:", StringComparison.Ordinal)) return false;
            var parts = message.Split(':');
            PaneOf(webView)?.SetTranslationState(parts.Length > 1 && parts[1] == "on",
                parts.Length > 2 ? parts[2] : "unavailable");
            return true;
        }
    }
}
