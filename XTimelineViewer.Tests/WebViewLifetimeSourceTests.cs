using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace XTimelineViewer.Tests
{
    /// <summary>
    /// WinUI を直接起動しない通常テストで、WebView2 破棄の安全条件を固定する。
    /// </summary>
    public class WebViewLifetimeSourceTests
    {
        private static string Read(string relative)
        {
            var rel = relative.Replace('/', Path.DirectorySeparatorChar);
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var path = Path.Combine(dir.FullName, rel);
                if (File.Exists(path)) return File.ReadAllText(path);
                dir = dir.Parent;
            }
            throw new FileNotFoundException($"リポジトリ内で {relative} が見つかりません");
        }

        [Fact]
        public void CoreEvents_AreNotAsyncVoidAndAreDetached()
        {
            var source = Read("Views/MainWindow.WebView2.cs");
            Assert.DoesNotMatch(
                new Regex(@"\.(NavigationCompleted|SourceChanged|NewWindowRequested|NavigationStarting)\s*\+=\s*async"),
                source);

            foreach (var eventName in new[]
            {
                "SourceChanged",
                "ContainsFullScreenElementChanged",
                "WebResourceResponseReceived",
                "WebMessageReceived",
                "NewWindowRequested",
                "NavigationStarting",
                "NavigationCompleted",
            })
            {
                Assert.Contains($"core.{eventName} -=", source);
            }
        }

        [Fact]
        public void Cleanup_CancelsLifetimeBeforeClosingWebView()
        {
            var source = Read("Views/MainWindow.HardReload.cs");
            var endLifetime = source.IndexOf("EndWebViewLifetime(wv)", StringComparison.Ordinal);
            var close = source.IndexOf("wv.Close()", StringComparison.Ordinal);

            Assert.True(endLifetime >= 0, "CleanupWebView で WebView2 の寿命を終了していません。");
            Assert.True(close > endLifetime, "イベント解除とキャンセルは Close() より前に必要です。");
        }

        [Fact]
        public void GlobalExceptionHandler_UsesRecoverableWhitelist()
        {
            var source = Read("App.xaml.cs");
            Assert.Contains("if (UiExceptionPolicy.CanContinue(e.Exception))", source);
            Assert.Contains("e.Handled = true", source);
        }
    }
}
