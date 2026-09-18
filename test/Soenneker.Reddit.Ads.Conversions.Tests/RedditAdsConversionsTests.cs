using Soenneker.Reddit.Ads.Conversions.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Reddit.Ads.Conversions.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class RedditAdsConversionsTests : HostedUnitTest
{
    private readonly IRedditAdsConversions _util;

    public RedditAdsConversionsTests(Host host) : base(host)
    {
        _util = Resolve<IRedditAdsConversions>(true);
    }

    [Test]
    public void Default()
    {

    }
}
