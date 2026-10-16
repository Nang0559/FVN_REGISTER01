using FVN_REGISTER.Core.Constants;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class BackgroundJobCatalogTests
{
    [Fact]
    public void Keys_AreUnique_AndFindIsCaseInsensitive()
    {
        var keys = BackgroundJobCatalog.All.Select(x => x.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.NotNull(BackgroundJobCatalog.Find("HRM-SYNC"));
        Assert.Null(BackgroundJobCatalog.Find("does-not-exist"));
    }

    [Fact]
    public void DefaultIntervals_AreWithinAllowedRange()
    {
        Assert.All(BackgroundJobCatalog.All, job =>
            Assert.InRange(job.DefaultIntervalMinutes,
                BackgroundJobCatalog.MinIntervalMinutes,
                BackgroundJobCatalog.MaxIntervalMinutes));
    }

    [Fact]
    public void HrmSync_DefaultsToAtLeastThirtyMinutes()
    {
        Assert.True(BackgroundJobCatalog.Find(BackgroundJobCatalog.HrmSync)!.DefaultIntervalMinutes >= 30);
    }

    [Fact]
    public void SecurityHousekeepingJobs_CannotBeDisabled()
    {
        Assert.False(BackgroundJobCatalog.Find(BackgroundJobCatalog.EndpointCredentialCleanup)!.AllowDisable);
        Assert.False(BackgroundJobCatalog.Find(BackgroundJobCatalog.SecurityFunctionDiscovery)!.AllowDisable);
    }
}
