using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VedaMatrix.Models;
using VedaMatrix.Services;

namespace VedaMatrix.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly MatrixEngine _engine;
    private readonly ProcedureConfig _config;

    // Full option lists (never mutate these – used as source for filtering)
    private static readonly string[] AllOrtBeginn = { "Dienststätte", "Wohnort", "Einsatzort", "Fortbildungsort" };
    private static readonly string[] AllPause = { "Ja", "Nein" };
    private static readonly string[] AllDienstgang = { "Ja", "Nein" };
    private static readonly string[] AllDienstgangOrt = { "Gleicher Ort", "Anderer Ort" };
    private static readonly string[] AllDienstgangEndeTyp = { "Rückkehr", "Außerhalb" };
    private static readonly string[] AllOrtEnde = { "Dienststätte", "Wohnort" };
    private static readonly string[] AllMitarbeiterTyp = { "Angestellte", "Beamte" };
    private static readonly string[] AllZusatzMobil = { "Nein", "Ja" };
    private static readonly string[] AllDienstreise = { "Nein", "Ja" };

    // Filtered, bound collections
    public ObservableCollection<string> OrtBeginnOptions { get; } = new(AllOrtBeginn);
    public ObservableCollection<string> PauseOptions { get; } = new(AllPause);
    public ObservableCollection<string> DienstgangOptions { get; } = new(AllDienstgang);
    public ObservableCollection<string> DienstgangOrtOptions { get; } = new(AllDienstgangOrt);
    public ObservableCollection<string> DienstgangEndeTypOptions { get; } = new(AllDienstgangEndeTyp);
    public ObservableCollection<string> OrtEndeOptions { get; } = new(AllOrtEnde);
    public ObservableCollection<string> MitarbeiterTypOptions { get; } = new(AllMitarbeiterTyp);
    public ObservableCollection<string> ZusatzMobilOptions { get; } = new(AllZusatzMobil);
    public ObservableCollection<string> DienstreiseOptions { get; } = new(AllDienstreise);

    public ObservableCollection<TimeInputItem> TimeInputs { get; } = new();
    public ObservableCollection<BookingResultItem> BookingResults { get; } = new();
    public ObservableCollection<Procedure> MatchingProcedures { get; } = new();

    private bool _suppressFilter;

    public MainViewModel()
    {
        try
        {
            _configService = new ConfigService();
            _config = _configService.Load();
            _engine = new MatrixEngine(_config.Procedures ?? new List<Procedure>());
            Console.WriteLine($"[VM] {_config.Procedures?.Count ?? 0} Prozeduren geladen.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[VM] Fehler beim Laden der Konfiguration:");
            Console.WriteLine(ex);
            Console.ResetColor();

            _configService = new ConfigService();
            _config = new ProcedureConfig();
            _engine = new MatrixEngine(Array.Empty<Procedure>());
        }

        SollzeitText = "07:48";

        // Initial selection without cascading side-effects
        _suppressFilter = true;
        SelectedOrtBeginn = "Dienststätte";
        SelectedPause = "Ja";
        SelectedDienstgang = "Nein";
        SelectedDienstgangOrt = "Gleicher Ort";
        SelectedDienstgangEndeTyp = "Rückkehr";
        SelectedOrtEnde = "Dienststätte";
        SelectedMitarbeiterTyp = "Angestellte";
        SelectedZusatzMobil = "Nein";
        SelectedDienstreise = "Nein";
        _suppressFilter = false;

        RefreshFiltersAndMatches();
    }

    // Selections
    [ObservableProperty] private string? selectedOrtBeginn;
    [ObservableProperty] private string? selectedPause;
    [ObservableProperty] private string? selectedDienstgang;
    [ObservableProperty] private string? selectedDienstgangOrt;
    [ObservableProperty] private string? selectedDienstgangEndeTyp;
    [ObservableProperty] private string? selectedOrtEnde;
    [ObservableProperty] private string? selectedMitarbeiterTyp;
    [ObservableProperty] private string? selectedZusatzMobil;
    [ObservableProperty] private string? selectedDienstreise;

    // Visibility
    [ObservableProperty] private bool showDienstgangDetails;
    [ObservableProperty] private bool showDienstgangOrt;
    [ObservableProperty] private bool showOrtEnde;
    [ObservableProperty] private bool showMitarbeiterTyp;
    [ObservableProperty] private bool showZusatzMobil;
    [ObservableProperty] private bool showDienstreise;
    [ObservableProperty] private bool showFortbildungHint;

    [ObservableProperty] private Procedure? selectedProcedure;
    [ObservableProperty] private string? attentionNote;
    [ObservableProperty] private string statusMessage = "Bitte Auswahl treffen und Zeiten eingeben.";

    // Sollzeit + Summary
    [ObservableProperty] private string sollzeitText = "07:48";
    [ObservableProperty] private string summaryGearbeitet = "—";
    [ObservableProperty] private string summarySollzeit = "07:48";
    [ObservableProperty] private string summaryDifferenz = "—";
    [ObservableProperty] private string summaryWarning = "";
    [ObservableProperty] private bool showSummaryWarning;

    // ── Change handlers ──────────────────────────────────────────────

    partial void OnSelectedOrtBeginnChanged(string? value)
    {
        if (_suppressFilter) return;
        // Auto-adjust OrtEnde default based on begin location
        _suppressFilter = true;
        if (value == "Wohnort" || value == "Fortbildungsort")
            SelectedOrtEnde = "Wohnort";
        else if (value == "Dienststätte" || value == "Einsatzort")
            SelectedOrtEnde = "Dienststätte";
        _suppressFilter = false;
        RefreshFiltersAndMatches();
    }

    partial void OnSelectedPauseChanged(string? value) { if (!_suppressFilter) RefreshFiltersAndMatches(); }
    partial void OnSelectedDienstgangChanged(string? value) { if (!_suppressFilter) RefreshFiltersAndMatches(); }
    partial void OnSelectedDienstgangOrtChanged(string? value) { if (!_suppressFilter) RefreshFiltersAndMatches(); }
    partial void OnSelectedDienstgangEndeTypChanged(string? value) { if (!_suppressFilter) RefreshFiltersAndMatches(); }
    partial void OnSelectedOrtEndeChanged(string? value) { if (!_suppressFilter) RefreshFiltersAndMatches(); }
    partial void OnSelectedMitarbeiterTypChanged(string? value) { if (!_suppressFilter) RefreshFiltersAndMatches(); }
    partial void OnSelectedZusatzMobilChanged(string? value) { if (!_suppressFilter) RefreshFiltersAndMatches(); }
    partial void OnSelectedDienstreiseChanged(string? value) { if (!_suppressFilter) RefreshFiltersAndMatches(); }

    // ── Core filter + match logic ────────────────────────────────────

    private void RefreshFiltersAndMatches()
    {
        UpdateVisibility();
        ApplyOptionFilters();
        UpdateMatches();
    }

    private void UpdateVisibility()
    {
        ShowDienstgangDetails = SelectedDienstgang == "Ja";
        ShowDienstgangOrt = SelectedDienstgang == "Ja" && SelectedOrtBeginn == "Dienststätte";
        ShowOrtEnde = true;
        ShowMitarbeiterTyp = SelectedOrtBeginn == "Einsatzort" || (SelectedOrtBeginn == "Wohnort" && SelectedDienstgang == "Ja");
        ShowZusatzMobil = SelectedOrtBeginn == "Wohnort" && SelectedDienstgang == "Ja";
        ShowDienstreise = SelectedOrtBeginn == "Dienststätte" && SelectedDienstgang == "Ja" && SelectedDienstgangOrt == "Anderer Ort";
        ShowFortbildungHint = SelectedOrtBeginn == "Fortbildungsort";
    }

    /// <summary>
    /// Rebuild every dropdown so only values that can still reach a procedure remain.
    /// If exactly one value is left, select it automatically.
    /// </summary>
    private void ApplyOptionFilters()
    {
        _suppressFilter = true;
        try
        {
            // Build selection WITHOUT the dimension being filtered
            FilterDimension("OrtBeginn", AllOrtBeginn, OrtBeginnOptions,
                () => SelectedOrtBeginn, v => SelectedOrtBeginn = v);

            FilterDimension("Pause", AllPause, PauseOptions,
                () => SelectedPause, v => SelectedPause = v);

            FilterDimension("Dienstgang", AllDienstgang, DienstgangOptions,
                () => SelectedDienstgang, v => SelectedDienstgang = v);

            if (ShowDienstgangDetails)
            {
                FilterDimension("DienstgangEndeTyp", AllDienstgangEndeTyp, DienstgangEndeTypOptions,
                    () => SelectedDienstgangEndeTyp, v => SelectedDienstgangEndeTyp = v);
            }

            if (ShowDienstgangOrt)
            {
                FilterDimension("DienstgangOrt", AllDienstgangOrt, DienstgangOrtOptions,
                    () => SelectedDienstgangOrt, v => SelectedDienstgangOrt = v);
            }

            FilterDimension("OrtEnde", AllOrtEnde, OrtEndeOptions,
                () => SelectedOrtEnde, v => SelectedOrtEnde = v);

            if (ShowMitarbeiterTyp)
            {
                FilterDimension("MitarbeiterTyp", AllMitarbeiterTyp, MitarbeiterTypOptions,
                    () => SelectedMitarbeiterTyp, v => SelectedMitarbeiterTyp = v);
            }

            if (ShowZusatzMobil)
            {
                FilterDimension("ZusatzMobil", AllZusatzMobil, ZusatzMobilOptions,
                    () => SelectedZusatzMobil, v => SelectedZusatzMobil = v);
            }

            if (ShowDienstreise)
            {
                FilterDimension("Dienstreise", AllDienstreise, DienstreiseOptions,
                    () => SelectedDienstreise, v => SelectedDienstreise = v);
            }
        }
        finally
        {
            _suppressFilter = false;
        }
    }

    private void FilterDimension(
        string dimension,
        string[] allPossible,
        ObservableCollection<string> target,
        Func<string?> getCurrent,
        Action<string> setCurrent)
    {
        // Selections excluding this dimension
        var baseSel = BuildSelectionsExcluding(dimension);
        var valid = _engine.GetValidValues(dimension, baseSel, allPossible);

        // Update collection in-place to keep UI responsive
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!valid.Contains(target[i]))
                target.RemoveAt(i);
        }
        foreach (var v in valid)
        {
            if (!target.Contains(v))
                target.Add(v);
        }

        // Preserve order of allPossible
        var ordered = allPossible.Where(v => valid.Contains(v)).ToList();
        if (!target.SequenceEqual(ordered))
        {
            target.Clear();
            foreach (var v in ordered)
                target.Add(v);
        }

        var current = getCurrent();
        if (valid.Count == 1)
        {
            // Only one path left → auto-select
            if (current != valid[0])
                setCurrent(valid[0]);
        }
        else if (current != null && !valid.Contains(current) && valid.Count > 0)
        {
            // Current selection became invalid → pick first remaining
            setCurrent(valid[0]);
        }
    }

    private Dictionary<string, string> BuildSelections()
    {
        return BuildSelectionsExcluding(null);
    }

    private Dictionary<string, string> BuildSelectionsExcluding(string? excludeDimension)
    {
        var s = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Add(string key, string? value)
        {
            if (excludeDimension != null && key == excludeDimension) return;
            if (!string.IsNullOrEmpty(value))
                s[key] = value;
        }

        Add("OrtBeginn", SelectedOrtBeginn);
        Add("Pause", SelectedPause);
        Add("Dienstgang", SelectedDienstgang);

        if (ShowDienstgangDetails)
            Add("DienstgangEndeTyp", SelectedDienstgangEndeTyp);
        if (ShowDienstgangOrt)
            Add("DienstgangOrt", SelectedDienstgangOrt);

        Add("OrtEnde", SelectedOrtEnde);

        if (ShowMitarbeiterTyp)
            Add("MitarbeiterTyp", SelectedMitarbeiterTyp);
        if (ShowZusatzMobil)
            Add("ZusatzMobil", SelectedZusatzMobil);
        if (ShowDienstreise)
            Add("Dienstreise", SelectedDienstreise);

        return s;
    }

    private void UpdateMatches()
    {
        if (MatchingProcedures is null || _engine is null) return;

        var selections = BuildSelections();
        MatchingProcedures.Clear();
        foreach (var p in _engine.FindMatching(selections))
            MatchingProcedures.Add(p);

        SelectedProcedure = MatchingProcedures.FirstOrDefault()
                            ?? _engine.FindBestMatch(selections);

        UpdateTimeFields();
        AttentionNote = SelectedProcedure?.AttentionNote;
        StatusMessage = SelectedProcedure != null
            ? $"Gefunden: {SelectedProcedure.Name}"
            : "Keine passende Prozedur. Auswahl anpassen.";
    }

    private void UpdateTimeFields()
    {
        if (TimeInputs is null) return;
        TimeInputs.Clear();
        if (SelectedProcedure == null) return;

        var labels = _config.TimeFieldLabels ?? new Dictionary<string, string>();
        foreach (var key in SelectedProcedure.RequiredTimeFields ?? new List<string>())
        {
            labels.TryGetValue(key, out var label);
            TimeInputs.Add(new TimeInputItem
            {
                Key = key,
                Label = label ?? key,
                Value = GetDefaultTime(key)
            });
        }
    }

    private static string GetDefaultTime(string key) => key switch
    {
        "Arbeitsbeginn" => "07:00",
        "PauseBeginn" => "12:30",
        "PauseEnde" => "13:00",
        "DienstgangStart" => "09:00",
        "DienstgangEnde" => "15:30",
        "Arbeitsende" => "16:00",
        "ArbeitsendeDienststaette" => "15:00",
        "ArbeitsbeginnMobil2" => "17:00",
        "FortbildungBeginn" => "09:00",
        "FortbildungEnde" => "15:45",
        "FahrtBeginnHin" => "07:00",
        "AnkunftOrt" => "08:15",
        "FahrtBeginnRueck" => "16:00",
        "AnkunftWohnort" => "17:30",
        _ => "08:00"
    };

    private TimeSpan ParseSollzeit()
    {
        if (MatrixEngine.TryParseTime(SollzeitText, out var ts))
            return ts;
        return new TimeSpan(7, 48, 0);
    }

    [RelayCommand]
    private void Generate()
    {
        if (SelectedProcedure == null)
        {
            StatusMessage = "Keine Prozedur ausgewählt.";
            return;
        }

        var times = TimeInputs.ToDictionary(t => t.Key, t => t.Value ?? "");
        var bookings = _engine.GenerateBookings(SelectedProcedure, times);

        BookingResults.Clear();
        int i = 1;
        foreach (var (action, time, comment) in bookings)
        {
            BookingResults.Add(new BookingResultItem
            {
                Nummer = i++,
                Aktion = action,
                Zeit = time,
                Kommentar = comment ?? ""
            });
        }

        var soll = ParseSollzeit();
        var summary = _engine.CalculateWorkTime(bookings, soll);

        SummaryGearbeitet = summary.GearbeitetText;
        SummarySollzeit = summary.SollzeitText;
        SummaryDifferenz = summary.DifferenzText;
        ShowSummaryWarning = summary.Exceeds10h;
        SummaryWarning = summary.Exceeds10h
            ? "⚠ Arbeitszeit überschreitet die 10-Stunden-Grenze!"
            : "";

        StatusMessage = $"Buchungssequenz generiert ({bookings.Count} Schritte). Gearbeitet: {summary.GearbeitetText}";
        if (SelectedProcedure.RequiresBewegungsantrag)
            StatusMessage += "  ·  Bewegungsantrag ggf. erforderlich.";
        if (summary.Exceeds10h)
            StatusMessage += "  ·  10h-Limit überschritten.";
    }

    [RelayCommand]
    private void Reset()
    {
        _suppressFilter = true;
        SelectedOrtBeginn = "Dienststätte";
        SelectedPause = "Ja";
        SelectedDienstgang = "Nein";
        SelectedDienstgangOrt = "Gleicher Ort";
        SelectedDienstgangEndeTyp = "Rückkehr";
        SelectedOrtEnde = "Dienststätte";
        SelectedMitarbeiterTyp = "Angestellte";
        SelectedZusatzMobil = "Nein";
        SelectedDienstreise = "Nein";
        SollzeitText = "07:48";
        _suppressFilter = false;

        BookingResults.Clear();
        SummaryGearbeitet = "—";
        SummarySollzeit = "07:48";
        SummaryDifferenz = "—";
        SummaryWarning = "";
        ShowSummaryWarning = false;

        // Restore full option lists then re-filter
        RestoreAllOptions();
        RefreshFiltersAndMatches();
        StatusMessage = "Zurückgesetzt.";
    }

    private void RestoreAllOptions()
    {
        void Fill(ObservableCollection<string> col, string[] all)
        {
            col.Clear();
            foreach (var v in all) col.Add(v);
        }
        Fill(OrtBeginnOptions, AllOrtBeginn);
        Fill(PauseOptions, AllPause);
        Fill(DienstgangOptions, AllDienstgang);
        Fill(DienstgangOrtOptions, AllDienstgangOrt);
        Fill(DienstgangEndeTypOptions, AllDienstgangEndeTyp);
        Fill(OrtEndeOptions, AllOrtEnde);
        Fill(MitarbeiterTypOptions, AllMitarbeiterTyp);
        Fill(ZusatzMobilOptions, AllZusatzMobil);
        Fill(DienstreiseOptions, AllDienstreise);
    }
}

public partial class TimeInputItem : ObservableObject
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    [ObservableProperty] private string? value;
}

public partial class BookingResultItem : ObservableObject
{
    public int Nummer { get; set; }
    public string Aktion { get; set; } = "";
    public string Zeit { get; set; } = "";
    public string Kommentar { get; set; } = "";
}
