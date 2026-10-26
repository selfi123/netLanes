using Microsoft.Extensions.Time.Testing;
using NetLanes.Core.RateLimiting;
using Xunit;

namespace NetLanes.Core.Tests;

public class TokenBucketTests
{
    [Fact]
    public void UnlimitedLimiter_AlwaysConsumesInstantly()
    {
        var limiter = TokenBucketRateLimiter.CreateUnlimited();

        bool consumed1 = limiter.TryConsume(1_000_000, out var delay1);
        bool consumed2 = limiter.TryConsume(50_000_000, out var delay2);

        Assert.True(consumed1);
        Assert.Equal(TimeSpan.Zero, delay1);
        Assert.True(consumed2);
        Assert.Equal(TimeSpan.Zero, delay2);
        Assert.True(limiter.IsUnlimited);
    }

    [Fact]
    public void InitialBurst_HonoredUpToCapacity()
    {
        var fakeTime = new FakeTimeProvider();
        long rateBps = 1024 * 1024; // 1 MiB/s
        long capacity = 2 * 1024 * 1024; // 2 MiB capacity

        var limiter = new TokenBucketRateLimiter(rateBps, capacity, fakeTime);

        // Can immediately consume up to capacity
        bool burstOk = limiter.TryConsume(capacity, out var delay);
        Assert.True(burstOk);
        Assert.Equal(TimeSpan.Zero, delay);

        // Immediate next packet should fail and return required delay
        bool nextOk = limiter.TryConsume(512 * 1024, out var nextDelay);
        Assert.False(nextOk);
        Assert.True(nextDelay.TotalSeconds >= 0.49 && nextDelay.TotalSeconds <= 0.51);
    }

    [Fact]
    public void ControlledTimeProvider_RefillsTokensAccurately()
    {
        var fakeTime = new FakeTimeProvider();
        long rateBps = 1_000_000; // 1 MB/s
        long capacity = 1_000_000;

        var limiter = new TokenBucketRateLimiter(rateBps, capacity, fakeTime);

        // Drain the bucket completely
        Assert.True(limiter.TryConsume(capacity, out _));
        Assert.False(limiter.TryConsume(100_000, out var neededDelay));
        Assert.True(neededDelay.TotalSeconds > 0);

        // Advance fake clock by 500 milliseconds
        fakeTime.Advance(TimeSpan.FromMilliseconds(500));

        // Exactly 500,000 tokens should have refilled
        double available = limiter.GetAvailableTokens();
        Assert.InRange(available, 490_000, 510_000);

        // Can consume 400,000 bytes now
        Assert.True(limiter.TryConsume(400_000, out var zeroDelay));
        Assert.Equal(TimeSpan.Zero, zeroDelay);

        // Cannot consume another 200,000
        Assert.False(limiter.TryConsume(200_000, out _));
    }

    [Fact]
    public void UpdateRate_DynamicallyAdjustsRefillRateAndCapacity()
    {
        var fakeTime = new FakeTimeProvider();
        var limiter = new TokenBucketRateLimiter(1_000_000, 1_000_000, fakeTime);

        limiter.UpdateRate(5_000_000, 5_000_000);

        Assert.Equal(5_000_000, limiter.RefillRateBytesPerSecond);
        Assert.Equal(5_000_000, limiter.CapacityBytes);
    }

    [Fact]
    public void Consume_CalculatesCorrectDeficitDelay()
    {
        var fakeTime = new FakeTimeProvider();
        long rateBps = 1_000_000; // 1 MB/s
        long capacity = 500_000;

        var limiter = new TokenBucketRateLimiter(rateBps, capacity, fakeTime);

        // Consume 500k: available is 0, delay is 0
        var delay1 = limiter.Consume(500_000);
        Assert.Equal(TimeSpan.Zero, delay1);

        // Consume another 500k: available is -500k, delay is 0.5s
        var delay2 = limiter.Consume(500_000);
        Assert.InRange(delay2.TotalSeconds, 0.49, 0.51);
    }
}
