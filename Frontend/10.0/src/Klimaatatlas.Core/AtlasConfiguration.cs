using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Klimaatatlas.Core;

/// <summary>
/// The contents of a rekenregels JSON file. Port of clsKlimaatatlas.ReadConfigurationFile,
/// PopulateScenarios and PopulateRules.
/// </summary>
public sealed class AtlasConfiguration
{
    public required string SourcePath { get; init; }
    public required List<string> Scenarios { get; init; }
    public required FeaturesDataset FeaturesDataset { get; init; }

    /// <summary>Rules in execution order (alphabetical by name, like the SortedDictionary of the original).</summary>
    public required List<Rule> Rules { get; init; }

    public static AtlasConfiguration Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        JObject root;
        try
        {
            root = JObject.Parse(File.ReadAllText(fullPath));
        }
        catch (JsonReaderException ex)
        {
            throw new ConfigurationException($"'{fullPath}' is not valid JSON: {ex.Message}", ex);
        }
        return FromJson(root, fullPath);
    }

    public static AtlasConfiguration FromJson(JObject root, string sourcePath)
    {
        var baseDirectory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();

        var scenarios = new List<string>();
        foreach (var token in Required<JArray>(root, "scenarios", "configuration"))
        {
            var name = token.ToString();
            if (scenarios.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase))
                throw new ConfigurationException($"Scenario '{name}' is listed more than once.");
            scenarios.Add(name);
        }

        var dataset = Required<JObject>(root, "features_dataset", "configuration");
        var features = new FeaturesDataset
        {
            Id = (string?)dataset["id"] ?? "",
            Path = ResolvePath(Required<JValue>(dataset, "path", "features_dataset").ToString(), baseDirectory),
            TableName = Required<JValue>(dataset, "tablename", "features_dataset").ToString(),
        };

        var rules = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Required<JArray>(root, "rules", "configuration").OfType<JObject>())
        {
            var rule = ReadRule(item);
            if (!rules.TryAdd(rule.Name.Trim(), rule))
                throw new ConfigurationException($"Rule '{rule.Name}' is defined more than once.");
        }

        return new AtlasConfiguration
        {
            SourcePath = sourcePath,
            Scenarios = scenarios,
            FeaturesDataset = features,
            Rules = rules.Values
                .OrderBy(r => r.Name.Trim().ToUpperInvariant(), StringComparer.InvariantCulture)
                .ToList(),
        };
    }

    private static Rule ReadRule(JObject item)
    {
        var rule = new Rule { Name = Required<JValue>(item, "name", "rule").ToString() };
        var context = $"rule '{rule.Name}'";

        foreach (var benchmarkItem in Required<JArray>(item, "benchmarks", context).OfType<JObject>())
        {
            var benchmark = ReadBenchmark(benchmarkItem, context);
            if (!rule.Benchmarks.TryAdd(benchmark.Name.Trim(), benchmark))
                throw new ConfigurationException($"Benchmark '{benchmark.Name}' is defined more than once in {context}.");
            // Original behaviour: the filter of the last benchmark becomes the filter of the rule.
            rule.Filter = benchmark.Filter;
        }

        foreach (var factor in Required<JArray>(item, "components", context).OfType<JObject>())
        {
            var benchmarkName = Required<JValue>(factor, "benchmark", context).ToString();
            if (!rule.Benchmarks.TryGetValue(benchmarkName.Trim(), out var benchmark))
                throw new ConfigurationException($"Component in {context} refers to unknown benchmark '{benchmarkName}'.");

            rule.Components.Add(new EquationComponent
            {
                Benchmark = benchmark,
                Weight = Required<JValue>(factor, "weight", context).ToObject<double>(),
                ResultsFieldName = Required<JValue>(factor, "resultsfield", context).ToString(),
            });
        }

        return rule;
    }

    private static Benchmark ReadBenchmark(JObject item, string ruleContext)
    {
        var name = Required<JValue>(item, "name", $"benchmark in {ruleContext}").ToString();
        var context = $"benchmark '{name}' of {ruleContext}";

        var classification = string.Equals((string?)item["classification"], "discrete", StringComparison.OrdinalIgnoreCase)
            ? ClassificationType.Discrete
            : ClassificationType.Continuous;

        var benchmark = new Benchmark
        {
            Name = name,
            ClassificationType = classification,
            Filter = Filter.Parse(item.TryGetValue("filter", out var filterToken) ? filterToken.ToString() : "*"),
        };

        if (item["fieldname"] is JArray fieldNames)
        {
            foreach (var entry in fieldNames.OfType<JObject>())
            {
                var scenario = Required<JValue>(entry, "scenario", context).ToString().Trim();
                if (!benchmark.FieldNamesPerScenario.TryAdd(scenario, Required<JValue>(entry, "field", context).ToString()))
                    throw new ConfigurationException($"Scenario '{scenario}' has more than one field in {context}.");
                benchmark.TransformationPerScenario[scenario] = (string?)entry["transformation"] ?? string.Empty;
            }
        }

        if (item["constants"] is JArray constants)
        {
            foreach (var entry in constants.OfType<JObject>())
            {
                var scenario = Required<JValue>(entry, "scenario", context).ToString().Trim();
                if (!benchmark.ConstantsPerScenario.TryAdd(scenario, Required<JValue>(entry, "value", context).ToObject<double>()))
                    throw new ConfigurationException($"Scenario '{scenario}' has more than one constant in {context}.");
            }
        }

        if (classification == ClassificationType.Discrete)
        {
            foreach (var entry in Required<JArray>(item, "classes", context).OfType<JObject>())
            {
                var className = Required<JValue>(entry, "name", context).ToString();
                if (benchmark.DiscreteClasses.Any(c => c.Key == className))
                    throw new ConfigurationException($"Class '{className}' is defined more than once in {context}.");
                benchmark.DiscreteClasses.Add(new(className, Required<JValue>(entry, "value", context).ToObject<double>()));
            }
        }
        else
        {
            foreach (var entry in Required<JArray>(item, "valuesRange", context).OfType<JObject>())
            {
                var value = Required<JValue>(entry, "value", context).ToObject<double>();
                if (!benchmark.ValuesRange.TryAdd(value, Required<JValue>(entry, "verdict", context).ToObject<double>()))
                    throw new ConfigurationException($"Value {value} occurs more than once in the valuesRange of {context}.");
            }
        }

        // The original checked for "nullValue" but then read "nullvalue" (always 0). Accept either spelling.
        var nullValue = item.GetValue("nullValue", StringComparison.OrdinalIgnoreCase);
        if (nullValue is not null && nullValue.Type != JTokenType.Null)
            benchmark.NullValue = nullValue.ToObject<double>();

        return benchmark;
    }

    private static T Required<T>(JObject parent, string key, string context) where T : JToken =>
        parent[key] as T ?? throw new ConfigurationException($"Missing or invalid '{key}' in {context}.");

    /// <summary>Relative paths are resolved against the folder of the configuration file.</summary>
    private static string ResolvePath(string path, string baseDirectory) =>
        Path.IsPathFullyQualified(path) || LooksLikeWindowsAbsolutePath(path)
            ? path
            : Path.GetFullPath(Path.Combine(baseDirectory, path));

    // "c:\..." is not fully qualified on macOS/Linux; leave it untouched so the error message shows the original path.
    private static bool LooksLikeWindowsAbsolutePath(string path) =>
        path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
}

public sealed class FeaturesDataset
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string TableName { get; init; }
}

public sealed class ConfigurationException(string message, Exception? inner = null) : Exception(message, inner);
