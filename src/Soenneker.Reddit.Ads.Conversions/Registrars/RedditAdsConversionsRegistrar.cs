using Microsoft.Extensions.DependencyInjection;
using Soenneker.Reddit.Ads.OpenApiClientUtil.Registrars;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Reddit.Ads.Conversions.Abstract;

namespace Soenneker.Reddit.Ads.Conversions.Registrars;

/// <summary>
/// A utility for sending Reddit Ads conversion events.
/// </summary>
public static class RedditAdsConversionsRegistrar
{
    /// <summary>
    /// Adds <see cref="IRedditAdsConversions"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddRedditAdsConversionsAsSingleton(this IServiceCollection services)
    {
        services.AddRedditAdsOpenApiClientUtilAsSingleton();
        services.TryAddSingleton<IRedditAdsConversions, RedditAdsConversions>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IRedditAdsConversions"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddRedditAdsConversionsAsScoped(this IServiceCollection services)
    {
        services.AddRedditAdsOpenApiClientUtilAsScoped();
        services.TryAddScoped<IRedditAdsConversions, RedditAdsConversions>();

        return services;
    }
}
