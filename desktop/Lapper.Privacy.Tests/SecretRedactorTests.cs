using Lapper.Privacy.Redaction;
using Xunit;

namespace Lapper.Privacy.Tests;

public class SecretRedactorTests
{
    private readonly SecretRedactor _redactor = new();

    [Theory]
    [InlineData("key AKIAIOSFODNN7EXAMPLE here", "aws_access_key")]
    [InlineData("token ghp_0123456789abcdefghijklmnopqrstuvwxyzAB done", "github_token")]
    [InlineData("-----BEGIN RSA PRIVATE KEY----- xxx", "private_key_block")]
    [InlineData("sk-abcdefghijklmnopqrstuvwx more", "openai_key")]
    [InlineData("password: hunter2!", "password_assignment")]
    [InlineData("Server=x;Pwd=supersecret;Db=y", "connection_string_secret")]
    public void RedactsSecretShapedContent(string input, string expectedPattern)
    {
        var result = _redactor.Redact(input);
        Assert.True(result.RedactionCount >= 1);
        Assert.Contains(expectedPattern, result.MatchedPatternNames);
        Assert.Contains($"[REDACTED:{expectedPattern}]", result.Text);
    }

    [Fact]
    public void RedactsTheEntirePrivateKeyBodyNotJustTheHeader()
    {
        var pem = "before\n-----BEGIN RSA PRIVATE KEY-----\nkeymaterialAAAA\nBBBB\n" +
                  "-----END RSA PRIVATE KEY-----\nafter";
        var result = _redactor.Redact(pem);
        Assert.DoesNotContain("keymaterial", result.Text);
        Assert.Contains("[REDACTED:private_key_block]", result.Text);
        Assert.Contains("before", result.Text);
        Assert.Contains("after", result.Text);

        // No END marker (key continues past the block): redact to the end.
        var headerOnly = _redactor.Redact("x\n-----BEGIN EC PRIVATE KEY-----\nkeybodyCCCC");
        Assert.DoesNotContain("keybodyCCCC", headerOnly.Text);
        Assert.Contains("[REDACTED:private_key_block]", headerOnly.Text);
    }

    [Fact]
    public void RedactsJwtShapedContent()
    {
        // Fixture assembled from parts so secret scanners never see a
        // token-shaped literal in the source; the joined value is pure fake.
        var jwt = string.Join('.', "eyJhbGciOiJIUzI1NiJ9", "eyJzdWIiOiIxMjM0In0", "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJVadQs");
        var result = _redactor.Redact("jwt " + jwt);
        Assert.True(result.RedactionCount >= 1);
        Assert.Contains("jwt", result.MatchedPatternNames);
        Assert.Contains("[REDACTED:jwt]", result.Text);
    }

    [Fact]
    public void RedactsLuhnValidCardNumbersOnly()
    {
        var valid = _redactor.Redact("card 4111 1111 1111 1111 on file");
        Assert.Contains("[REDACTED:payment_card]", valid.Text);

        var invalid = _redactor.Redact("card 4111 1111 1111 1112 on file");
        Assert.DoesNotContain("REDACTED", invalid.Text);

        var phoneish = _redactor.Redact("ref 1234 5678 9012 track");
        Assert.DoesNotContain("payment_card", string.Join(',', phoneish.MatchedPatternNames));
    }

    [Theory]
    [InlineData("Your fee rises from £400 to £472 on 1 October.")]
    [InlineData("Meeting at 10:30 with password policy review team")]
    [InlineData("Invoice number INV-2026-00412 due 2026-10-01")]
    public void LeavesOrdinaryTextUntouched(string input)
    {
        var result = _redactor.Redact(input);
        Assert.Equal(0, result.RedactionCount);
        Assert.Equal(input, result.Text);
    }

    [Fact]
    public void MatchedPatternNamesNeverContainTheSecret()
    {
        var result = _redactor.Redact("password: SUPERSECRET99");
        Assert.All(result.MatchedPatternNames, name => Assert.DoesNotContain("SUPERSECRET", name));
    }

    [Fact]
    public void LuhnHelperIsCorrect()
    {
        Assert.True(SecretRedactor.PassesLuhn("4111111111111111"));
        Assert.False(SecretRedactor.PassesLuhn("4111111111111112"));
    }
}
