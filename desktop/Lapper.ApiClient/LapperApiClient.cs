using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Lapper.Contracts;

namespace Lapper.ApiClient;

/// <summary>
/// SSE client for POST /v1/context/orient and /v1/context/action.
/// Cancellation is first-class: disposing the enumeration aborts the HTTP
/// request, which the backend propagates to the model call.
/// </summary>
public sealed class LapperApiClient(HttpClient httpClient, Func<Uri> baseUrlProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IAsyncEnumerable<ApiStreamEvent> OrientAsync(OrientRequest request, CancellationToken ct) =>
        StreamAsync("v1/context/orient", request, request.RequestId, structured: true, ct);

    public IAsyncEnumerable<ApiStreamEvent> ActionAsync(ActionRequest request, CancellationToken ct) =>
        StreamAsync("v1/context/action", request, request.RequestId, structured: false, ct);

    private async IAsyncEnumerable<ApiStreamEvent> StreamAsync<TRequest>(
        string path,
        TRequest request,
        string requestId,
        bool structured,
        [EnumeratorCancellation] CancellationToken ct)
    {
        HttpResponseMessage? response = null;
        StreamReader? reader = null;
        try
        {
            ApiStreamEvent.Error? sendError = null;
            try
            {
                var message = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUrlProvider(), path))
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(request, JsonOptions),
                        Encoding.UTF8,
                        "application/json"),
                };
                message.Headers.Add("X-Lapper-Request-Id", requestId);
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                response = await httpClient
                    .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                sendError = new ApiStreamEvent.Error(
                    "BACKEND_UNREACHABLE",
                    "Could not reach the Lapper backend. Is it running?");
            }
            if (sendError is not null || response is null)
            {
                yield return sendError ?? new ApiStreamEvent.Error(
                    "BACKEND_UNREACHABLE",
                    "Could not reach the Lapper backend. Is it running?");
                yield break;
            }

            if (!response.IsSuccessStatusCode)
            {
                yield return await ReadErrorEnvelopeAsync(response, ct).ConfigureAwait(false);
                yield break;
            }

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            reader = new StreamReader(stream, Encoding.UTF8);

            while (true)
            {
                SseFrame? frame = null;
                var interrupted = false;
                try
                {
                    frame = await SseFrameReader.ReadFrameAsync(reader, ct).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    interrupted = true;
                }
                if (interrupted)
                {
                    yield return new ApiStreamEvent.Error(
                        "STREAM_INTERRUPTED",
                        "The connection to the backend was interrupted.");
                    yield break;
                }
                if (frame is null)
                {
                    yield break;
                }

                var mapped = MapFrame(frame, structured);
                if (mapped is not null)
                {
                    yield return mapped;
                }
            }
        }
        finally
        {
            reader?.Dispose();
            response?.Dispose();
        }
    }

    private static ApiStreamEvent? MapFrame(SseFrame frame, bool structured)
    {
        try
        {
            switch (frame.Event)
            {
                case "accepted":
                {
                    var data = JsonDocument.Parse(frame.Data).RootElement;
                    return new ApiStreamEvent.Accepted(
                        data.TryGetProperty("model", out var m) ? m.GetString() ?? "" : "",
                        data.TryGetProperty("route", out var r) ? r.GetString() ?? "" : "");
                }
                case "orientation.delta":
                {
                    var data = JsonDocument.Parse(frame.Data).RootElement;
                    return new ApiStreamEvent.Delta(
                        data.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "");
                }
                case "orientation.complete":
                    return new ApiStreamEvent.Completed();
                case "result" when structured:
                {
                    var result = JsonSerializer.Deserialize<OrientationResult>(frame.Data, JsonOptions);
                    return result is null
                        ? new ApiStreamEvent.Error("CLIENT_PARSE_ERROR", "The result could not be read.")
                        : new ApiStreamEvent.OrientResult(result);
                }
                case "result":
                {
                    var data = JsonDocument.Parse(frame.Data).RootElement;
                    return new ApiStreamEvent.TextResult(
                        data.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "");
                }
                case "usage":
                {
                    var data = JsonDocument.Parse(frame.Data).RootElement;
                    return new ApiStreamEvent.Usage(
                        IntOrZero(data, "inputTokens"),
                        IntOrZero(data, "outputTokens"),
                        IntOrZero(data, "latencyMs"));
                }
                case "error":
                {
                    var data = JsonDocument.Parse(frame.Data).RootElement;
                    return new ApiStreamEvent.Error(
                        data.TryGetProperty("code", out var c) ? c.GetString() ?? "UNKNOWN" : "UNKNOWN",
                        data.TryGetProperty("message", out var m)
                            ? m.GetString() ?? "The request failed."
                            : "The request failed.");
                }
                default:
                    return null;
            }
        }
        catch (JsonException)
        {
            return new ApiStreamEvent.Error("CLIENT_PARSE_ERROR", "A stream event could not be read.");
        }
    }

    private static async Task<ApiStreamEvent.Error> ReadErrorEnvelopeAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var envelope = JsonDocument.Parse(body).RootElement;
            var error = envelope.GetProperty("error");
            return new ApiStreamEvent.Error(
                error.GetProperty("code").GetString() ?? "HTTP_ERROR",
                error.GetProperty("message").GetString() ?? "The backend rejected the request.");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new ApiStreamEvent.Error(
                "HTTP_ERROR",
                $"The backend returned HTTP {(int)response.StatusCode}.");
        }
    }

    private static int IntOrZero(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
