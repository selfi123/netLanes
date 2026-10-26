using NetLanes.Core.Utilities;
using Xunit;

namespace NetLanes.Core.Tests;

public class UnitConverterTests
{
    [Theory]
    [InlineData(1048576, "MiB/s", "1.00 MiB/s")]
    [InlineData(52428800, "MiB/s", "50.00 MiB/s")]
    [InlineData(1000000, "MB/s", "1.00 MB/s")]
    [InlineData(1250000, "Mbps", "10.00 Mbps")]
    [InlineData(1024, "KiB/s", "1.00 KiB/s")]
    [InlineData(0, "MiB/s", "0.00 MiB/s")]
    [InlineData(-500, "MiB/s", "0.00 MiB/s")]
    public void FormatRate_ProducesExpectedString(double bytesPerSec, string unit, string expected)
    {
        string result = UnitConverter.FormatRate(bytesPerSec, unit);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1.0, "MiB/s", 1048576)]
    [InlineData(35.0, "MiB/s", 36700160)]
    [InlineData(1.0, "MB/s", 1000000)]
    [InlineData(10.0, "Mbps", 1250000)]
    [InlineData(1.0, "KiB/s", 1024)]
    [InlineData(0, "MiB/s", 0)]
    public void ParseRateToBps_CalculatesAccurateByteRate(double val, string unit, long expectedBps)
    {
        long bps = UnitConverter.ParseRateToBps(val, unit);
        Assert.Equal(expectedBps, bps);
    }

    [Theory]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1 KiB")]
    [InlineData(1048576, "1.0 MiB")]
    [InlineData(1073741824, "1.00 GiB")]
    [InlineData(0, "0 B")]
    public void FormatBytes_ProducesExpectedUnits(long totalBytes, string expectedPrefix)
    {
        string result = UnitConverter.FormatBytes(totalBytes);
        Assert.StartsWith(expectedPrefix, result);
    }

    [Fact]
    public void FormatAdaptiveRate_HandlesSmallAndLargeRates()
    {
        Assert.Equal("0 B/s", UnitConverter.FormatAdaptiveRate(0));
        Assert.Equal("500 B/s", UnitConverter.FormatAdaptiveRate(500));
        Assert.Equal("5.0 KiB/s", UnitConverter.FormatAdaptiveRate(5120));
        Assert.Equal("10.00 MiB/s", UnitConverter.FormatAdaptiveRate(10485760));
    }
}
