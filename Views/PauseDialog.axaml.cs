using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VedaMatrix.Models;
using VedaMatrix.Services;

namespace VedaMatrix.Views;

public partial class PauseDialog : Window
{
    public PauseEntry? Result { get; private set; }

    private readonly IReadOnlyList<PauseEntry> _existing;

    public PauseDialog() : this(null) { }

    public PauseDialog(IEnumerable<PauseEntry>? existing)
    {
        _existing = existing?.ToList() ?? new List<PauseEntry>();
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var startBox = this.FindControl<TextBox>("StartBox");
        var endBox = this.FindControl<TextBox>("EndBox");
        var error = this.FindControl<TextBlock>("ErrorText");

        var start = startBox?.Text?.Trim() ?? "";
        var end = endBox?.Text?.Trim() ?? "";

        if (!MatrixEngine.TryParseTime(start, out var t0) || !MatrixEngine.TryParseTime(end, out var t1))
        {
            if (error != null) error.Text = "Bitte gültige Zeiten (HH:mm) eingeben.";
            return;
        }

        if (t1 <= t0)
        {
            if (error != null) error.Text = "Ende muss nach dem Beginn liegen.";
            return;
        }

        // Same interval or any overlap with existing pauses
        foreach (var p in _existing)
        {
            if (!MatrixEngine.TryParseTime(p.Start, out var e0) || !MatrixEngine.TryParseTime(p.End, out var e1))
                continue;

            // Exact same frame
            if (e0 == t0 && e1 == t1)
            {
                if (error != null) error.Text = "Diese Pause existiert bereits (gleiche Zeitspanne).";
                return;
            }

            // Overlap: ranges share any common time (touching endpoints = no overlap)
            // [t0,t1) overlaps [e0,e1) if t0 < e1 && e0 < t1
            if (t0 < e1 && e0 < t1)
            {
                if (error != null)
                    error.Text = $"Pause überschneidet sich mit bestehender Pause {p.Display}.";
                return;
            }
        }

        Result = new PauseEntry
        {
            Start = MatrixEngine.FormatTime(t0),
            End = MatrixEngine.FormatTime(t1)
        };
        Close(Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(null);
    }
}
