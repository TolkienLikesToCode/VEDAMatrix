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
        Dictionary<string, string> times,
        IReadOnlyList<PauseEntry>? extraPauses = null)
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

        // Determine whether mobile-style pause actions are appropriate
        var useMobilePause = result.Any(b =>
            b.Action.Contains("Mob.", StringComparison.OrdinalIgnoreCase));

        // Inject user-defined extra pauses as Gehen/Kommen (or Mob. Ende/Beginn)
        if (extraPauses is { Count: > 0 })
            InjectExtraPauses(result, extraPauses, useMobilePause);

        // Enforce statutory minimum pause duration (ArbZG-style)
        EnforceMinimumPause(result, useMobilePause);

        ApplyMinuteCollisions(result);
        return result;
    }

    private static void InjectExtraPauses(
        List<(string Action, string Time, string? Comment)> bookings,
        IReadOnlyList<PauseEntry> pauses,
        bool useMobile)
    {
        var go = useMobile ? "Mob. Arbeiten Ende" : "Gehen";
        var come = useMobile ? "Mob. Arbeiten Beginn" : "Kommen";

        foreach (var pause in pauses.OrderBy(p => TryParseTime(p.Start, out var t) ? t : TimeSpan.MaxValue))
        {
            if (!TryParseTime(pause.Start, out var t0) || !TryParseTime(pause.End, out var t1) || t1 <= t0)
                continue;

            // Remove any existing booking exactly at these times that would conflict? Keep simple: insert chronologically
            InsertChronological(bookings, (go, FormatTime(t0), "Zusätzliche Pause (Beginn)"));
            InsertChronological(bookings, (come, FormatTime(t1), "Zusätzliche Pause (Ende)"));
        }
    }

    private static void InsertChronological(
        List<(string Action, string Time, string? Comment)> bookings,
        (string Action, string Time, string? Comment) item)
    {
        if (!TryParseTime(item.Time, out var tNew))
        {
            bookings.Add(item);
            return;
        }

        int idx = bookings.Count;
        for (int i = 0; i < bookings.Count; i++)
        {
            if (!TryParseTime(bookings[i].Time, out var t))
                continue;
            if (t > tNew || (t == tNew && IsPauseStart(bookings[i].Action)))
            {
                idx = i;
                break;
            }
        }
        bookings.Insert(idx, item);
    }

    /// <summary>
    /// If net working time is ≥ 6h, total pause must be ≥ 30 min;
    /// if ≥ 9h, total pause must be ≥ 45 min. Shortfall is added to the last pause
    /// (or a new pause is created around midday if none exist).
    /// </summary>
    private static void EnforceMinimumPause(
        List<(string Action, string Time, string? Comment)> bookings,
        bool useMobile)
    {
        var summary = CalculateWorkTimeInternal(bookings);
        var required = TimeSpan.Zero;
        if (summary.Gearbeitet >= TimeSpan.FromHours(9))
            required = TimeSpan.FromMinutes(45);
        else if (summary.Gearbeitet >= TimeSpan.FromHours(6))
            required = TimeSpan.FromMinutes(30);

        if (required == TimeSpan.Zero)
            return;

        if (summary.PauseTotal >= required)
            return;

        var shortfall = required - summary.PauseTotal;

        // Try to extend the last pause-resume pair
        int lastPauseStartIdx = -1;
        for (int i = bookings.Count - 1; i >= 0; i--)
        {
            if (IsPauseStart(bookings[i].Action))
            {
                lastPauseStartIdx = i;
                break;
            }
        }

        if (lastPauseStartIdx >= 0 && lastPauseStartIdx + 1 < bookings.Count &&
            IsPauseResume(bookings[lastPauseStartIdx + 1].Action) &&
            TryParseTime(bookings[lastPauseStartIdx + 1].Time, out var resumeTime))
        {
            var newResume = resumeTime.Add(shortfall);
            var (a, _, c) = bookings[lastPauseStartIdx + 1];
            var note = string.IsNullOrEmpty(c)
                ? $"+{FormatTime(shortfall)} Mindestpause"
                : c + $"  ·  +{FormatTime(shortfall)} Mindestpause";
            bookings[lastPauseStartIdx + 1] = (a, FormatTime(newResume), note);

            // Shift all later events by shortfall to avoid overlap
            for (int i = lastPauseStartIdx + 2; i < bookings.Count; i++)
            {
                if (!TryParseTime(bookings[i].Time, out var t)) continue;
                var shifted = t.Add(shortfall);
                var (aa, _, cc) = bookings[i];
                bookings[i] = (aa, FormatTime(shifted), cc);
            }
            return;
        }

        // No pause found: insert one starting at day mid-point or after 6h of work
        if (bookings.Count == 0 || !TryParseTime(bookings[0].Time, out var dayStart))
            return;

        var pauseStart = dayStart.Add(TimeSpan.FromHours(3)); // rough default
        var pauseEnd = pauseStart.Add(required);
        var go = useMobile ? "Mob. Arbeiten Ende" : "Gehen";
        var come = useMobile ? "Mob. Arbeiten Beginn" : "Kommen";

        InsertChronological(bookings, (go, FormatTime(pauseStart), "Mindestpause (automatisch)"));
        InsertChronological(bookings, (come, FormatTime(pauseEnd), "Mindestpause (automatisch)"));

        // Shift events after the inserted pause
        for (int i = 0; i < bookings.Count; i++)
        {
            if (!TryParseTime(bookings[i].Time, out var t)) continue;
            if (t > pauseStart && t < pauseEnd && !IsPauseStart(bookings[i].Action) && !IsPauseResume(bookings[i].Action))
            {
                // event fell inside pause window → push after pause
                var (aa, _, cc) = bookings[i];
                bookings[i] = (aa, FormatTime(pauseEnd.Add(TimeSpan.FromMinutes(1))), cc);
            }
            else if (t >= pauseEnd && !IsPauseResume(bookings[i].Action) && bookings[i].Comment?.Contains("Mindestpause") != true)
            {
                // optional: leave as-is if already after
            }
        }
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

    public WorkTimeSummary CalculateWorkTime(
        List<(string Action, string Time, string? Comment)> bookings,
        TimeSpan sollzeit)
    {
        var s = CalculateWorkTimeInternal(bookings);
        s.Sollzeit = sollzeit;
        s.Differenz = s.Gearbeitet - sollzeit;
        s.Exceeds10h = s.Gearbeitet > TimeSpan.FromHours(10);

        // Theoretical end of day if exactly Sollzeit is worked, given known pauses:
        // Start + Sollzeit + PauseTotal (pauses do not count as work).
        if (s.DayStart != default || bookings.Count > 0)
        {
            var ende = s.DayStart + sollzeit + s.PauseTotal;
            // Cap display at 23:59 style – still show full span via FormatTime
            s.SollEnde = ende;
            s.HasSollEnde = true;
        }

        return s;
    }

    private static WorkTimeSummary CalculateWorkTimeInternal(
        List<(string Action, string Time, string? Comment)> bookings)
    {
        var summary = new WorkTimeSummary();
        var events = new List<(TimeSpan Time, string Action)>();
        foreach (var (action, timeStr, _) in bookings)
        {
            if (TryParseTime(timeStr, out var t))
                events.Add((t, action));
        }

        if (events.Count == 0)
            return summary;

        var dayStart = events[0].Time;
        var dayEnd = events[^1].Time;
        if (dayEnd < dayStart)
            return summary;

        var daySpan = dayEnd - dayStart;
        var pauseTotal = TimeSpan.Zero;
        var pauseIntervals = new List<(TimeSpan Start, TimeSpan End)>();

        for (int i = 0; i < events.Count - 1; i++)
        {
            var (t1, a1) = events[i];
            var (t2, a2) = events[i + 1];
            if (!IsPauseStart(a1) || !IsPauseResume(a2) || t2 <= t1) continue;
            pauseTotal += t2 - t1;
            pauseIntervals.Add((t1, t2));
        }

        var worked = daySpan - pauseTotal;
        if (worked < TimeSpan.Zero) worked = TimeSpan.Zero;

        summary.Gearbeitet = worked;
        summary.PauseTotal = pauseTotal;
        summary.DayStart = dayStart;
        summary.DayEnd = dayEnd;
        summary.Intervals = pauseIntervals;
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
            return $"{sign}{MatrixEngine.FormatTime(Differenz.Duration())}";
        }
    }

    /// <summary>Clock time when the day would end if exactly Sollzeit were worked (start + soll + pauses).</summary>
    public TimeSpan SollEnde { get; set; }
    public bool HasSollEnde { get; set; }
    public string SollEndeText => HasSollEnde ? MatrixEngine.FormatTime(SollEnde) : "—";
}
