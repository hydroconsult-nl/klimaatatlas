namespace Klimaatatlas.Core;

/// <summary>
/// Collects errors, warnings and messages and optionally mirrors them to a diagnostics file
/// (the "klimaatatlas.dia" file of the original application).
/// Port of clsLog without the WinForms message boxes and notepad call.
/// </summary>
public sealed class DiagnosticsLog
{
    private readonly object _lock = new();

    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Messages { get; } = [];

    public string? DiagnosticsFilePath { get; private set; }

    /// <summary>Raised for every entry, so a front-end (CLI or GUI) can show it live.</summary>
    public event Action<LogLevel, string>? EntryAdded;

    public void SetDiagnosticsPath(string path)
    {
        DiagnosticsFilePath = path;
        File.WriteAllText(path, $"DIAGNOSTICS FILE, CREATED ON: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
    }

    public void AddError(string message) => Add(LogLevel.Error, Errors, message);
    public void AddWarning(string message) => Add(LogLevel.Warning, Warnings, message);
    public void AddMessage(string message) => Add(LogLevel.Message, Messages, message);

    /// <summary>Writes a line to the diagnostics file only (verbose trace).</summary>
    public void Trace(string message)
    {
        WriteToDiagnosticsFile(message);
        EntryAdded?.Invoke(LogLevel.Trace, message);
    }

    private void Add(LogLevel level, List<string> list, string message)
    {
        lock (_lock) list.Add(message);
        WriteToDiagnosticsFile(message);
        EntryAdded?.Invoke(level, message);
    }

    private void WriteToDiagnosticsFile(string message)
    {
        if (DiagnosticsFilePath is null) return;
        lock (_lock) File.AppendAllText(DiagnosticsFilePath, message + Environment.NewLine);
    }
}

public enum LogLevel
{
    Trace,
    Message,
    Warning,
    Error,
}
