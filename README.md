[![](https://img.shields.io/nuget/v/soenneker.reddit.ads.conversions.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.reddit.ads.conversions/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.reddit.ads.conversions/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.reddit.ads.conversions/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.reddit.ads.conversions.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.reddit.ads.conversions/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Reddit.Ads.Conversions
### A utility for sending Reddit Ads conversion events.

## Installation

```
dotnet add package Soenneker.Reddit.Ads.Conversions
```

## Usage

Register the conversions service and its client dependencies:

```csharp
using Soenneker.Reddit.Ads.Conversions.Registrars;

services.AddRedditAdsConversionsAsSingleton();
```

Set `Reddit:Ads:AccessToken` (environment variable `Reddit__Ads__AccessToken`) to a
conversion-capable bearer token. Inject `IRedditAdsConversions` from
`Soenneker.Reddit.Ads.Conversions.Abstract`:

```csharp
using Soenneker.Reddit.Ads.OpenApiClient.Models;

var purchase = conversions.CreateEvent(
    StandardTrackingType.Purchase,
    conversionId: "order-123", // Reuse for retries and the browser Pixel event.
    occurredAt: DateTimeOffset.UtcNow);

purchase.User = conversions.CreateUser(
    email: "customer@example.com",
    phoneNumber: "+15554441234",
    externalId: "customer-123");
purchase.ClickId = redditClickId;
purchase.EventSourceUrl = "https://example.com/checkout/complete";
purchase.Metadata!.Currency = "USD";
purchase.Metadata.Value = 49.95;
purchase.Metadata.OrderId = "order-123";

var response = await conversions.Send(pixelId, purchase,
    cancellationToken: cancellationToken);
```

Use `StandardTrackingType.Lead` or `SignUp` for those events. Custom events use
`StandardTrackingType.Custom` with `customEventName`. `CreateEvent` defaults to
`Website`; pass another action source for app or offline activity. Generated models
expose the remaining fields, such as products, IP address, user agent, and device IDs.

## Identifiers and delivery

`CreateUser` accepts raw email, phone, and external ID and returns SHA-256 hashes.
It applies Reddit's email canonicalization (lowercase, remove local-part dots and
plus suffix), and phone canonicalization (country code, leading plus, no extension).
Phone inputs must already include their country code; the library cannot infer it.
External IDs are hashed exactly as supplied.

For identifiers that are already hashed, construct a
`ComponentsSchemaPixelConversionEventUser` directly. `Send` and `SendBatch` preserve
user data without hashing it again. They also preserve timestamps and conversion IDs.
Other fields, including click IDs and IP addresses, can be assigned directly.

`SendBatch` accepts 1–1000 prepared events per request. It validates basic required
fields and batch size before obtaining the client. API errors and cancellation are
propagated. There is no automatic batch splitting or additional retry loop; callers
control retry policy and must reuse the original conversion IDs.

Pass the Events Manager `testId` to `Send` or `SendBatch` for test events. Ordinary
calls omit it. Token acquisition and refresh remain the application's responsibility,
following the lifetime of the underlying OpenApiClientUtil.

See Reddit's [direct integration guide](https://ads-api.reddit.com/docs/v3/guides/programs/capi/direct-integration)
for identifier rules, event testing, and delivery requirements.

## Local development

Normal builds use the `Soenneker.Reddit.Ads.OpenApiClientUtil` NuGet package. With the
companion repositories checked out alongside this repository, use:

```sh
dotnet build -p:UseLocalRedditAdsProjects=true
dotnet test --project test/Soenneker.Reddit.Ads.Conversions.Tests -p:UseLocalRedditAdsProjects=true -- --treenode-filter "/*/*/ConversionPayloadTests/*"
```

The focused tests use an in-memory HTTP handler and never send events to Reddit.
