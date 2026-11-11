using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Klimaatatlas.Core;

namespace Klimaatatlas.Gui.ViewModels;

/// <summary>A rule as shown in the "Maatlatten" and "Rekenregels" tabs, plus its selection for a run.</summary>
public sealed partial class RuleViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;

    public RuleViewModel(Rule rule)
    {
        Rule = rule;
        var totalWeight = rule.Components.Sum(c => c.Weight);
        Components = rule.Components
            .Select(c => new ComponentViewModel(c, totalWeight))
            .ToList();
        Benchmarks = rule.Benchmarks.Values
            .Select(b => new BenchmarkViewModel(b, rule.Components.Where(c => c.Benchmark == b).Sum(c => c.Weight)))
            .ToList();
    }

    public Rule Rule { get; }
    public string Name => Rule.Name;
    public string FilterText => Rule.Filter.IsActive ? Rule.Filter.Text : "alle records";
    public IReadOnlyList<ComponentViewModel> Components { get; }
    public IReadOnlyList<BenchmarkViewModel> Benchmarks { get; }

    public string Formula
    {
        get
        {
            var terms = Rule.Components.Select(c => $"{Number(c.Weight)} × {c.Benchmark.Name}");
            var total = Rule.Components.Sum(c => c.Weight);
            return $"{Name} = ({string.Join(" + ", terms)}) / {Number(total)}";
        }
    }

    public string OutputColumns => $"{{scenario}}_{Name}, " + string.Join(", ", Rule.Components.Select(c => $"{{scenario}}_{c.ResultsFieldName}"));

    internal static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed class ComponentViewModel(EquationComponent component, double totalWeight)
{
    public string Benchmark => component.Benchmark.Name;
    public string Weight => RuleViewModel.Number(component.Weight);
    public string Share => totalWeight == 0 ? "–" : $"{component.Weight / totalWeight:P0}";
    public string ResultsField => $"{{scenario}}_{component.ResultsFieldName}";
}

public sealed class BenchmarkViewModel(Benchmark benchmark, double weight)
{
    public Benchmark Benchmark { get; } = benchmark;
    public string Name => Benchmark.Name;

    public string Kind => Benchmark.ClassificationType == ClassificationType.Discrete ? "discreet" : "continu";

    public string WeightText => weight == 0 ? "niet gebruikt in de formule" : $"weging {RuleViewModel.Number(weight)}";

    public string Inputs
    {
        get
        {
            var parts = new List<string>();
            foreach (var (scenario, fieldName) in Benchmark.FieldNamesPerScenario)
            {
                var transformation = Benchmark.TransformationPerScenario.GetValueOrDefault(scenario);
                parts.Add(string.IsNullOrWhiteSpace(transformation)
                    ? $"{scenario}: {fieldName}"
                    : $"{scenario}: {transformation.Replace("x", $"[{fieldName}]")}");
            }
            foreach (var (scenario, constant) in Benchmark.ConstantsPerScenario)
                parts.Add($"{scenario}: constante {RuleViewModel.Number(constant)}");
            return string.Join("   ·   ", parts);
        }
    }

    public string NullText => double.IsNaN(Benchmark.NullValue)
        ? Benchmark.ClassificationType == ClassificationType.Discrete
            ? "lege waarde → oordeel 0"
            : "lege waarde → regeluitkomst leeg"
        : $"lege waarde → {RuleViewModel.Number(Benchmark.NullValue)}";

    public string FilterText => Benchmark.Filter.IsActive ? $"filter {Benchmark.Filter.Text}" : "";
}
