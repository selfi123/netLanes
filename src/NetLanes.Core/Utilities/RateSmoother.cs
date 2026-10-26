namespace NetLanes.Core.Utilities;

/// <summary>
/// Exponential Moving Average (EMA) smoother for real-time traffic rates.
/// Prevents sudden rate jumps and UI flicker while reacting swiftly to sustained changes.
/// </summary>
public class RateSmoother
{
    private readonly double _alpha;
    private double _currentSmoothed;
    private bool _initialized;

    public double CurrentRate => _currentSmoothed;

    /// <param name="alpha">Smoothing factor between 0.0 (maximum smoothing) and 1.0 (no smoothing). Default: 0.35</param>
    public RateSmoother(double alpha = 0.35)
    {
        _alpha = Math.Clamp(alpha, 0.05, 1.0);
    }

    public double Sample(double instantaneousRate)
    {
        if (double.IsNaN(instantaneousRate) || double.IsInfinity(instantaneousRate) || instantaneousRate < 0)
        {
            instantaneousRate = 0;
        }

        if (!_initialized)
        {
            _currentSmoothed = instantaneousRate;
            _initialized = true;
            return _currentSmoothed;
        }

        // EMA formula: S_t = alpha * Y_t + (1 - alpha) * S_{t-1}
        _currentSmoothed = (_alpha * instantaneousRate) + ((1.0 - _alpha) * _currentSmoothed);

        // Snap near-zero values to absolute zero to avoid micro-tails
        if (_currentSmoothed < 1.0)
        {
            _currentSmoothed = 0.0;
        }

        return _currentSmoothed;
    }

    public void Reset()
    {
        _currentSmoothed = 0.0;
        _initialized = false;
    }
}
