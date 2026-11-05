using System.Collections.ObjectModel;
using System.Globalization;
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

    public ObservableCollection<string> OrtBeginnOptions { get; } = new() { "Dienststätte", "Wohnort", "Einsatzort", "Fortbildungsort" };
    public ObservableCollection<string> PauseOptions { get; } = new() { "Ja", "Nein" };
    public ObservableCollection<string> DienstgangOptions { get; } = new() { "Ja", "Nein" };
    public ObservableCollection<string> DienstgangOrtOptions { get; } = new() { "Gleicher Ort", "Anderer Ort" };
    public ObservableCollection<string> DienstgangEndeTypOptions { get; } = new() { "Rückkehr", "Außerhalb" };
    public ObservableCollection<string> OrtEndeOptions { get; } = new() { "Dienststätte", "Wohnort" };
    public ObservableCollection<string> MitarbeiterTypOptions { get; } = new() { "Angestellte", "Beamte" };
    public ObservableCollection<string> ZusatzMobilOptions { get; } = new() { "Nein", "Ja" };
    public ObservableCollection<string> DienstreiseOptions { get; } = new() { "Nein", "Ja" };

    public ObservableCollection<TimeInputItem> TimeInputs { get; } = new();
    public ObservableCollection<BookingResultItem> BookingResults { get; } = new();
    public ObservableCollection<Procedure> MatchingProcedures { get; } = new();

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

        SelectedOrtBeginn = "Dienststätte";
        SelectedPause = "Ja";
        SelectedDienstgang = "Nein";
        SelectedDienstgangOrt = "Gleicher Ort";
        SelectedDienstgangEndeTyp = "Rückkehr";
        SelectedOrtEnde = "Dienststätte";
        SelectedMitarbeiterTyp = "Angestellte";
        SelectedZusatzMobil = "Nein";
        SelectedDienstreise = "Nein";

        UpdateVisibility();
        UpdateMatches();
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

    partial void OnSelectedOrtBeginnChanged(string? value)
    {
        if (value == "Wohnort" || value == "Fortbildungsort")
            SelectedOrtEnde = "Wohnort";
        else if (value == "Dienststätte" || value == "Einsatzort")
            SelectedOrtEnde = "Dienststätte";
        UpdateVisibility();
        UpdateMatches();
    }

    partial void OnSelectedPauseChanged(string? value) { UpdateVisibility(); UpdateMatches(); }
    partial void OnSelectedDienstgangChanged(string? value) { UpdateVisibility(); UpdateMatches(); }
    partial void OnSelectedDienstgangOrtChanged(string? value) { UpdateMatches(); }
    partial void OnSelectedDienstgangEndeTypChanged(string? value) { UpdateMatches(); }
    partial void OnSelectedOrtEndeChanged(string? value) { UpdateMatches(); }
    partial void OnSelectedMitarbeiterTypChanged(string? value) { UpdateMatches(); }
    partial void OnSelectedZusatzMobilChanged(string? value) { UpdateMatches(); }
    partial void OnSelectedDienstreiseChanged(string? value) { UpdateMatches(); }

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

    private Dictionary<string, string> BuildSelections()
    {
        var s = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(SelectedOrtBeginn)) s["OrtBeginn"] = SelectedOrtBeginn;
        if (!string.IsNullOrEmpty(SelectedPause)) s["Pause"] = SelectedPause;
        if (!string.IsNullOrEmpty(SelectedDienstgang)) s["Dienstgang"] = SelectedDienstgang;
        if (ShowDienstgangDetails && !string.IsNullOrEmpty(SelectedDienstgangEndeTyp))
            s["DienstgangEndeTyp"] = SelectedDienstgangEndeTyp;
        if (ShowDienstgangOrt && !string.IsNullOrEmpty(SelectedDienstgangOrt))
            s["DienstgangOrt"] = SelectedDienstgangOrt;
        if (!string.IsNullOrEmpty(SelectedOrtEnde))
            s["OrtEnde"] = SelectedOrtEnde;
        if (ShowMitarbeiterTyp && !string.IsNullOrEmpty(SelectedMitarbeiterTyp))
            s["MitarbeiterTyp"] = SelectedMitarbeiterTyp;
        if (ShowZusatzMobil && !string.IsNullOrEmpty(SelectedZusatzMobil))
            s["ZusatzMobil"] = SelectedZusatzMobil;
        if (ShowDienstreise && !string.IsNullOrEmpty(SelectedDienstreise))
            s["Dienstreise"] = SelectedDienstreise;
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

        // Summary
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
        BookingResults.Clear();
        SummaryGearbeitet = "—";
        SummarySollzeit = "07:48";
        SummaryDifferenz = "—";
        SummaryWarning = "";
        ShowSummaryWarning = false;
        StatusMessage = "Zurückgesetzt.";
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
