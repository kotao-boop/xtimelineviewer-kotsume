using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XTimelineViewer.Tests
{
    public class LocalizationParityTests
    {
        private static string FindRepoFile(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new FileNotFoundException(relative);
        }

        private static HashSet<string> Keys(string path) => XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToHashSet(StringComparer.Ordinal);

        [Fact]
        public void JapaneseAndEnglishResources_HaveTheSameKeys()
        {
            var ja = Keys(FindRepoFile("Strings/ja-JP/Resources.resw"));
            var en = Keys(FindRepoFile("Strings/en-US/Resources.resw"));
            Assert.True(ja.SetEquals(en),
                $"翻訳キーが一致しません。日本語のみ: {string.Join(", ", ja.Except(en))}; 英語のみ: {string.Join(", ", en.Except(ja))}");
        }
    }
}
