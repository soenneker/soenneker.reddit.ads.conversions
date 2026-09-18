using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Reddit.Ads.OpenApiClient.Models;

namespace Soenneker.Reddit.Ads.Conversions.Abstract;

/// <summary>
/// A utility for sending Reddit Ads conversion events.
/// </summary>
public interface IRedditAdsConversions
{
    /// <summary>Creates an event with a Unix-millisecond timestamp and a caller-supplied deduplication ID.
    /// Reuse the same ID for retries and the corresponding browser Pixel event. Defaults to WEBSITE and the current UTC time.
    /// Add user identifiers, click ID, URLs, and purchase metadata to the returned model before sending.</summary>
    ComponentsSchemaPixelConversionEvent CreateEvent(StandardTrackingType trackingType, string conversionId,
        DateTimeOffset? occurredAt = null, ComponentsSchemaPixelConversionEventActionSource actionSource = ComponentsSchemaPixelConversionEventActionSource.Website,
        string? customEventName = null);

    /// <summary>Creates match data from raw identifiers, hashing email, phone, and external ID with SHA-256.
    /// Email is canonicalized according to Reddit's rules; phone must include a country code (extensions are removed).
    /// Do not pass prehashed identifiers here; assign them directly to the generated user model instead.</summary>
    ComponentsSchemaPixelConversionEventUser CreateUser(string? email = null, string? phoneNumber = null, string? externalId = null);

    /// <summary>Sends one event to the specified Pixel. Optional testId routes the request to Reddit's event testing flow.
    /// Prepared identifiers are sent unchanged. Exceptions and cancellation propagate to the caller.</summary>
    ValueTask<PostConversionEvents200Response?> Send(string pixelId, ComponentsSchemaPixelConversionEvent conversion,
        string? testId = null, CancellationToken cancellationToken = default);

    /// <summary>Sends 1–1000 events in a single request, preserving supplied IDs, timestamps, and match data.
    /// Does not automatically retry or split batches. Use stable conversion IDs when retrying.</summary>
    ValueTask<PostConversionEvents200Response?> SendBatch(string pixelId, IReadOnlyCollection<ComponentsSchemaPixelConversionEvent> conversions,
        string? testId = null, CancellationToken cancellationToken = default);
}
