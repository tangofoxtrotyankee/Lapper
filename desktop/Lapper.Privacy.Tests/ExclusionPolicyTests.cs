using Lapper.Privacy.Exclusions;
using Xunit;

namespace Lapper.Privacy.Tests;

public class ExclusionPolicyTests
{
    private sealed class FakeSource(params string[] names) : IUserExclusionSource
    {
        public IReadOnlyCollection<string> GetExcludedProcessNames() => names;
    }

    private static WindowIdentity Window(string process, string? imagePath = "C:\\apps\\app.exe") =>
        new(imagePath, process, ProcessId: 4242, WindowTitle: "t", ClassName: "c");

    [Fact]
    public void FailsClosedWhenProcessCannotBeIdentified()
    {
        var policy = new ExclusionPolicy(new FakeSource());
        var decision = policy.Evaluate(Window("", imagePath: null));
        Assert.True(decision.IsBlocked);
        Assert.Equal(ExclusionReason.UnknownProcess, decision.Reason);
    }

    [Theory]
    [InlineData("keepass.exe")]
    [InlineData("1Password.exe")]
    [InlineData("BITWARDEN.EXE")]
    public void BlocksBuiltInSensitiveApps(string process)
    {
        var policy = new ExclusionPolicy(new FakeSource());
        var decision = policy.Evaluate(Window(process));
        Assert.True(decision.IsBlocked);
        Assert.Equal(ExclusionReason.BuiltInSensitiveApp, decision.Reason);
    }

    [Fact]
    public void BlocksSystemSecuritySurfaces()
    {
        var policy = new ExclusionPolicy(new FakeSource());
        Assert.Equal(
            ExclusionReason.SystemSecuritySurface,
            policy.Evaluate(Window("consent.exe")).Reason);
    }

    [Fact]
    public void BlocksUserExcludedAppsCaseInsensitivelyAndWithoutExeSuffix()
    {
        var policy = new ExclusionPolicy(new FakeSource("MyBank", "other.exe"));
        Assert.Equal(ExclusionReason.UserExcludedApp, policy.Evaluate(Window("mybank.exe")).Reason);
        Assert.Equal(ExclusionReason.UserExcludedApp, policy.Evaluate(Window("OTHER.EXE")).Reason);
    }

    [Fact]
    public void UserListCannotUnblockBuiltIns()
    {
        var policy = new ExclusionPolicy(new FakeSource());
        Assert.True(policy.Evaluate(Window("lockapp.exe")).IsBlocked);
    }

    [Fact]
    public void AllowsOrdinaryApps()
    {
        var policy = new ExclusionPolicy(new FakeSource());
        var decision = policy.Evaluate(Window("notepad.exe"));
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void DecisionCarriesRuleNamesNeverContent()
    {
        var policy = new ExclusionPolicy(new FakeSource("secretapp"));
        var decision = policy.Evaluate(
            new WindowIdentity("C:\\x\\secretapp.exe", "secretapp.exe", 1, "My secret document title", "cls"));
        Assert.True(decision.IsBlocked);
        Assert.DoesNotContain("secret document", decision.MatchedRule ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
