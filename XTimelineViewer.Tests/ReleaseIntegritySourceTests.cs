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

            Assert.Contains("xtv_translation_external_consent_v1", source);
            Assert.Contains("requestTranslationConsent", source);
            Assert.Contains("localStorage.getItem('xtv_auto_translate') === 'true'", source);
            Assert.DoesNotContain("localStorage.getItem('xtv_auto_translate') !== 'false'", source);
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
