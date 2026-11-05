# VEDA Matrix

Native C# Desktop-Anwendung (.NET 10 / Avalonia) zur Generierung von Buchungsvorgängen für **Veda Horizon**.

## Features

- **Deutsche Oberfläche**, minimalistisch, schwarz-weiß
- Dropdown-basierte Entscheidungsmatrix für alle 12 Beispiel-Buchungsvorgänge aus der Dokumentation
- Dynamisch ein- und ausblendende Felder je nach Auswahl
- Zeit-Eingaben führen zu konkreten Buchungssequenzen
- **Konfigurierbar** über `Config/procedures.json` (Building-Block-System)
- Alle 12 Prozeduren als JSON-Bausteine hinterlegt

## Build (macOS)

```bash
dotnet restore
dotnet build
dotnet run
```

Voraussetzung: .NET 10 SDK.

## Konfiguration erweitern

Die Datei `Config/procedures.json` enthält:

- `AvailableActions` – erlaubte Buchungsaktionen
- `TimeFieldLabels` – Labels für Zeitfelder
- `Procedures` – Liste der Prozeduren mit:
  - `Conditions` – Matching-Schlüssel (OrtBeginn, Pause, Dienstgang, …)
  - `RequiredTimeFields` – welche Zeiten abgefragt werden
  - `Steps` – die eigentliche Buchungssequenz (Action + TimeKey + optional Comment)

Neue Szenarien können als weitere Prozedur-Objekte hinzugefügt werden, ohne Code zu ändern.

## Architektur

- `Models/` – Procedure, BookingStep, Config
- `Services/ConfigService` – JSON laden/speichern
- `Services/MatrixEngine` – Matching & Generierung der Buchungen
- `ViewModels/MainViewModel` – gesamte UI-Logik (MVVM)
- Avalonia UI, Fluent Theme, B&W Custom Styles
