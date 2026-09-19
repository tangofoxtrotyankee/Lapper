using System.Net;
using System.Text;
using Lapper.ApiClient;
using Lapper.Contracts;
using Xunit;

namespace Lapper.ApiClient.Tests;

public class BackendUrlValidatorTests
{
    [Theory]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("http://localhost:3000/")]
    [InlineData("https://api.lapper.example")]
    public void AcceptsLoopbackHttpAndAnyHttps(string url)
    {
        Assert.True(BackendUrlValidator.TryValidate(url, out var parsed));
        Assert.NotNull(parsed);
    }

    [Theory]
    [InlineData("http://lapper.example")] // plain http off-machine
    [InlineData("ftp://127.0.0.1")]
    [InlineData("https://user:pw@host")]
    [InlineData("https://host/?q=1")]
    [InlineData("https://host/#frag")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsUnsafeUrls(string? url)
    {
        Assert.False(BackendUrlValidator.TryValidate(url, out _));
    }

    [Theory]
    [InlineData("https://host.example/api", "https://host.example/api/")]
    [InlineData("http://127.0.0.1:3000", "http://127.0.0.1:3000/")]
    [InlineData("https://host.example/api/", "https://host.example/api/")]
    public void NormalizesToTrailingSlashSoRelativePathsKeepThePrefix(string input, string expected)
    {
        // RFC 3986: new Uri(base, "v1/...") drops the last segment of a
        // non-slash-terminated base — "/api" would silently vanish.
        Assert.True(BackendUrlValidator.TryValidate(input, out var parsed));
        Assert.Equal(expected, parsed!.ToString());
        Assert.Equal(expected + "v1/context/orient", new Uri(parsed, "v1/context/orient").ToString());
    }
}

public class SseFrameReaderTests
{
    [Fact]
    public async Task ParsesFramesAndSkipsKeepAliveComments()
    {
        var reader = new StringReader(
            "event: accepted\ndata: {\"a\":1}\n\n" +
            ": keep-alive\n\n" +
            "event: result\ndata: {\"b\":2}\n\n");

        var first = await SseFrameReader.ReadFrameAsync(reader, CancellationToken.None);
        Assert.Equal("accepted", first!.Event);
        Assert.Equal("{\"a\":1}", first.Data);

        var second = await SseFrameReader.ReadFrameAsync(reader, CancellationToken.None);
        Assert.Equal("result", second!.Event);

        Assert.Null(await SseFrameReader.ReadFrameAsync(reader, CancellationToken.None));
    }
}

public class LapperApiClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static OrientRequest Request() => new()
    {
        RequestId = "11111111-2222-3333-4444-555555555555",
        Application = new ApplicationInfo { ProcessName = "notepad.exe", Category = "document" },
        Context = new ScreenContextPayload
        {
            Blocks = [new ContextBlock { Id = "b1", Role = "document", Text = "hello" }],
            ImageIncluded = false,
        },
        Client = new ClientInfo { Version = "0.2.0", Capabilities = ["uia"] },
    };

    private static HttpResponseMessage SseResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(body))),
    };

    [Fact]
    public async Task StreamsTypedEventsAndSendsRequestIdHeader()
    {
        var sse =
            "event: accepted\ndata: {\"requestId\":\"x\",\"model\":\"gpt-5.6-terra\",\"route\":\"default\"}\n\n" +
            "event: orientation.delta\ndata: {\"text\":\"{\\\"orientation\\\":\\\"Hi\\\"}\"}\n\n" +
            "event: orientation.complete\ndata: {}\n\n" +
            "event: result\ndata: {\"contentType\":\"unknown\",\"orientation\":\"Hi\",\"summary\":\"\",\"facts\":[],\"suggestedActions\":[],\"warnings\":[],\"needsMoreContext\":false}\n\n" +
            "event: usage\ndata: {\"inputTokens\":5,\"outputTokens\":2,\"latencyMs\":100}\n\n";
        var handler = new StubHandler(_ => SseResponse(sse));
        var client = new LapperApiClient(new HttpClient(handler), () => new Uri("http://127.0.0.1:3000/"));

        var events = new List<ApiStreamEvent>();
        await foreach (var apiEvent in client.OrientAsync(Request(), CancellationToken.None))
        {
            events.Add(apiEvent);
        }

        Assert.Equal("11111111-2222-3333-4444-555555555555",
            handler.LastRequest!.Headers.GetValues("X-Lapper-Request-Id").Single());
        Assert.Contains(handler.LastBody!, s => true); // body captured
        Assert.Contains("\"imageIncluded\":false", handler.LastBody);
        Assert.DoesNotContain("windowTitle\":\"", handler.LastBody); // titles stay local

        Assert.IsType<ApiStreamEvent.Accepted>(events[0]);
        Assert.Contains(events, e => e is ApiStreamEvent.Delta);
        var result = Assert.IsType<ApiStreamEvent.OrientResult>(
            events.Single(e => e is ApiStreamEvent.OrientResult));
        Assert.Equal("Hi", result.Result.Orientation);
        Assert.Contains(events, e => e is ApiStreamEvent.Usage { InputTokens: 5 });
    }

    [Fact]
    public async Task MapsErrorEnvelopesFromNonSuccessResponses()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(
                "{\"error\":{\"code\":\"MODEL_UNAVAILABLE\",\"message\":\"No AI provider is configured on this server.\",\"requestId\":\"x\"}}"),
        });
        var client = new LapperApiClient(new HttpClient(handler), () => new Uri("http://127.0.0.1:3000/"));

        var events = new List<ApiStreamEvent>();
        await foreach (var apiEvent in client.OrientAsync(Request(), CancellationToken.None))
        {
            events.Add(apiEvent);
        }

        var error = Assert.IsType<ApiStreamEvent.Error>(Assert.Single(events));
        Assert.Equal("MODEL_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task SurfacesUnreachableBackendAsTypedError()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));
        var client = new LapperApiClient(new HttpClient(handler), () => new Uri("http://127.0.0.1:3000/"));

        var events = new List<ApiStreamEvent>();
        await foreach (var apiEvent in client.OrientAsync(Request(), CancellationToken.None))
        {
            events.Add(apiEvent);
        }

        var error = Assert.IsType<ApiStreamEvent.Error>(Assert.Single(events));
        Assert.Equal("BACKEND_UNREACHABLE", error.Code);
    }
}
