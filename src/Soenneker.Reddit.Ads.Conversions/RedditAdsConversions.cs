using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Reddit.Ads.Conversions.Abstract;
using Soenneker.Reddit.Ads.OpenApiClient.Models;
using Soenneker.Reddit.Ads.OpenApiClientUtil.Abstract;

namespace Soenneker.Reddit.Ads.Conversions;

public sealed class RedditAdsConversions(IRedditAdsOpenApiClientUtil clientUtil) : IRedditAdsConversions
{
    public ComponentsSchemaPixelConversionEvent CreateEvent(StandardTrackingType trackingType, string conversionId,
        DateTimeOffset? occurredAt = null, ComponentsSchemaPixelConversionEventActionSource actionSource = ComponentsSchemaPixelConversionEventActionSource.Website,
        string? customEventName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversionId);
        var conversion = new ComponentsSchemaPixelConversionEvent
        {
            EventAt = (occurredAt ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds(),
            ActionSource = actionSource,
            Type = new ComponentsSchemaPixelConversionEventType { TrackingType = trackingType, CustomEventName = customEventName },
            Metadata = new ComponentsSchemaPixelConversionEventMetadata { ConversionId = conversionId }
        };
        Validate(conversion);
        return conversion;
    }

    public ComponentsSchemaPixelConversionEventUser CreateUser(string? email = null, string? phoneNumber = null, string? externalId = null) => new()
    {
        Email = email is null ? null : Hash(CanonicalizeEmail(email)),
        PhoneNumber = phoneNumber is null ? null : Hash(CanonicalizePhone(phoneNumber)),
        ExternalId = externalId is null ? null : Hash(RequireIdentifier(externalId))
    };

    public ValueTask<PostConversionEvents200Response?> Send(string pixelId, ComponentsSchemaPixelConversionEvent conversion,
        string? testId = null, CancellationToken cancellationToken = default) => SendBatch(pixelId, [conversion], testId, cancellationToken);

    public async ValueTask<PostConversionEvents200Response?> SendBatch(string pixelId, IReadOnlyCollection<ComponentsSchemaPixelConversionEvent> conversions,
        string? testId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(pixelId);
        ArgumentNullException.ThrowIfNull(conversions);
        if (conversions.Count is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(conversions), "Reddit accepts 1–1000 conversion events per request.");
        foreach (ComponentsSchemaPixelConversionEvent conversion in conversions)
            Validate(conversion);

        var client = await clientUtil.Get(cancellationToken).ConfigureAwait(false);
        return await client.Pixels[pixelId].Conversion_events.PostAsync(new PostConversionEventsRequest
        {
            Data = new PostConversionEventsRequestData { Events = conversions.ToList(), TestId = testId }
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(ComponentsSchemaPixelConversionEvent conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        if (conversion.EventAt is null or <= 0)
            throw new ArgumentException("EventAt must be a positive Unix timestamp in milliseconds.", nameof(conversion));
        if (conversion.ActionSource is null || !Enum.IsDefined(conversion.ActionSource.Value))
            throw new ArgumentException("A valid action source is required.", nameof(conversion));
        if (conversion.Type?.TrackingType is null || !Enum.IsDefined(conversion.Type.TrackingType.Value))
            throw new ArgumentException("A valid tracking type is required.", nameof(conversion));
        if (conversion.Type.TrackingType == StandardTrackingType.Custom)
        {
            string? name = conversion.Type.CustomEventName;
            if (string.IsNullOrWhiteSpace(name) || name.EnumerateRunes().Count() > 64)
                throw new ArgumentException("Custom events require a name of 1–64 characters.", nameof(conversion));
        }
    }

    private static string CanonicalizeEmail(string email)
    {
        email = RequireIdentifier(email).Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out MailAddress? parsed) || parsed.Address != email)
            throw new ArgumentException("Provide an email address without a display name.", nameof(email));
        int separator = email.LastIndexOf('@');
        string local = email[..separator].Split('+')[0].Replace(".", "", StringComparison.Ordinal);
        if (local.Length == 0)
            throw new ArgumentException("The canonical email local part cannot be empty.", nameof(email));
        return local + email[separator..];
    }

    private static string CanonicalizePhone(string phone)
    {
        phone = RequireIdentifier(phone).Trim();
        phone = Regex.Replace(phone, @"\s*(?:ext\.?|extension|x|#)\s*\d+\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (phone.Any(c => !char.IsAsciiDigit(c) && c is not '+' and not ' ' and not '(' and not ')' and not '-' and not '.'))
            throw new ArgumentException("Provide a phone number including its country code.", nameof(phone));
        string digits = new(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 7 or > 15 || digits[0] == '0')
            throw new ArgumentException("Provide a phone number including its country code, with 7–15 digits.", nameof(phone));
        return "+" + digits;
    }

    private static string RequireIdentifier(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
