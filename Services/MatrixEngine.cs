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
    {
        return _procedures.Where(p => Matches(p, selections));
    }

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

    /// <summary>
    /// Returns all values for <paramref name="dimension"/> that still leave
    /// at least one matching procedure when combined with the other current selections.
    /// </summary>
    public List<string> GetValidValues(string dimension, Dictionary<string, string> currentSelections, IEnumerable<string> allPossible)
    {
        var valid = new List<string>();
        foreach (var candidate in allPossible)
        {
            var test = new Dictionary<string, string>(currentSelections, StringComparer.OrdinalIgnoreCase)
            {
                [dimension] = candidate
            };

            // A value is valid if ANY procedure is still compatible
            // (procedure conditions that are present in test must match;
            //  missing keys in the procedure are fine – partial match)
            if (_procedures.Any(p => IsCompatible(p, test)))
                valid.Add(candidate);
        }
        return valid;
    }

    /// <summary>
    /// Soft match: every key that exists in BOTH the procedure conditions AND the selections must agree.
    /// Keys only in the procedure (not yet selected) are ignored.
    /// </summary>
    private static bool IsCompatible(Procedure procedure, Dictionary<string, string> selections)
    {
        foreach (var (key, expected) in procedure.Conditions)
        {
            if (!selections.TryGetValue(key, out var actual))
                continue; // not yet chosen – still possible
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
            {
                timeValue = step.FixedTime;
            }
            else if (!string.IsNullOrEmpty(step.TimeKey) && times.TryGetValue(step.TimeKey, out var t) && !string.IsNullOrWhiteSpace(t))
            {
                timeValue = t.Trim();
            }
            else
            {
                timeValue = "—";
            }

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

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time))
            return true;

        return false;
    }

    public static string FormatTime(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";

    public WorkTimeSummary CalculateWorkTime(
        List<(string Action, string Time, string? Comment)> bookings,
        TimeSpan sollzeit)
    {
        var summary = new WorkTimeSummary { Sollzeit = sollzeit };

        var intervals = new List<(TimeSpan Start, TimeSpan End)>();
        TimeSpan? openStart = null;

        foreach (var (action, timeStr, _) in bookings)
        {
            if (!TryParseTime(timeStr, out var t)) continue;

            var isStart = IsStartAction(action);
            var isEnd = IsEndAction(action);

            if (isStart)
            {
                openStart = t;
            }
            else if (isEnd && openStart.HasValue)
            {
                if (t > openStart.Value)
                    intervals.Add((openStart.Value, t));
                openStart = null;
            }
        }

        var total = TimeSpan.Zero;
        foreach (var (s, e) in intervals)
            total += e - s;

        summary.Gearbeitet = total;
        summary.Differenz = total - sollzeit;
        summary.Exceeds10h = total > TimeSpan.FromHours(10);
        summary.Intervals = intervals;

        return summary;
    }

    private static bool IsStartAction(string action) =>
        action.Contains("Kommen", StringComparison.OrdinalIgnoreCase) ||
        action.Contains("Beginn", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("Mob. Arbeiten Beginn", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("Fortbildung Beginn", StringComparison.OrdinalIgnoreCase);

    private static bool IsEndAction(string action) =>
        action.Contains("Gehen", StringComparison.OrdinalIgnoreCase) ||
        action.Contains("Ende", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("Mob. Arbeiten Ende", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("Dienstgang (Ende außerhalb)", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("Fortbildung Ende", StringComparison.OrdinalIgnoreCase);
}

public class WorkTimeSummary
{
    public TimeSpan Gearbeitet { get; set; }
    public TimeSpan Sollzeit { get; set; }
    public TimeSpan Differenz { get; set; }
    public bool Exceeds10h { get; set; }
    public List<(TimeSpan Start, TimeSpan End)> Intervals { get; set; } = new();

    public string GearbeitetText => MatrixEngine.FormatTime(Gearbeitet);
    public string SollzeitText => MatrixEngine.FormatTime(Sollzeit);
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
