using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Soenneker.Reddit.Ads.OpenApiClient;
using Soenneker.Reddit.Ads.OpenApiClient.Models;
using Soenneker.Reddit.Ads.OpenApiClientUtil.Abstract;

namespace Soenneker.Reddit.Ads.Conversions.Tests;

public sealed class ConversionPayloadTests
{
    [Test]
    public void Hashes_canonical_identifiers_using_Reddit_examples()
    {
        var conversions = new RedditAdsConversions(new UnusedClient());
        var user = conversions.CreateUser("Al.ice+Apple@Example.Com", "+1 (555) 444-1234 ext. 789", "customer12345");
        if (user.Email != "ff8d9819fc0e12bf0d24892e45987e249a28dce836a85cad60e28eaaa8c6d976" ||
            user.PhoneNumber != "e5b124c58580eb16bd959b8d0cac12b12c952e2ceae0203d416cff94f10b994a" ||
            user.ExternalId != "a4cc2fc5adf58a029291c1514d273989113a1d05e1d753c1d0c3a848af7109cc")
            throw new Exception("Canonical SHA-256 values do not match Reddit's examples.");
    }

    [Test]
    public async Task Sends_conversion_envelope_and_preserves_deduplication_id()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http);
        var client = new RedditAdsOpenApiClient(adapter);
        var conversions = new RedditAdsConversions(new ClientStub(client));
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var conversion = conversions.CreateEvent(StandardTrackingType.Purchase, "order-123", occurredAt);
        conversion.User = conversions.CreateUser(email: "alice@example.com");
        conversion.Metadata!.Value = 42.5;
        conversion.Metadata.Currency = "USD";
        conversion.ClickId = "click-123";
        var response = await conversions.Send("pixel-123", conversion, testId: "test-123");
        using var json = JsonDocument.Parse(handler.Body!);
        var data = json.RootElement.GetProperty("data");
        var item = data.GetProperty("events")[0];
        if (handler.Url != "https://ads-api.reddit.com/api/v3/pixels/pixel-123/conversion_events" || handler.Method != HttpMethod.Post ||
            data.GetProperty("test_id").GetString() != "test-123" || item.GetProperty("event_at").GetInt64() != occurredAt.ToUnixTimeMilliseconds() ||
            item.GetProperty("action_source").GetString() != "WEBSITE" || item.GetProperty("type").GetProperty("tracking_type").GetString() != "PURCHASE" ||
            item.GetProperty("metadata").GetProperty("conversion_id").GetString() != "order-123" ||
            item.GetProperty("metadata").GetProperty("value").GetDouble() != 42.5 ||
            item.GetProperty("user").GetProperty("email").GetString() != conversion.User.Email ||
            response?.Data?.Message != "ok")
            throw new Exception("Unexpected conversion request or response.");
        if (conversion.Metadata.ConversionId != "order-123")
            throw new Exception("The wrapper must not replace caller-supplied IDs.");
    }

    [Test]
    [Arguments(0)]
    [Arguments(1001)]
    public async Task Rejects_invalid_batch_sizes_before_obtaining_client(int count)
    {
        var conversions = new RedditAdsConversions(new UnusedClient());
        try
        {
            await conversions.SendBatch("pixel", new ComponentsSchemaPixelConversionEvent[count]);
        }
        catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Expected batch-size validation.");
    }

    [Test]
    public void Custom_event_requires_a_name()
    {
        var conversions = new RedditAdsConversions(new UnusedClient());
        try { conversions.CreateEvent(StandardTrackingType.Custom, "id"); }
        catch (ArgumentException) { return; }
        throw new Exception("Expected custom-event validation.");
    }

    [Test]
    public async Task Cancellation_prevents_obtaining_client()
    {
        var conversions = new RedditAdsConversions(new UnusedClient());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await conversions.Send("pixel", conversions.CreateEvent(StandardTrackingType.Lead, "lead-1"), cancellationToken: cancellation.Token);
        }
        catch (OperationCanceledException) { return; }
        throw new Exception("Expected cancellation.");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Url { get; private set; }
        public HttpMethod? Method { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri?.AbsoluteUri;
            Method = request.Method;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"message\":\"ok\"}}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ClientStub(RedditAdsOpenApiClient client) : IRedditAdsOpenApiClientUtil
    {
        public ValueTask<RedditAdsOpenApiClient> Get(CancellationToken cancellationToken = default) => ValueTask.FromResult(client);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class UnusedClient : IRedditAdsOpenApiClientUtil
    {
        public ValueTask<RedditAdsOpenApiClient> Get(CancellationToken cancellationToken = default) => throw new Exception("Client should not be obtained.");
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
