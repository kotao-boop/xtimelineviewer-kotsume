using System;
using Xunit;
using XTimelineViewer.Services;

namespace XTimelineViewer.Tests.Services;

public class AdvancedSearchQueryBuilderTests
{
    [Fact]
    public void Build_CombinesFriendlyFieldsIntoXOperators()
    {
        var query = AdvancedSearchQueryBuilder.Build(new(
            AllWords: "Microsoft Store",
            ExactPhrase: "Kotsume Edition",
            AnyWords: "WinUI WebView2",
            ExcludedWords: "広告 PR",
            FromAccount: "@Microsoft",
            ToAccount: "XDevelopers",
            Language: "ja",
            ImagesOnly: true,
            ExcludeReplies: true,
            Since: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            Until: new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal("Microsoft Store \"Kotsume Edition\" (WinUI OR WebView2) -広告 -PR from:Microsoft to:XDevelopers lang:ja filter:images -filter:replies since:2026-08-01 until:2026-09-01", query);
    }

    [Fact]
    public void Build_StripsQuotesAndAtSigns()
        => Assert.Equal("\"quoted phrase\" from:user", AdvancedSearchQueryBuilder.Build(new(
            ExactPhrase: "\"quoted phrase\"", FromAccount: "@@user")));

    [Fact]
    public void Build_EmptyOptions_ReturnsEmptyString()
        => Assert.Empty(AdvancedSearchQueryBuilder.Build(new()));
}
