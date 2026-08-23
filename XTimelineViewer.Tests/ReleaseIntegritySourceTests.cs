using System;
using System.IO;
using Xunit;

namespace XTimelineViewer.Tests
{
    /// <summary>
    /// コード署名申請に必要な安全条件が、将来の変更で静かに後退しないようにする。
    /// 実際の証明書検証は署名導入後のリリースCIで行う。
    /// </summary>
    public class ReleaseIntegritySourceTests
    {
        private static string FindRepoPath(string relative)
        {
            var rel = relative.Replace('/', Path.DirectorySeparatorChar);
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, rel);
                if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new FileNotFoundException($"リポジトリ内で {relative} が見つかりません");
        }

        private static string Read(string relative) => File.ReadAllText(FindRepoPath(relative));

        [Fact]
        public void Translation_IsOffUntilExternalTransferConsent()
        {
            var source = Read("extensions/xtv-translator/content.js");
            var background = Read("extensions/xtv-translator/background.js");

            Assert.Contains("xtv_translation_external_consent_v1", source);
            Assert.Contains("requestTranslationConsent", source);
            Assert.Contains("chrome.storage.local", source);
            Assert.Contains("showConsentSettings", source);
            Assert.Contains("同意を取り消す", source);
            Assert.DoesNotContain("localStorage.getItem", source);
            Assert.DoesNotContain("localStorage.setItem", source);
            Assert.Contains("getStoredConsent", background);
            Assert.Contains("method: 'POST'", background);
            Assert.DoesNotContain("?client=gtx", background);
        }

        [Fact]
        public void NativeWebBridge_RejectsUntrustedOriginsAndUnsafeSchemes()
        {
            var webView = Read("Views/MainWindow.WebView2.cs");
            var window = Read("Views/MainWindow.xaml.cs");
            var post = Read("Views/MainWindow.Post.cs");

            Assert.Contains("UrlHelper.IsXUrl(e.Source)", webView);
            Assert.Contains("UrlHelper.IsSameHttpsOrigin(args.Uri, cfg.Url)", webView);
            Assert.Contains("UrlHelper.IsSafeExternalUri(external)", webView);
            Assert.Contains("UrlHelper.IsSafeExternalUri(uri)", window);
            Assert.Contains("UrlHelper.IsXUri(timestampUri)", post);
            Assert.Contains("MaxFrameBase64Chars", post);
            Assert.Contains("maxMediaBytes", post);
            Assert.DoesNotContain(" SNIP=", post);
        }

        [Fact]
        public void UnsignedReleaseCandidates_AreNotPublished()
        {
            var workflow = Read(".github/workflows/release.yml");

            Assert.Contains("permissions:\n  contents: read", workflow.Replace("\r\n", "\n"));
            Assert.Contains("UNSIGNED-DO-NOT-DISTRIBUTE-", workflow);
            Assert.Contains("UNSIGNED-DO-NOT-DISTRIBUTE.txt", workflow);
            Assert.DoesNotContain("gh release create", workflow);
            Assert.DoesNotContain("gh release upload", workflow);
            Assert.DoesNotContain("--clobber", workflow);
            Assert.Contains("collect-legal-notices.ps1", workflow);
            Assert.Contains("verify-third-party-signatures.ps1", workflow);
            Assert.True(
                workflow.IndexOf("Separate debug symbols", StringComparison.Ordinal) <
                workflow.IndexOf("Build Inno Setup installer", StringComparison.Ordinal),
                "PDB files must be removed before the installer and portable ZIP are built.");
        }

        [Fact]
        public void SignPathScope_IncludesManagedApplicationDll()
        {
            var runbook = Read("docs/CODE_SIGNING_RUNBOOK.md");
            var verifier = Read("scripts/verify-release-signatures.ps1");

            Assert.Contains("`XTimelineViewer.dll`", runbook);
            Assert.Contains("XTimelineViewer.dll", verifier);
            Assert.Contains("TimeStamperCertificate", verifier);
        }

        [Fact]
        public void Distribution_IncludesLegalNotices()
        {
            var project = Read("XTimelineViewer.csproj");
            var workflow = Read(".github/workflows/release.yml");

            Assert.Contains("<Content Include=\"LICENSE\">", project);
            Assert.Contains("<Content Include=\"THIRD-PARTY-NOTICES.md\">", project);
            Assert.Contains("collect-legal-notices.ps1", workflow);
            Assert.Contains("Microsoft-WindowsAppSDK-LICENSE.txt", Read("scripts/installer.iss"));
        }

        [Fact]
        public void StoreManifest_DoesNotReuseUpstreamIdentity()
        {
            var manifest = Read("Package.appxmanifest");

            Assert.DoesNotContain("4275.XTimelineViewer", manifest);
            Assert.DoesNotContain("B73FDB0C-06E5-4824-9E7F-3AF969921DF4", manifest);
            Assert.DoesNotContain("だるやなぎ", manifest);
            Assert.Contains("XTimelineViewerKotsume.Development", manifest);
            Assert.Contains("Microsoft Storeへ提出してはいけない", manifest);
        }

        [Fact]
        public void LockedRestore_IsRequiredForApplicationAndTests()
        {
            var appProject = Read("XTimelineViewer.csproj");
            var testProject = Read("XTimelineViewer.Tests/XTimelineViewer.Tests.csproj");
            var ci = Read(".github/workflows/ci.yml");

            Assert.Contains("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", appProject);
            Assert.Contains("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", testProject);
            Assert.True(File.Exists(FindRepoPath("packages.lock.json")));
            Assert.True(File.Exists(FindRepoPath("XTimelineViewer.Tests/packages.lock.json")));
            Assert.Contains("--locked-mode", ci);
        }

        [Fact]
        public void UpdateCheck_TargetsKotsumeRepository_NotUpstreamPackage()
        {
            var source = Read("Views/MainWindow.Updates.cs");

            Assert.Contains("kotao-boop/xtimelineviewer-kotsume/releases/latest", source);
            Assert.DoesNotContain("api.github.com/repos/daruyanagi/XTimelineViewer", source);
            Assert.DoesNotContain("winget upgrade daruyanagi.XTimelineViewer", source);
        }

        [Fact]
        public void Launcher_IsBuiltFromSourceInReleaseWorkflow()
        {
            var workflow = Read(".github/workflows/release.yml");

            Assert.Contains("build-launcher.ps1 -Architecture x64", workflow);
            Assert.Contains("build-launcher.ps1 -Architecture arm64", workflow);
            Assert.False(File.Exists(Path.Combine(FindRepoPath("tools/launcher"), "xtv.exe")),
                "ビルド済みxtv.exeをコミットせず、CIで公開ソースから生成してください。");
        }

        [Fact]
        public void Readme_DoesNotDescribeCurrentUnsignedReleaseAsSigned()
        {
            var readme = Read("README.md");

            Assert.Contains("現在公開中のv2.1.0は未署名", readme);
            Assert.Contains("## Code signing policy", readme);
            Assert.Contains("Application preparation in progress", readme);
        }

        [Fact]
        public void FirstPartyProductMetadata_IsExplicit()
        {
            var project = Read("XTimelineViewer.csproj");
            var launcher = Read("tools/launcher/xtv.rc");

            Assert.Contains("<Product>XTimelineViewer Kotsume Edition</Product>", project);
            Assert.Contains("<Company>Kotsume Project</Company>", project);
            Assert.Contains("<IncludeSourceRevisionInInformationalVersion>false", project);
            Assert.Contains("XTimelineViewer Kotsume Edition", launcher);
            Assert.Contains("XTV_VERSION_STRING", launcher);
        }
    }
}
