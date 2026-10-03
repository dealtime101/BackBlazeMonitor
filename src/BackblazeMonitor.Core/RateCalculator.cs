namespace BackblazeMonitor.Core;

/// <summary>Current rate, "is it sending" and gap detection over the window points.</summary>
public static class RateCalculator
{
    /// <summary>
    /// Current rate in bits/s. The log gives the rate of ONE block, ONE thread, and Backblaze runs several
    /// threads: the bytes completed within the last 2 minutes tell the truth. Nothing completed but a
    /// block still in flight: keep the last block's rate rather than zero (a single slow thread would
    /// otherwise drop to zero between two lines).
    /// </summary>
    /// <param name="points">Window points, sorted by time.</param>
    public static double GetCurrentRate(IReadOnlyList<LogBlock> points, DateTime now)
    {
        var cut = now.AddSeconds(-BzConstants.RateSec);
        var octets = 0.0;
        foreach (var p in points)
        {
            if (p.Time >= cut) octets += p.Bytes;
        }

        if (octets > 0) return 8 * octets / BzConstants.RateSec;
        if (points.Count > 0) return points[^1].Bits;
        return 0.0;
    }

    /// <summary>
    /// Sending is in progress as long as the next line may still arrive: <see cref="BzConstants.IdleSec"/>,
    /// or twice the in-flight time of the last block.
    /// </summary>
    public static bool IsSending(LogBlock last, DateTime now) =>
        (now - last.Time).TotalSeconds <= Math.Max(BzConstants.IdleSec, 2 * last.InFlightSeconds);

    /// <summary>
    /// Gap in transmissions: the interval between two lines, excluding the in-flight time of the following
    /// block, exceeds <see cref="BzConstants.GapSec"/>.
    /// </summary>
    public static bool IsHole(DateTime prevTime, LogBlock p) =>
        (p.Time - prevTime).TotalSeconds - p.InFlightSeconds > BzConstants.GapSec;
}
