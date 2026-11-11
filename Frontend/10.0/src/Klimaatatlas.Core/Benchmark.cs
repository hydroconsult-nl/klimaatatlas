using System.Globalization;

namespace Klimaatatlas.Core;

public enum ClassificationType
{
    Discrete = 0,
    Continuous = 1,
}

/// <summary>
/// A "maatlat": translates an input value into a verdict (risk score), either through discrete classes
/// or through linear interpolation in a value range. Port of clsBenchmark.
/// Scenario keys are case-insensitive, like the upper-cased keys of the original.
/// </summary>
public sealed class Benchmark
{
    private readonly Dictionary<string, SecondDegreePolynomial> _parsedTransformations = new(StringComparer.OrdinalIgnoreCase);

    public required string Name { get; init; }
    public required ClassificationType ClassificationType { get; init; }

    /// <summary>Source field per scenario.</summary>
    public Dictionary<string, string> FieldNamesPerScenario { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional "a x^2 + b x + c" transformation per scenario (empty string = none).</summary>
    public Dictionary<string, string> TransformationPerScenario { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Constant input value per scenario, used when no field is given for that scenario.</summary>
    public Dictionary<string, double> ConstantsPerScenario { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Discrete classes: class name → verdict. Compared case-insensitively and trimmed.</summary>
    public List<KeyValuePair<string, double>> DiscreteClasses { get; init; } = [];

    /// <summary>Continuous classification: value → verdict, sorted by value.</summary>
    public SortedList<double, double> ValuesRange { get; init; } = [];

    /// <summary>Replacement for NULL values in the source field. NaN = no replacement.</summary>
    public double NullValue { get; set; } = double.NaN;

    public Filter Filter { get; init; } = Filter.None;

    /// <summary>
    /// Returns the input value for one record and scenario, after NULL replacement and transformation.
    /// Returns null when this benchmark has neither a field nor a constant for the scenario.
    /// A NULL field value is replaced by <see cref="NullValue"/>; when none is configured that is NaN,
    /// exactly as in the original application. The effect of NaN is:
    /// discrete → verdict 0 (counted in the weights); continuous → NaN, so the rule result for that record is NULL.
    /// </summary>
    public object? GetInputValue(string scenario, Func<string, object?> readField) =>
        GetInputValue(scenario, readField, out _);

    public object? GetInputValue(string scenario, Func<string, object?> readField, out bool wasNull)
    {
        wasNull = false;
        if (FieldNamesPerScenario.TryGetValue(scenario, out var fieldName))
        {
            var value = readField(fieldName);
            if (value is null or DBNull)
            {
                wasNull = true;
                value = NullValue;
            }

            if (TransformationPerScenario.TryGetValue(scenario, out var transformation) && !string.IsNullOrWhiteSpace(transformation))
            {
                if (!_parsedTransformations.TryGetValue(transformation, out var polynomial))
                {
                    polynomial = SecondDegreePolynomial.Parse(transformation);
                    _parsedTransformations[transformation] = polynomial;
                }
                value = polynomial.Evaluate(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            }
            return value;
        }

        if (ConstantsPerScenario.TryGetValue(scenario, out var constant)) return constant;
        return null;
    }

    public double GetResult(object value) => ClassificationType switch
    {
        ClassificationType.Discrete => GetDiscreteResult(value),
        ClassificationType.Continuous => GetContinuousResult(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
        _ => 0,
    };

    private double GetDiscreteResult(object value)
    {
        var text = (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
        foreach (var (name, verdict) in DiscreteClasses)
        {
            if (string.Equals(name.Trim(), text, StringComparison.OrdinalIgnoreCase)) return verdict;
        }
        return 0;
    }

    private double GetContinuousResult(double value)
    {
        if (ValuesRange.Count == 0)
            throw new InvalidOperationException($"Benchmark '{Name}' has no valuesRange.");

        if (double.IsNaN(value)) return double.NaN;

        var keys = ValuesRange.Keys;
        var verdicts = ValuesRange.Values;

        if (value <= keys[0]) return verdicts[0];
        if (value > keys[^1]) return verdicts[^1];

        var i = 0;
        while (value > keys[i]) i++;

        double x0 = keys[i - 1], x1 = keys[i], y0 = verdicts[i - 1], y1 = verdicts[i];
        return y0 + (value - x0) * (y1 - y0) / (x1 - x0);
    }
}
