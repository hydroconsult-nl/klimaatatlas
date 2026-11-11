using System.Reflection;
using Klimaatatlas.Core;

namespace Klimaatatlas.Cli;

internal static class Program
{
    private const string Usage = """
        Klimaatatlas - rekent de rekenregels uit een configuratiebestand door en schrijft
        de resultaten naar de geopackage uit dat bestand.

        Gebruik:
          klimaatatlas <config.json> [opties]

        Opties:
          --gpkg <pad>          Gebruik deze geopackage i.p.v. features_dataset.path uit de config
          --table <naam>        Gebruik deze tabel i.p.v. features_dataset.tablename
          --rule <naam>         Reken alleen deze regel door (mag vaker worden opgegeven)
          --scenario <naam>     Reken alleen dit scenario door (mag vaker worden opgegeven)
          --spatialite <pad>    Pad naar de SpatiaLite-bibliotheek (standaard: mod_spatialite)
          --no-spatialite       Laad SpatiaLite niet
          --diagnostics <pad>   Diagnosebestand (standaard: klimaatatlas.dia naast de config)
          --list                Toon scenario's en regels uit de config en stop
          -q, --quiet           Geen voortgang tonen
          -v, --verbose         Ook trace-regels uit het diagnosebestand tonen
          -h, --help            Deze hulptekst
          --version             Versienummer

        Exitcodes: 0 = gelukt, 1 = fout bij doorrekenen, 2 = ongeldige invoer of configuratie.
        """;

    private static int Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Fout: {ex.Message}");
            Console.Error.WriteLine("Gebruik --help voor de opties.");
            return 2;
        }

        if (options.ShowHelp) { Console.WriteLine(Usage); return 0; }
        if (options.ShowVersion)
        {
            Console.WriteLine(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?");
            return 0;
        }

        if (options.ConfigPath is not null) return Run(options);

        // Started without a configuration file (double-click, F5 in Visual Studio, "dotnet run"):
        // ask for it, like the old application that remembered the last used file.
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Fout: Geen configuratiebestand opgegeven.");
            Console.Error.WriteLine("Gebruik: klimaatatlas <config.json>   (--help voor de opties)");
            return 2;
        }

        var configPath = AskConfigPath();
        if (configPath is null) return 2;
        options.ConfigPath = configPath;
        var exitCode = Run(options);

        Console.Error.WriteLine();
        Console.Error.Write("Druk op Enter om af te sluiten...");
        Console.ReadLine();
        return exitCode;
    }

    private static string? AskConfigPath()
    {
        var last = UserSettings.LastConfigPath;
        if (last is not null && !File.Exists(last)) last = null;

        Console.Error.WriteLine("Klimaatatlas - geen configuratiebestand opgegeven.");
        Console.Error.WriteLine("(Tip: klimaatatlas <config.json>, of --help voor alle opties.)");
        Console.Error.WriteLine();
        while (true)
        {
            Console.Error.Write(last is null
                ? "Pad naar het configuratiebestand (.json): "
                : $"Pad naar het configuratiebestand [Enter = {last}]: ");
            var input = Console.ReadLine();
            if (input is null) return null; // end of input

            // Paths dragged into a terminal come with quotes (Windows) or escaped spaces (macOS).
            input = input.Trim().Trim('"', '\'').Replace("\\ ", " ");
            if (input.Length == 0)
            {
                if (last is not null) return last;
                continue;
            }
            if (input.StartsWith('~'))
                input = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), input[1..].TrimStart('/', '\\'));
            if (File.Exists(input)) return Path.GetFullPath(input);
            Console.Error.WriteLine($"Bestand niet gevonden: {input}");
        }
    }

    private static int Run(Options options)
    {
        var progress = options.Quiet ? null : new ConsoleProgress();
        var log = new DiagnosticsLog();
        log.EntryAdded += (level, message) =>
        {
            // finish a running "... 57%" line first, so messages start on their own line
            if (level != LogLevel.Trace || options.Verbose) progress?.Done();
            switch (level)
            {
                case LogLevel.Error: Console.Error.WriteLine($"FOUT: {message}"); break;
                case LogLevel.Warning: Console.Error.WriteLine($"WAARSCHUWING: {message}"); break;
                case LogLevel.Message: Console.Error.WriteLine(message); break;
                case LogLevel.Trace when options.Verbose: Console.Error.WriteLine($"  {message}"); break;
            }
        };

        AtlasConfiguration config;
        try
        {
            config = AtlasConfiguration.Load(options.ConfigPath!);
        }
        catch (Exception ex) when (ex is ConfigurationException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Fout bij lezen configuratie: {ex.Message}");
            return 2;
        }
        UserSettings.LastConfigPath = config.SourcePath;

        if (options.ListOnly)
        {
            Console.WriteLine($"Geopackage: {config.FeaturesDataset.Path}");
            Console.WriteLine($"Tabel:      {config.FeaturesDataset.TableName}");
            Console.WriteLine($"Scenario's: {string.Join(", ", config.Scenarios)}");
            Console.WriteLine("Regels (in uitvoervolgorde):");
            foreach (var rule in config.Rules)
                Console.WriteLine($"  {rule.Name}  ({rule.Benchmarks.Count} maatlatten, filter: {rule.Filter})");
            return 0;
        }

        List<Rule> rules;
        List<string> scenarios;
        try
        {
            rules = Select(config.Rules, options.Rules, r => r.Name, "regel");
            scenarios = Select(config.Scenarios, options.Scenarios, s => s, "scenario");
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Fout: {ex.Message}");
            return 2;
        }

        var diagnosticsPath = options.DiagnosticsPath
            ?? Path.Combine(Path.GetDirectoryName(config.SourcePath)!, "klimaatatlas.dia");
        try
        {
            log.SetDiagnosticsPath(diagnosticsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"WAARSCHUWING: diagnosebestand '{diagnosticsPath}' kan niet worden geschreven ({ex.Message}).");
        }

        var gpkgPath = options.GpkgPath is null ? config.FeaturesDataset.Path : Path.GetFullPath(options.GpkgPath);
        var table = options.TableName ?? config.FeaturesDataset.TableName;

        log.Trace("Configuration file read.");
        try
        {
            using var connection = GeoPackage.Open(gpkgPath, log, options.NoSpatialite ? null : options.SpatialitePath);
            log.Trace("Geopackage connection set.");

            var engine = new RuleEngine(connection, table, log, progress);
            var ok = engine.ProcessRules(rules, scenarios);
            progress?.Done();

            log.Trace("Closing geopackage.");
            if (!ok)
            {
                Console.Error.WriteLine($"Klaar met fouten. Zie {diagnosticsPath}");
                return 1;
            }
            Console.Error.WriteLine($"Klaar: {rules.Count} regel(s) x {scenarios.Count} scenario('s) doorgerekend in {gpkgPath}");
            return 0;
        }
        catch (FileNotFoundException ex)
        {
            log.AddError(ex.Message);
            Console.Error.WriteLine(OperatingSystem.IsWindows()
                ? "Tip: geef met --gpkg een ander pad op."
                : "Tip: de config bevat mogelijk een Windows-pad; geef met --gpkg het pad op deze computer op.");
            return 2;
        }
        catch (Exception ex)
        {
            log.AddError($"Error setting geopackage connection: {ex.Message}");
            return 1;
        }
    }

    private static List<T> Select<T>(List<T> all, List<string> wanted, Func<T, string> name, string kind)
    {
        if (wanted.Count == 0) return all;
        bool Matches(T item, string w) => string.Equals(name(item).Trim(), w.Trim(), StringComparison.OrdinalIgnoreCase);
        var unknown = wanted.Where(w => !all.Any(a => Matches(a, w))).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Onbekende {kind}: {string.Join(", ", unknown)}. Beschikbaar: {string.Join(", ", all.Select(name))}");
        return all.Where(a => wanted.Any(w => Matches(a, w))).ToList();
    }
}

