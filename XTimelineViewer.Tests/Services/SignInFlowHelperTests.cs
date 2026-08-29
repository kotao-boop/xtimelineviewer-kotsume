using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public class SignInFlowHelperTests
{
    [Fact]
    public void GuardScript_BlocksOnlyNamedProviderButtonsWithoutReadingCredentials()
    {
        var script = SignInFlowHelper.GuardScript;
        Assert.Contains("Google|Apple", script);
        Assert.Contains(SignInFlowHelper.BlockedMessage, script);
        Assert.DoesNotContain("document.cookie", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("input.value", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PasswordReset_UsesExactXHttpsOrigin()
    {
        var uri = new Uri(SignInFlowHelper.PasswordResetUrl);
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("x.com", uri.Host);
        Assert.Equal("/account/begin_password_reset", uri.AbsolutePath);
    }
}
