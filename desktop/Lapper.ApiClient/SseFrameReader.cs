namespace Lapper.ApiClient;

public sealed record SseFrame(string Event, string Data);

/// <summary>
/// Line-based SSE frame parser over a StreamReader. Pure enough to test
/// with a MemoryStream; comment lines (": keep-alive") are skipped.
/// </summary>
public static class SseFrameReader
{
    public static async Task<SseFrame?> ReadFrameAsync(TextReader reader, CancellationToken ct)
    {
        string? eventName = null;
        List<string>? dataLines = null;

        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                return null; // end of stream
            }

            if (line.Length == 0)
            {
                if (dataLines is not null)
                {
                    return new SseFrame(eventName ?? "message", string.Join('\n', dataLines));
                }
                eventName = null;
                continue; // empty frame (e.g. after a comment)
            }

            if (line[0] == ':')
            {
                continue; // comment / keep-alive
            }
            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line[6..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                (dataLines ??= []).Add(line[5..].TrimStart());
            }
        }
    }
}
