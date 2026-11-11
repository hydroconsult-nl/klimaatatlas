using System.Globalization;
using Klimaatatlas.Core;

namespace Klimaatatlas.Core.Tests;

public class FilterTests
{
    [Theory]
    [InlineData("[waterdiepte]>=3", "waterdiepte", ">=", 3.0)]
    [InlineData(" [diepte] < 1.5 ", "diepte", "<", 1.5)]
    [InlineData("[a]<>0", "a", "<>", 0.0)]
    [InlineData("[a]=2", "a", "=", 2.0)]
    public void ParsesNumericFilters(string text, string field, string op, double operand)
    {
        var filter = Filter.Parse(text);
        Assert.True(filter.IsActive);
        Assert.Equal(field, filter.FieldName);
        Assert.Equal(op, filter.Operator);
        Assert.Equal(operand, filter.Operand);
    }

    [Fact]
    public void KeepsTextOperandAsString() => Assert.Equal("ja", Filter.Parse("[doodlopend]=ja").Operand);

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("[a] like 3")]
    public void UnparseableFilterSelectsEverything(string? text) => Assert.False(Filter.Parse(text).IsActive);

    [Fact]
    public void OperandParsingIgnoresRegionalSettings()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
            Assert.Equal(1.5, Filter.Parse("[a]>1.5").Operand);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}

public class SecondDegreePolynomialTests
{
    [Theory]
    [InlineData("52.185x^2 - 524.98x + 1422.2", 2.0, 52.185 * 4 - 524.98 * 2 + 1422.2)]
    [InlineData("x^2+0x+1", 3.0, 10.0)]
    [InlineData("-x^2 - x + 0.5", 2.0, -5.5)]
    [InlineData("0.5X^2 +2x -1", 2.0, 5.0)]
    public void Evaluates(string expression, double x, double expected) =>
        Assert.Equal(expected, SecondDegreePolynomial.Parse(expression).Evaluate(x), 9);

    [Theory]
    [InlineData("1683.6*EXP(-0.578*x)")]
    [InlineData("2x + 1")]
    public void RejectsOtherForms(string expression)
    {
        Assert.False(SecondDegreePolynomial.TryParse(expression, out _));
        Assert.Throws<FormatException>(() => SecondDegreePolynomial.Parse(expression));
    }

    [Fact]
    public void IgnoresRegionalSettings()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
            Assert.Equal(52.185, SecondDegreePolynomial.Parse("52.185x^2 - 524.98x + 1422.2").A);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}

public class BenchmarkTests
{
    private static Benchmark Continuous(params (double value, double verdict)[] range)
    {
        var b = new Benchmark { Name = "b", ClassificationType = ClassificationType.Continuous };
        foreach (var (value, verdict) in range) b.ValuesRange.Add(value, verdict);
        return b;
    }

    [Theory]
    [InlineData(10.0, 0.0)]   // below range → first verdict
    [InlineData(15.0, 0.0)]
    [InlineData(21.0, 0.5)]   // interpolated
    [InlineData(27.0, 1.0)]
    [InlineData(31.0, 0.5)]
    [InlineData(40.0, 0.0)]   // above range → last verdict
    public void InterpolatesContinuousRange(double input, double expected) =>
        Assert.Equal(expected, Continuous((15, 0), (27, 1), (35, 0)).GetResult(input), 9);

    [Fact]
    public void AcceptsIntegerAndStringInput()
    {
        var b = Continuous((0, 0), (10, 1));
        Assert.Equal(0.5, b.GetResult(5L), 9);
        Assert.Equal(0.25, b.GetResult("2.5"), 9);
    }

    [Fact]
    public void DiscreteMatchesCaseInsensitiveAndTrimmed()
    {
        var b = new Benchmark
        {
            Name = "d",
            ClassificationType = ClassificationType.Discrete,
            DiscreteClasses = [new("Ja", 1), new("nee", 0.2)],
        };
        Assert.Equal(1, b.GetResult(" ja "));
        Assert.Equal(0.2, b.GetResult("NEE"));
        Assert.Equal(0, b.GetResult("onbekend"));
    }

    [Fact]
    public void InputValueUsesFieldThenConstantAndNullValue()
    {
        var b = Continuous((0, 0), (1, 1));
        b.FieldNamesPerScenario["K2023"] = "diepte";
        b.TransformationPerScenario["K2023"] = "";
        b.ConstantsPerScenario["K2050"] = 25;

        Assert.Equal(0.7, b.GetInputValue("k2023", _ => 0.7));          // scenario lookup is case-insensitive
        Assert.Equal(25.0, b.GetInputValue("K2050", _ => throw new InvalidOperationException()));
        Assert.Null(b.GetInputValue("K2100", _ => 1.0));                 // neither field nor constant
        Assert.Null(b.GetInputValue("K2023", _ => DBNull.Value));        // NULL, no nullValue

        b.NullValue = 0;
        Assert.Equal(0.0, b.GetInputValue("K2023", _ => DBNull.Value));
    }

    [Fact]
    public void InputValueAppliesTransformation()
    {
        var b = Continuous((0, 0), (1, 1));
        b.FieldNamesPerScenario["A"] = "x";
        b.TransformationPerScenario["A"] = "x^2+1x+0";
        Assert.Equal(6.0, b.GetInputValue("A", _ => 2.0));
    }
}
