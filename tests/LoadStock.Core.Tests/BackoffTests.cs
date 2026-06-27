using LoadStock.Core.Polling;

namespace LoadStock.Core.Tests;

public class BackoffTests
{
    [Fact]
    public void Success_returns_base_interval()
        => Assert.Equal(300, Backoff.NextDelaySeconds(300, 0, 3600));

    [Theory]
    [InlineData(1, 600)]
    [InlineData(2, 1200)]
    [InlineData(3, 2400)]
    public void Grows_exponentially(int failCount, int expected)
        => Assert.Equal(expected, Backoff.NextDelaySeconds(300, failCount, 3600));

    [Fact]
    public void Capped_at_max()
        => Assert.Equal(3600, Backoff.NextDelaySeconds(300, 10, 3600));

    [Fact]
    public void Base_above_max_is_capped()
        => Assert.Equal(3600, Backoff.NextDelaySeconds(5000, 0, 3600));
}
