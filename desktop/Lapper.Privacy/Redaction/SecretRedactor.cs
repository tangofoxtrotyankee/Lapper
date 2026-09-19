namespace Lapper.Privacy.Redaction;

/// <summary>
/// Result of a redaction pass. MatchedPatternNames carries pattern NAMES
/// only — safe for flags/telemetry; never the matched text.
/// </summary>
public sealed record RedactionResult(string Text, int RedactionCount, IReadOnlyList<string> MatchedPatternNames);

public interface ISecretRedactor
{
    RedactionResult Redact(string text);
}

/// <summary>
/// Replaces secret-shaped content with [REDACTED:name] markers. Runs after
/// extraction and before anything is ranked, shown or transmitted.
/// </summary>
public sealed class SecretRedactor : ISecretRedactor
{
    public RedactionResult Redact(string text)
    {
        var count = 0;
        var matched = new List<string>();
        var current = text;

        foreach (var pattern in SecretPatterns.All)
        {
            current = pattern.Regex.Replace(current, _ =>
            {
                count++;
                if (!matched.Contains(pattern.Name))
                {
                    matched.Add(pattern.Name);
                }
                return $"[REDACTED:{pattern.Name}]";
            });
        }

        current = SecretPatterns.CardCandidate().Replace(current, match =>
        {
            var digits = new string([.. match.Value.Where(char.IsDigit)]);
            if (digits.Length < 13 || digits.Length > 19 || !PassesLuhn(digits))
            {
                return match.Value;
            }
            count++;
            if (!matched.Contains("payment_card"))
            {
                matched.Add("payment_card");
            }
            return "[REDACTED:payment_card]";
        });

        return new RedactionResult(current, count, matched);
    }

    public static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }
            sum += digit;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }
}
