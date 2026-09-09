using System;
using System.Collections.Generic;
using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public class ExtensionConflictServiceTests
{
    [Fact]
    public void NormalizeName_IgnoresCaseAndRepeatedWhitespace()
    {
        Assert.Equal(
            "X TIMELINE TRANSLATOR",
            ExtensionConflictService.NormalizeName("  X   Timeline\tTranslator "));
    }

    [Fact]
    public void ClaimedName_SuppressesOnlyTheDuplicateName()
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        Assert.False(ExtensionConflictService.IsNameClaimed(claimed, "X Timeline Translator"));
        ExtensionConflictService.ClaimName(claimed, "X Timeline Translator");

        Assert.True(ExtensionConflictService.IsNameClaimed(claimed, "x  timeline translator"));
        Assert.False(ExtensionConflictService.IsNameClaimed(claimed, "Another Extension"));
    }

    [Fact]
    public void EmptyName_DoesNotBlockOtherExtensions()
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        ExtensionConflictService.ClaimName(claimed, " ");

        Assert.False(ExtensionConflictService.IsNameClaimed(claimed, "Another Extension"));
        Assert.Empty(claimed);
    }
}
