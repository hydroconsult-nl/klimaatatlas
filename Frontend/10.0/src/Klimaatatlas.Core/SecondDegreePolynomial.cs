using System.Globalization;
using System.Text.RegularExpressions;

namespace Klimaatatlas.Core;

/// <summary>
/// A transformation of the form <c>a x^2 + b x + c</c>, e.g. "52.185x^2 - 524.98x + 1422.2".
/// Port of clsGeneralFunctions.EvaluateSecondDegreePolynomeExpression.
/// </summary>
public sealed partial class SecondDegreePolynomial
{
    public double A { get; }
    public double B { get; }
    public double C { get; }

    private SecondDegreePolynomial(double a, double b, double c) => (A, B, C) = (a, b, c);

    [GeneratedRegex(@"^([+-]?\d*\.?\d*)x\^2([+-]\d*\.?\d*)x([+-]\d*\.?\d+)$")]
    private static partial Regex Pattern();

    public static bool TryParse(string expression, out SecondDegreePolynomial? polynomial)
    {
        polynomial = null;
        var compact = expression.ToLowerInvariant().Replace(" ", "");
        var match = Pattern().Match(compact);
        if (!match.Success) return false;

        if (!TryCoefficient(match.Groups[1].Value, out var a) ||
            !TryCoefficient(match.Groups[2].Value, out var b) ||
            !double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var c))
            return false;

        polynomial = new SecondDegreePolynomial(a, b, c);
        return true;
    }

    public static SecondDegreePolynomial Parse(string expression) =>
        TryParse(expression, out var p)
            ? p!
            : throw new FormatException(
                $"Error evaluating mathematical expression {expression}. Please check if your equation obeys the a * x^2 + b * x + c format convention.");

    public double Evaluate(double x) => A * x * x + B * x + C;

    // An empty coefficient means 1 ("x^2"), a bare sign means +1 or -1 ("-x^2", "+x").
    private static bool TryCoefficient(string text, out double value)
    {
        switch (text)
        {
            case "" or "+": value = 1; return true;
            case "-": value = -1; return true;
            default: return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