internal sealed class ConsoleProgress : IProgress<ProgressReport>
{
    private readonly bool _interactive = !Console.IsErrorRedirected;
    private int _lastPercent = -1;
    private string _label = "";

    public void Report(ProgressReport value)
    {
        if (value.Message is not null)
        {
            Done();
            _label = value.Message;
            if (!_interactive) Console.Error.WriteLine(_label);
            return;
        }
        if (!_interactive || value.Total <= 0) return;

        var percent = (int)(100 * value.Current / value.Total);
        if (percent == _lastPercent) return;
        _lastPercent = percent;
        Console.Error.Write($"\r{_label} {percent,3}%");
    }

    public void Done()
    {
        if (_interactive && _lastPercent >= 0) Console.Error.WriteLine();
        _lastPercent = -1;
    }
}

internal sealed class Options
{
    public string? ConfigPath { get; set; }
    public string? GpkgPath { get; private set; }
    public string? TableName { get; private set; }
    public string? DiagnosticsPath { get; private set; }
    public string SpatialitePath { get; private set; } = GeoPackage.DefaultSpatialiteExtension;
    public bool NoSpatialite { get; private set; }
    public List<string> Rules { get; } = [];
    public List<string> Scenarios { get; } = [];
    public bool ListOnly { get; private set; }
    public bool Quiet { get; private set; }
    public bool Verbose { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool ShowVersion { get; private set; }

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{arg} verwacht een waarde.");

            switch (arg)
            {
                case "-h" or "--help" or "/?": o.ShowHelp = true; break;
                case "--version": o.ShowVersion = true; break;
                case "--gpkg": o.GpkgPath = Next(); break;
                case "--table": o.TableName = Next(); break;
                case "--rule": o.Rules.Add(Next()); break;
                case "--scenario": o.Scenarios.Add(Next()); break;
                case "--spatialite": o.SpatialitePath = Next(); break;
                case "--no-spatialite": o.NoSpatialite = true; break;
                case "--diagnostics": o.DiagnosticsPath = Next(); break;
                case "--list": o.ListOnly = true; break;
                case "-q" or "--quiet": o.Quiet = true; break;
                case "-v" or "--verbose": o.Verbose = true; break;
                default:
                    if (arg.StartsWith('-')) throw new ArgumentException($"Onbekende optie {arg}.");
                    if (o.ConfigPath is not null) throw new ArgumentException("Geef precies één configuratiebestand op.");
                    o.ConfigPath = arg;
                    break;
            }
        }
        return o;
    }
}
