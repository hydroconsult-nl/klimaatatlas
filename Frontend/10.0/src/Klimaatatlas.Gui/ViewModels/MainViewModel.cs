using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klimaatatlas.Core;
using Klimaatatlas.Gui.Views;

namespace Klimaatatlas.Gui.ViewModels;

/// <param name="rememberConfiguration">Store the loaded configuration as "last used" (off in tests).</param>
public sealed partial class MainViewModel(IFileDialogs dialogs, bool rememberConfiguration = true) : ObservableObject
{
    private AtlasConfiguration? _configuration;

    [ObservableProperty] private string _configPath = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGpkgOverridden))]
    private string _gpkgPath = "";

    /// <summary>True when the user picked another geopackage than the one in the configuration.</summary>
    public bool IsGpkgOverridden => _configuration is not null &&
        !string.Equals(GpkgPath, _configuration.FeaturesDataset.Path, StringComparison.OrdinalIgnoreCase);

    /// <summary>Outcome of the last run, shown as a banner: null = no run yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunResult), nameof(RunFailed))]
    private bool? _runSucceeded;

    [ObservableProperty] private string _runSummary = "";

    public bool HasRunResult => RunSucceeded is not null;
    public bool RunFailed => RunSucceeded == false;
    [ObservableProperty] private string _tableName = "";
    [ObservableProperty] private RuleViewModel? _selectedRule;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCommand), nameof(ReloadCommand), nameof(BrowseConfigCommand), nameof(BrowseGpkgCommand))]
    private bool _isBusy;

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _progressText = "Kies een configuratiebestand.";
    [ObservableProperty] private string? _diagnosticsPath;

    public ObservableCollection<SelectableItem> Scenarios { get; } = [];
    public ObservableCollection<RuleViewModel> Rules { get; } = [];
    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public bool HasConfiguration => _configuration is not null;

    public string Title => _configuration is null
        ? "Klimaatatlas"
        : $"Klimaatatlas – {Path.GetFileName(_configuration.SourcePath)}";

    public void LoadConfiguration(string path)
    {
        try
        {
            _configuration = AtlasConfiguration.Load(path);
        }
        catch (Exception ex) when (ex is ConfigurationException or IOException or UnauthorizedAccessException)
        {
            _configuration = null;
            AddLog(LogLevel.Error, $"Configuratie kan niet worden gelezen: {ex.Message}");
            ProgressText = "Configuratie niet gelezen.";
            NotifyConfigurationChanged();
            return;
        }

        ConfigPath = _configuration.SourcePath;
        GpkgPath = _configuration.FeaturesDataset.Path;
        TableName = _configuration.FeaturesDataset.TableName;
        if (rememberConfiguration) UserSettings.LastConfigPath = _configuration.SourcePath;

        Scenarios.Clear();
        foreach (var scenario in _configuration.Scenarios) Scenarios.Add(new SelectableItem(scenario));

        Rules.Clear();
        foreach (var rule in _configuration.Rules) Rules.Add(new RuleViewModel(rule));
        SelectedRule = Rules.FirstOrDefault();

        ProgressValue = 0;
        RunSucceeded = null;
        ProgressText = $"Configuratie gelezen: {Rules.Count} regel(s), {Scenarios.Count} scenario('s).";
        if (!File.Exists(GpkgPath))
            AddLog(LogLevel.Warning, $"De geopackage uit de configuratie bestaat niet op deze computer: {GpkgPath}. Kies hierboven de juiste geopackage.");

        NotifyConfigurationChanged();
    }

    private void NotifyConfigurationChanged()
    {
        OnPropertyChanged(nameof(HasConfiguration));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsGpkgOverridden));
        ExecuteCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
    }

    private bool CanEdit() => !IsBusy;
    private bool CanUseConfiguration() => !IsBusy && _configuration is not null;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task BrowseConfigAsync()
    {
        var path = await dialogs.PickFileAsync("Configuratiebestand kiezen", "Klimaatatlas-configuratie", "*.json",
            string.IsNullOrEmpty(ConfigPath) ? null : ConfigPath);
        if (path is not null) LoadConfiguration(path);
    }

    [RelayCommand(CanExecute = nameof(CanUseConfiguration))]
    private void Reload() => LoadConfiguration(ConfigPath);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task BrowseGpkgAsync()
    {
        var path = await dialogs.PickFileAsync("Geopackage kiezen", "GeoPackage", "*.gpkg",
            string.IsNullOrEmpty(GpkgPath) ? ConfigPath : GpkgPath);
        if (path is not null) GpkgPath = path;
    }

    [RelayCommand]
    private void ResetGpkg()
    {
        if (_configuration is not null) GpkgPath = _configuration.FeaturesDataset.Path;
    }

    [RelayCommand]
    private async Task OpenDiagnosticsAsync()
    {
        if (DiagnosticsPath is not null && File.Exists(DiagnosticsPath))
            await dialogs.OpenWithDefaultAppAsync(DiagnosticsPath);
    }

    [RelayCommand]
    private void ClearLog() => LogEntries.Clear();

    [RelayCommand(CanExecute = nameof(CanUseConfiguration))]
    private async Task ExecuteAsync()
    {
        var configuration = _configuration!;
        var rules = Rules.Where(r => r.IsSelected).Select(r => r.Rule).ToList();
        var scenarios = Scenarios.Where(s => s.IsSelected).Select(s => s.Name).ToList();
        if (rules.Count == 0 || scenarios.Count == 0)
        {
            AddLog(LogLevel.Warning, "Selecteer ten minste één regel en één scenario.");
            return;
        }

        IsBusy = true;
        ProgressValue = 0;
        RunSucceeded = null;
        LogEntries.Clear();

        var log = new DiagnosticsLog();
        log.EntryAdded += (level, message) =>
        {
            if (level != LogLevel.Trace) Dispatcher.UIThread.Post(() => AddLog(level, message));
        };
        DiagnosticsPath = Path.Combine(Path.GetDirectoryName(configuration.SourcePath)!, "klimaatatlas.dia");
        try { log.SetDiagnosticsPath(DiagnosticsPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddLog(LogLevel.Warning, $"Diagnosebestand kan niet worden geschreven: {ex.Message}");
            DiagnosticsPath = null;
        }

        var gpkg = GpkgPath;
        var table = TableName;
        var progress = new UiProgress(this);

        bool ok;
        try
        {
            ok = await Task.Run(() =>
            {
                using var connection = GeoPackage.Open(gpkg, log);
                return new RuleEngine(connection, table, log, progress).ProcessRules(rules, scenarios);
            });
        }
        catch (Exception ex)
        {
            log.AddError(ex.Message);
            ok = false;
        }

        // let queued log entries arrive before the summary
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        var errors = log.Errors.Count;
        var warnings = log.Warnings.Count;
        ProgressValue = ok ? 100 : 0;
        ProgressText = ok
            ? $"Klaar: {rules.Count} regel(s) × {scenarios.Count} scenario('s) doorgerekend" + (warnings > 0 ? $", {warnings} waarschuwing(en)." : ".")
            : $"Klaar met {errors} fout(en). Zie de meldingen.";
        RunSummary = ok
            ? $"Rekenregels doorgerekend en weggeschreven naar {Path.GetFileName(gpkg)} ({rules.Count} regel(s) × {scenarios.Count} scenario('s))." +
              (warnings > 0 ? $" {warnings} waarschuwing(en), zie hieronder." : "")
            : $"Doorrekenen niet volledig gelukt: {errors} fout(en), zie hieronder. Geslaagde scenario's zijn weggeschreven; een mislukt scenario is teruggedraaid en de volgende scenario's van die regel zijn overgeslagen.";
        RunSucceeded = ok;
        IsBusy = false;
    }

    private void AddLog(LogLevel level, string message) => LogEntries.Add(new LogEntry(level, message));

    /// <summary>
    /// Progress from the worker thread. Row-by-row reports are reduced to whole percentages
    /// so the UI thread is not flooded with 100.000 updates.
    /// </summary>
    private sealed class UiProgress(MainViewModel owner) : IProgress<ProgressReport>
    {
        private int _lastPercent = -1;

        public void Report(ProgressReport value)
        {
            if (value.Message is not null)
            {
                _lastPercent = -1;
                Dispatcher.UIThread.Post(() => { owner.ProgressText = value.Message; owner.ProgressValue = 0; });
                return;
            }
            if (value.Total <= 0) return;
            var percent = (int)(100 * value.Current / value.Total);
            if (percent == _lastPercent) return;
            _lastPercent = percent;
            Dispatcher.UIThread.Post(() => owner.ProgressValue = percent);
        }
    }
}

public sealed partial class SelectableItem(string name) : ObservableObject
{
    public string Name { get; } = name;
    [ObservableProperty] private bool _isSelected = true;
}

public sealed record LogEntry(LogLevel Level, string Message)
{
    public string Prefix => Level switch
    {
        LogLevel.Error => "FOUT",
        LogLevel.Warning => "LET OP",
        _ => "",
    };

    public bool IsError => Level == LogLevel.Error;
    public bool IsWarning => Level == LogLevel.Warning;
}
