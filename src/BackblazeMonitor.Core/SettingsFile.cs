using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BackblazeMonitor.Core;

/// <summary>
/// User preferences saved next to the program in settings.txt (ASCII, CR LF):
/// <c>TopMost</c>, <c>Units</c>, <c>Period</c>, <c>StallAlertMin</c>, <c>Schedule</c>.
/// The launch-at-sign-in state is deliberately not here: it is the Startup shortcut itself.
/// </summary>
public sealed class Settings
{
    private static readonly RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex UnitsBytes = new("Units=bytes", Opt);
    private static readonly Regex PeriodRx = new("Period=([0-9])", Opt);
    private static readonly Regex StallRx = new("StallAlertMin=([0-9]+)", Opt);
    private static readonly Regex ScheduleRx = new("Schedule=([^\r\n]*)", Opt);
    private static readonly Regex TopMostRx = new("TopMost=True", Opt);

    /// <summary>Always on top (the pin).</summary>
    public bool TopMost { get; set; }

    /// <summary>true = Mbps (megabits), false = MB/s (megabytes).</summary>
    public bool ShowBits { get; set; } = true;

    /// <summary>Chart period, index into <see cref="Periods.All"/>.</summary>
    public int Period { get; set; }

    /// <summary>Minutes without sending before the alert; 0 = never.</summary>
    public int StallAlertMin { get; set; } = 120;

    /// <summary>Time window in place; <c>null</c> = none.</summary>
    public Schedule? Schedule { get; set; }

    /// <summary>Parses the text of settings.txt; anything missing or invalid keeps its default.</summary>
    public static Settings Parse(string? text)
    {
        var s = new Settings();
        var cfg = text ?? "";
        if (UnitsBytes.IsMatch(cfg)) s.ShowBits = false;
        var m = PeriodRx.Match(cfg);
        if (m.Success) s.Period = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) % Periods.Count;
        m = StallRx.Match(cfg);
        if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var stall)) s.StallAlertMin = stall;
        m = ScheduleRx.Match(cfg);
        if (m.Success) s.Schedule = Schedule.Parse(m.Groups[1].Value);
        if (TopMostRx.IsMatch(cfg)) s.TopMost = true;
        return s;
    }

    /// <summary>The lines of settings.txt.</summary>
    public IReadOnlyList<string> ToLines() => new[]
    {
        "TopMost=" + (TopMost ? "True" : "False"),
        "Units=" + (ShowBits ? "bits" : "bytes"),
        "Period=" + Period.ToString(CultureInfo.InvariantCulture),
        "StallAlertMin=" + StallAlertMin.ToString(CultureInfo.InvariantCulture),
        "Schedule=" + (Schedule?.Text ?? ""),
    };

    /// <summary>The text of settings.txt: lines each ended by CR LF.</summary>
    public string ToFileText() => string.Concat(ToLines().Select(l => l + "\r\n"));

    /// <summary>Loads settings.txt; missing or unreadable gives the defaults.</summary>
    public static Settings Load(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    /// <summary>Writes settings.txt (ASCII). Returns whether it was written.</summary>
    public bool Save(string path)
    {
        try
        {
            File.WriteAllText(path, ToFileText(), Encoding.ASCII);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
