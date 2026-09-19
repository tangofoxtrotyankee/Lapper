using System.Text.RegularExpressions;

namespace Lapper.Privacy.Redaction;

/// <summary>
/// Named secret patterns applied to every extracted text block before it can
/// leave the machine. Names are metadata-safe; matched text never is.
/// </summary>
public static partial class SecretPatterns
{
    public sealed record NamedPattern(string Name, Regex Regex);

    public static IReadOnlyList<NamedPattern> All { get; } =
    [
        new("aws_access_key", AwsAccessKey()),
        new("github_token", GitHubToken()),
        new("private_key_block", PrivateKeyBlock()),
        new("jwt", Jwt()),
        new("openai_key", OpenAiKey()),
        new("password_assignment", PasswordAssignment()),
        new("connection_string_secret", ConnectionStringSecret()),
    ];

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b")]
    private static partial Regex AwsAccessKey();

    [GeneratedRegex(@"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{36,}\b|\bgithub_pat_[A-Za-z0-9_]{60,}\b")]
    private static partial Regex GitHubToken();

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----")]
    private static partial Regex PrivateKeyBlock();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_-]{20,}\b")]
    private static partial Regex OpenAiKey();

    [GeneratedRegex(@"(?i)\b(?:password|passwd|pwd|passphrase)\s*[:=]\s*\S+")]
    private static partial Regex PasswordAssignment();

    [GeneratedRegex(@"(?i)(?:pwd|password)=[^;\s]+")]
    private static partial Regex ConnectionStringSecret();

    /// <summary>Regex prefilter for card-like digit runs; confirmed by Luhn in code.</summary>
    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b")]
    public static partial Regex CardCandidate();
}
