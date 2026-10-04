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
    private static readonly Regex VacHostRx = new("VacationHost=([^\r\n]*)", Opt);
    private static readonly Regex VacKeyRx = new("VacationKey=([^\r\n]*)", Opt);

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

    /// <summary>Host of the optional vacation mode (ssh); empty = the feature is off. Edited by hand.</summary>
    public string VacationHost { get; set; } = "";

    /// <summary>Path of the ssh key of the vacation mode. Edited by hand.</summary>
    public string VacationKey { get; set; } = "";

    /// <summary>Whether the vacation mode is set up (both keys present and usable).</summary>
    public bool VacationEnabled => Vacation.IsConfigured(VacationHost, VacationKey);

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
        m = VacHostRx.Match(cfg);
        if (m.Success) s.VacationHost = m.Groups[1].Value.Trim();
        m = VacKeyRx.Match(cfg);
        if (m.Success) s.VacationKey = m.Groups[1].Value.Trim();
        return s;
    }

    /// <summary>The lines of settings.txt.</summary>
    public IReadOnlyList<string> ToLines()
    {
        var lines = new List<string>
        {
            "TopMost=" + (TopMost ? "True" : "False"),
            "Units=" + (ShowBits ? "bits" : "bytes"),
            "Period=" + Period.ToString(CultureInfo.InvariantCulture),
            "StallAlertMin=" + StallAlertMin.ToString(CultureInfo.InvariantCulture),
            "Schedule=" + (Schedule?.Text ?? ""),
        };
        // Hand-edited, kept as found: written back only when present, so a plain file stays plain
        if (VacationHost.Length > 0) lines.Add("VacationHost=" + VacationHost);
        if (VacationKey.Length > 0) lines.Add("VacationKey=" + VacationKey);
        return lines;
    }

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
            // UTF-8 without BOM: the same bytes as ASCII for the plain keys, and a key path with an accent
            // (a user profile name) survives the round trip
            File.WriteAllText(path, ToFileText(), new UTF8Encoding(false));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
