namespace NetLanes.Core.RateLimiting;

/// <summary>
/// High-precision token bucket rate limiter with microsecond resolution
/// and controllable TimeProvider for deterministic testing.
/// </summary>
public class TokenBucketRateLimiter
{
    private readonly object _syncRoot = new();
    private readonly TimeProvider _timeProvider;
    private long _refillRateBytesPerSecond;
    private long _capacityBytes;
    private double _availableTokens;
    private long _lastRefillTimestamp;
    private readonly bool _isUnlimited;

    public long RefillRateBytesPerSecond => _refillRateBytesPerSecond;
    public long CapacityBytes => _capacityBytes;
    public bool IsUnlimited => _isUnlimited;

    public TokenBucketRateLimiter(
        long refillRateBytesPerSecond,
        long? capacityBytes = null,
        TimeProvider? timeProvider = null,
        bool isUnlimited = false)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _isUnlimited = isUnlimited || refillRateBytesPerSecond <= 0;
        _refillRateBytesPerSecond = Math.Max(0, refillRateBytesPerSecond);
        
        // Default capacity: 1.5 seconds of burst allowance, minimum 64 KB (handles standard MTU bursts)
        long defaultCapacity = Math.Max(64 * 1024, (long)(_refillRateBytesPerSecond * 1.5));
        _capacityBytes = capacityBytes.HasValue && capacityBytes.Value > 0 ? capacityBytes.Value : defaultCapacity;
        
        _availableTokens = _capacityBytes;
        _lastRefillTimestamp = _timeProvider.GetTimestamp();
    }

    public static TokenBucketRateLimiter CreateUnlimited(TimeProvider? timeProvider = null)
    {
        return new TokenBucketRateLimiter(0, null, timeProvider, isUnlimited: true);
    }

    /// <summary>
    /// Attempts to consume the specified number of bytes.
    /// If sufficient tokens exist, consumes them and returns true with delay = Zero.
    /// Otherwise returns false with the calculated delay until sufficient tokens will be available.
    /// </summary>
    public bool TryConsume(long byteCount, out TimeSpan delay)
    {
        if (_isUnlimited || _refillRateBytesPerSecond <= 0)
        {
            delay = TimeSpan.Zero;
            return true;
        }

        lock (_syncRoot)
        {
            RefillTokens();

            if (_availableTokens >= byteCount)
            {
                _availableTokens -= byteCount;
                delay = TimeSpan.Zero;
                return true;
            }

            // Calculate deficit and time needed to accumulate the missing tokens
            double deficit = byteCount - _availableTokens;
            double secondsNeeded = deficit / _refillRateBytesPerSecond;
            delay = TimeSpan.FromSeconds(Math.Max(0.001, secondsNeeded));
            return false;
        }
    }

    /// <summary>
    /// Consumes tokens directly, allowing the bucket to go negative for pacing calculations.
    /// Returns the delay incurred before subsequent packets can be processed.
    /// </summary>
    public TimeSpan Consume(long byteCount)
    {
        if (_isUnlimited || _refillRateBytesPerSecond <= 0)
        {
            return TimeSpan.Zero;
        }

        lock (_syncRoot)
        {
            RefillTokens();

            _availableTokens -= byteCount;
            if (_availableTokens >= 0)
            {
                return TimeSpan.Zero;
            }

            double deficit = -_availableTokens;
            double secondsNeeded = deficit / _refillRateBytesPerSecond;
            return TimeSpan.FromSeconds(Math.Max(0.001, secondsNeeded));
        }
    }

    public void UpdateRate(long newRateBps, long? newCapacityBytes = null)
    {
        lock (_syncRoot)
        {
            RefillTokens();
            _refillRateBytesPerSecond = Math.Max(0, newRateBps);
            if (newCapacityBytes.HasValue && newCapacityBytes.Value > 0)
            {
                _capacityBytes = newCapacityBytes.Value;
            }
            else
            {
                _capacityBytes = Math.Max(64 * 1024, (long)(_refillRateBytesPerSecond * 1.5));
            }

            if (_availableTokens > _capacityBytes)
            {
                _availableTokens = _capacityBytes;
            }
        }
    }

    private void RefillTokens()
    {
        long currentTimestamp = _timeProvider.GetTimestamp();
        double elapsedSeconds = _timeProvider.GetElapsedTime(_lastRefillTimestamp, currentTimestamp).TotalSeconds;
        _lastRefillTimestamp = currentTimestamp;

        if (elapsedSeconds > 0 && _refillRateBytesPerSecond > 0)
        {
            double newTokens = elapsedSeconds * _refillRateBytesPerSecond;
            _availableTokens = Math.Min(_capacityBytes, _availableTokens + newTokens);
        }
    }

    public double GetAvailableTokens()
    {
        lock (_syncRoot)
        {
            RefillTokens();
            return _availableTokens;
        }
    }
}
