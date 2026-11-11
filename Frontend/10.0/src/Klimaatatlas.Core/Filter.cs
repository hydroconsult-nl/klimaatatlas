using System.Globalization;

namespace Klimaatatlas.Core;

/// <summary>
/// A simple record filter such as <c>[waterdiepte]&gt;=3</c>. Port of clsFilter.
/// </summary>
public sealed class Filter
{
    private static readonly string[] TwoCharOperators = [">=", "<=", "<>"];
    private static readonly string[] OneCharOperators = [">", "<", "="];

    public string? FieldName { get; private set; }
    public string? Operator { get; private set; }

    /// <summary>A <see cref="double"/> when the operand is numeric, otherwise the raw string.</summary>
    public object? Operand { get; private set; }

    /// <summary>True when the filter selects a subset (i.e. it was parsed successfully).</summary>
    public bool IsActive => !string.IsNullOrEmpty(FieldName) && Operator is not null;

    public string Text { get; private set; } = "*";

    public static Filter None { get; } = new();

    /// <summary>
    /// Parses a filter string. Anything that cannot be parsed (for instance "*") yields an inactive filter,
    /// which means "select all records", exactly like the original implementation.
    /// </summary>
    public static Filter Parse(string? filterText)
    {
        var filter = new Filter { Text = filterText ?? "*" };
        var s = (filterText ?? string.Empty).Trim();

        int open = s.IndexOf('['), close = s.IndexOf(']');
        if (open < 0 || close < open) return filter;

        var fieldName = s.Substring(open + 1, close - open - 1);
        s = s[(close + 1)..].Trim();

        string? op = TwoCharOperators.FirstOrDefault(s.StartsWith)
                     ?? OneCharOperators.FirstOrDefault(s.StartsWith);
        if (op is null) return filter;

        var operandText = s[op.Length..].Trim();

        filter.FieldName = fieldName;
        filter.Operator = op;
        // Culture-invariant: "1.5" means one and a half on every machine, regardless of regional settings.
        filter.Operand = double.TryParse(operandText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : operandText;
        return filter;
    }

    public override string ToString() => Text;
}
