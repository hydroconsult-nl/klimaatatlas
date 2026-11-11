namespace Klimaatatlas.Core;

/// <summary>
/// A "rekenregel": a weighted average of benchmark verdicts. Port of clsRule (data part; execution lives in <see cref="RuleEngine"/>).
/// </summary>
public sealed class Rule
{
    public required string Name { get; init; }

    /// <summary>Benchmarks by name (case-insensitive), in configuration order.</summary>
    public Dictionary<string, Benchmark> Benchmarks { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<EquationComponent> Components { get; } = [];

    /// <summary>
    /// The record filter for this rule. As in the original application this is the filter of the
    /// <em>last</em> benchmark in the rule (filters are written per benchmark but applied per rule).
    /// </summary>
    public Filter Filter { get; set; } = Filter.None;

    /// <summary>Distinct source fields needed for the given scenario, in benchmark order.</summary>
    public List<string> GetFieldNamesForScenario(string scenario)
    {
        var fields = new List<string>();
        foreach (var benchmark in Benchmarks.Values)
        {
            if (benchmark.FieldNamesPerScenario.TryGetValue(scenario, out var field) &&
                !fields.Contains(field, StringComparer.OrdinalIgnoreCase))
                fields.Add(field);
        }
        return fields;
    }

    public string ResultColumn(string scenario) => $"{scenario}_{Name}";
}

/// <summary>One term of a rule: a benchmark, its weight and the column that receives its relative contribution.</summary>
public sealed class EquationComponent
{
    public required Benchmark Benchmark { get; init; }
    public required double Weight { get; init; }
    public required string ResultsFieldName { get; init; }

    public string ResultColumn(string scenario) => $"{scenario}_{ResultsFieldName}";
}
