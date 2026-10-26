using System.Globalization;

namespace NetLanes.Core.Utilities;

public static class UnitConverter
{
    public const long KiB = 1024L;
    public const long MiB = 1024L * 1024L;
    public const long GiB = 1024L * 1024L * 1024L;

    public const long KB = 1000L;
    public const long MB = 1000L * 1000L;
    public const long GB = 1000L * 1000L * 1000L;

    public static readonly string[] SupportedUnits =
    [
        "MiB/s",
        "MB/s",
        "Mbps",
        "KiB/s",
        "KB/s",
        "Kbps",
        "B/s"
    ];

    public static string FormatRate(double bytesPerSec, string unit = "MiB/s")
    {
        if (bytesPerSec <= 0 || double.IsNaN(bytesPerSec) || double.IsInfinity(bytesPerSec))
        {
            return $"0.00 {unit}";
        }

        double val = unit switch
        {
            "MiB/s" => bytesPerSec / MiB,
            "MB/s"  => bytesPerSec / MB,
            "Mbps"  => (bytesPerSec * 8) / MB,
            "KiB/s" => bytesPerSec / KiB,
            "KB/s"  => bytesPerSec / KB,
            "Kbps"  => (bytesPerSec * 8) / KB,
            "B/s"   => bytesPerSec,
            _       => bytesPerSec / MiB
        };

        return string.Format(CultureInfo.InvariantCulture, "{0:0.00} {1}", val, unit);
    }

    public static string FormatAdaptiveRate(double bytesPerSec)
    {
        if (bytesPerSec <= 0 || double.IsNaN(bytesPerSec) || double.IsInfinity(bytesPerSec))
        {
            return "0 B/s";
        }

        if (bytesPerSec >= GiB)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00} GiB/s", bytesPerSec / GiB);
        }
        if (bytesPerSec >= MiB)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00} MiB/s", bytesPerSec / MiB);
        }
        if (bytesPerSec >= KiB)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} KiB/s", bytesPerSec / KiB);
        }
        return string.Format(CultureInfo.InvariantCulture, "{0:0} B/s", bytesPerSec);
    }

    public static string FormatBytes(long totalBytes)
    {
        if (totalBytes <= 0) return "0 B";

        if (totalBytes >= GiB)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00} GiB", (double)totalBytes / GiB);
        }
        if (totalBytes >= MiB)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} MiB", (double)totalBytes / MiB);
        }
        if (totalBytes >= KiB)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0} KiB", (double)totalBytes / KiB);
        }
        return $"{totalBytes} B";
    }

    public static long ParseRateToBps(double value, string unit)
    {
        if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        double bps = unit switch
        {
            "MiB/s" => value * MiB,
            "MB/s"  => value * MB,
            "Mbps"  => (value * MB) / 8.0,
            "KiB/s" => value * KiB,
            "KB/s"  => value * KB,
            "Kbps"  => (value * KB) / 8.0,
            "B/s"   => value,
            _       => value * MiB
        };

        return (long)Math.Round(bps);
    }
}
