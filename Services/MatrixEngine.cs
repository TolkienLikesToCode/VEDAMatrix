using System.Globalization;
using VedaMatrix.Models;

namespace VedaMatrix.Services;

public class MatrixEngine
{
    private readonly List<Procedure> _procedures;

    public MatrixEngine(IEnumerable<Procedure> procedures)
    {
        _procedures = procedures.ToList();
    }

    public IEnumerable<Procedure> FindMatching(Dictionary<string, string> selections)
        => _procedures.Where(p => Matches(p, selections));

    public Procedure? FindBestMatch(Dictionary<string, string> selections)
    {
        var matches = FindMatching(selections).ToList();
        if (matches.Count == 0) return null;
        return matches.OrderByDescending(m => m.Conditions.Count).First();
    }

    private static bool Matches(Procedure procedure, Dictionary<string, string> selections)
    {
        foreach (var (key, expected) in procedure.Conditions)
        {
            if (!selections.TryGetValue(key, out var actual))
                return false;
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    public List<(string Action, string Time, string? Comment)> GenerateBookings(
        Procedure procedure,
        Dictionary<string, string> times)
    {
        var result = new List<(string Action, string Time, string? Comment)>();

        foreach (var step in procedure.Steps)
        {
            string timeValue;
            if (!string.IsNullOrEmpty(step.FixedTime))
                timeValue = step.FixedTime;
            else if (!string.IsNullOrEmpty(step.TimeKey) && times.TryGetValue(step.TimeKey, out var t) && !string.IsNullOrWhiteSpace(t))
                timeValue = t.Trim();
            else
                timeValue = "—";

            result.Add((step.Action, timeValue, step.Comment));
        }

        ApplyMinuteCollisions(result);
        return result;
    }

    private static void ApplyMinuteCollisions(List<(string Action, string Time, string? Comment)> bookings)
    {
        TimeSpan? lastTime = null;

        for (int i = 0; i < bookings.Count; i++)
        {
            var (action, timeStr, comment) = bookings[i];

            if (!TryParseTime(timeStr, out var current))
            {
                lastTime = null;
                continue;
            }

            if (lastTime.HasValue && current <= lastTime.Value)
            {
                current = lastTime.Value.Add(TimeSpan.FromMinutes(1));
                var adjusted = FormatTime(current);
                var note = string.IsNullOrEmpty(comment)
                    ? "+1 Min (Kollisionsvermeidung)"
                    : comment + "  ·  +1 Min (Kollisionsvermeidung)";
                bookings[i] = (action, adjusted, note);
            }

            lastTime = current;
        }
    }

    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text) || text == "—") return false;

        text = text.Trim().Replace('.', ':');

        if (TimeSpan.TryParseExact(text, new[] { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" },
                CultureInfo.InvariantCulture, out time))
            return true;

        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time);
    }

    public static string FormatTime(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";

    /// <summary>
    /// Working time = everything that is not a pause.
    /// Dienstgang, Fortbildung, mobiles Arbeiten, normal office time all count.
    /// Only explicit pause intervals (Gehen→Kommen / Mob.Ende→Mob.Beginn in the middle of the day) are subtracted.
    /// </summary>
    public WorkTimeSummary CalculateWorkTime(
        List<(string Action, string Time, string? Comment)> bookings,
        TimeSpan sollzeit)
    {
        var summary = new WorkTimeSummary { Sollzeit = sollzeit };

        // Collect timed events in order
        var events = new List<(TimeSpan Time, string Action)>();
        foreach (var (action, timeStr, _) in bookings)
        {
            if (TryParseTime(timeStr, out var t))
                events.Add((t, action));
        }

        if (events.Count == 0)
            return summary;

        // Day span: first → last timestamp
        var dayStart = events[0].Time;
        var dayEnd = events[^1].Time;
        if (dayEnd < dayStart)
            return summary; // invalid

        var daySpan = dayEnd - dayStart;

        // Pause intervals: end-of-presence followed by resume, not the final end-of-day
        // Pause start: Gehen, Mob. Arbeiten Ende
        // Pause end (resume): Kommen, Mob. Arbeiten Beginn, Dienstgang Beginn (after pause at Schulung)
        var pauseTotal = TimeSpan.Zero;
        var pauseIntervals = new List<(TimeSpan Start, TimeSpan End)>();

        for (int i = 0; i < events.Count - 1; i++)
        {
            var (t1, a1) = events[i];
            var (t2, a2) = events[i + 1];

            if (!IsPauseStart(a1)) continue;
            if (!IsPauseResume(a2)) continue;

            // Skip if this Gehen/Mob.Ende is the last event overall (end of day)
            // Already guarded by i < Count-1, but also skip if no real resume work after
            if (t2 <= t1) continue;

            pauseTotal += t2 - t1;
            pauseIntervals.Add((t1, t2));
        }

        var worked = daySpan - pauseTotal;
        if (worked < TimeSpan.Zero)
            worked = TimeSpan.Zero;

        summary.Gearbeitet = worked;
        summary.Differenz = worked - sollzeit;
        summary.Exceeds10h = worked > TimeSpan.FromHours(10);
        summary.Intervals = pauseIntervals; // for transparency: these are the excluded pauses
        summary.PauseTotal = pauseTotal;
        summary.DayStart = dayStart;
        summary.DayEnd = dayEnd;

        return summary;
    }

    private static bool IsPauseStart(string action) =>
        action.Equals("Gehen", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("Mob. Arbeiten Ende", StringComparison.OrdinalIgnoreCase);

    private static bool IsPauseResume(string action) =>
        action.Equals("Kommen", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("Mob. Arbeiten Beginn", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("Dienstgang Beginn", StringComparison.OrdinalIgnoreCase);
}

public class WorkTimeSummary
{
    public TimeSpan Gearbeitet { get; set; }
    public TimeSpan Sollzeit { get; set; }
    public TimeSpan Differenz { get; set; }
    public TimeSpan PauseTotal { get; set; }
    public TimeSpan DayStart { get; set; }
    public TimeSpan DayEnd { get; set; }
    public bool Exceeds10h { get; set; }
    public List<(TimeSpan Start, TimeSpan End)> Intervals { get; set; } = new();

    public string GearbeitetText => MatrixEngine.FormatTime(Gearbeitet);
    public string SollzeitText => MatrixEngine.FormatTime(Sollzeit);
    public string PauseText => MatrixEngine.FormatTime(PauseTotal);
    public string DifferenzText
    {
        get
        {
            var sign = Differenz >= TimeSpan.Zero ? "+" : "−";
            var abs = Differenz.Duration();
            return $"{sign}{MatrixEngine.FormatTime(abs)}";
        }
    }
}
