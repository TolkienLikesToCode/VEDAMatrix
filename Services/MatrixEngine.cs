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
        // Prefer exact match with most conditions
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
        var result = new List<(string, string, string?)>();
        foreach (var step in procedure.Steps)
        {
            string timeValue;
            if (!string.IsNullOrEmpty(step.FixedTime))
            {
                timeValue = step.FixedTime;
            }
            else if (!string.IsNullOrEmpty(step.TimeKey) && times.TryGetValue(step.TimeKey, out var t) && !string.IsNullOrWhiteSpace(t))
            {
                timeValue = t;
            }
            else
            {
                timeValue = "—";
            }

            result.Add((step.Action, timeValue, step.Comment));
        }
        return result;
    }
}
