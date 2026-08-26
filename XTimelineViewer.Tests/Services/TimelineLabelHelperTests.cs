using XTimelineViewer.Models;
using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services
{
    public class TimelineLabelHelperTests
    {
        private static string Text(string key) => key switch
        {
            "Timeline_Home" => "ホーム",
            "Timeline_Notifications" => "通知",
            "Timeline_Bookmarks" => "ブックマーク",
            "Timeline_Lists" => "リスト",
            "Timeline_SearchName" => "「{0}」の検索",
            _ => key,
        };

        [Theory]
        [InlineData("https://x.com/home", "ホーム")]
        [InlineData("https://x.com/notifications", "通知")]
        [InlineData("https://x.com/i/bookmarks", "ブックマーク")]
        [InlineData("https://x.com/kotao/lists", "リスト")]
        [InlineData("https://x.com/kotao", "@kotao")]
        public void FriendlyName_RecognizesCommonTimelineUrls(string url, string expected)
        {
            Assert.Equal(expected, TimelineLabelHelper.GetFriendlyName(new TimelineConfig { Url = url }, Text));
        }

        [Fact]
        public void FriendlyName_PrefersCustomName()
        {
            var config = new TimelineConfig { Url = "https://x.com/home", Name = "朝の確認" };
            Assert.Equal("朝の確認", TimelineLabelHelper.GetFriendlyName(config, Text));
        }

        [Fact]
        public void Clone_CopiesUxFieldsWithoutSharingInstance()
        {
            var source = new TimelineConfig { Name = "仕事", Url = "https://x.com/home", IsVisible = false };
            var clone = source.Clone();
            Assert.NotSame(source, clone);
            Assert.Equal("仕事", clone.Name);
            Assert.False(clone.IsVisible);
        }
    }
}
