using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Klimaatatlas.Core;

namespace Klimaatatlas.Gui.Controls;

/// <summary>
/// Draws a benchmark as a colour bar: verdict 0 = green, 0.5 = yellow, 1 = red.
/// Continuous: a gradient through the actual breakpoints of the valuesRange, with the values underneath.
/// Discrete: one cell per class, labelled with the class name.
/// Replaces clsColorScale (which drew continuous scales as a fixed green-yellow-red gradient).
/// </summary>
public sealed class ColorScale : Control
{
    public static readonly StyledProperty<Benchmark?> BenchmarkProperty =
        AvaloniaProperty.Register<ColorScale, Benchmark?>(nameof(Benchmark));

    static ColorScale() => AffectsRender<ColorScale>(BenchmarkProperty);

    private const double BarHeight = 28;
    private const double LabelHeight = 18;

    public Benchmark? Benchmark
    {
        get => GetValue(BenchmarkProperty);
        set => SetValue(BenchmarkProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width, BarHeight + LabelHeight);

    public override void Render(DrawingContext context)
    {
        var benchmark = Benchmark;
        if (benchmark is null || Bounds.Width < 20) return;

        // leave room so the outer labels are not cut off
        var bar = new Rect(16, 0, Bounds.Width - 32, BarHeight);
        var textBrush = GetTextBrush();

        if (benchmark.ClassificationType == ClassificationType.Continuous)
            RenderContinuous(context, benchmark, bar, textBrush);
        else
            RenderDiscrete(context, benchmark, bar);
    }

    private static void RenderContinuous(DrawingContext context, Benchmark benchmark, Rect bar, IBrush textBrush)
    {
        var range = benchmark.ValuesRange;
        if (range.Count == 0) return;

        double min = range.Keys[0], max = range.Keys[^1], span = max - min;
        if (span <= 0)
        {
            context.FillRectangle(new SolidColorBrush(VerdictColor(range.Values[0])), bar, 4);
            DrawLabel(context, Format(min), bar.Center.X, bar.Bottom + 2, textBrush);
            return;
        }

        // The verdict is linear between breakpoints, but the colour scale bends through yellow,
        // so sample each segment instead of letting the brush blend green straight into red (brown).
        const int samplesPerSegment = 12;
        var stops = new GradientStops { new GradientStop(VerdictColor(range.Values[0]), 0) };
        for (var i = 1; i < range.Count; i++)
        {
            for (var s = 1; s <= samplesPerSegment; s++)
            {
                var t = (double)s / samplesPerSegment;
                var value = range.Keys[i - 1] + t * (range.Keys[i] - range.Keys[i - 1]);
                var verdict = range.Values[i - 1] + t * (range.Values[i] - range.Values[i - 1]);
                stops.Add(new GradientStop(VerdictColor(verdict), (value - min) / span));
            }
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = stops,
        };
        context.FillRectangle(brush, bar, 4);

        // a tick and value at every breakpoint; skip labels that would overlap the previous one
        var lastLabelRight = double.NegativeInfinity;
        for (var i = 0; i < range.Count; i++)
        {
            var x = bar.Left + (range.Keys[i] - min) / span * bar.Width;
            context.DrawLine(new Pen(textBrush, 1), new Point(x, bar.Bottom - 6), new Point(x, bar.Bottom));
            var text = MakeText(Format(range.Keys[i]), textBrush);
            var left = x - text.Width / 2;
            if (left < lastLabelRight + 4) continue;
            context.DrawText(text, new Point(left, bar.Bottom + 2));
            lastLabelRight = left + text.Width;
        }
    }

    private static void RenderDiscrete(DrawingContext context, Benchmark benchmark, Rect bar)
    {
        var classes = benchmark.DiscreteClasses;
        if (classes.Count == 0) return;

        var width = bar.Width / classes.Count;
        for (var i = 0; i < classes.Count; i++)
        {
            var cell = new Rect(bar.Left + i * width, bar.Top, width, bar.Height).Deflate(new Thickness(i == 0 ? 0 : 1, 0, 0, 0));
            var color = VerdictColor(classes[i].Value);
            context.FillRectangle(new SolidColorBrush(color), cell, 4);

            // dark text on light colours, so the class name stays readable
            var luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
            var text = MakeText($"{classes[i].Key} ({Format(classes[i].Value)})", luminance > 140 ? Brushes.Black : Brushes.White);
            if (text.Width < cell.Width - 4)
                context.DrawText(text, new Point(cell.Center.X - text.Width / 2, cell.Center.Y - text.Height / 2));
        }
    }

    /// <summary>0 = green, 0.5 = yellow, 1 = red (values outside 0..1 are clamped).</summary>
    internal static Color VerdictColor(double verdict)
    {
        var v = double.IsNaN(verdict) ? 0 : Math.Clamp(verdict, 0, 1);
        Color green = Color.FromRgb(46, 160, 67), yellow = Color.FromRgb(240, 200, 40), red = Color.FromRgb(214, 48, 49);
        return v <= 0.5 ? Lerp(green, yellow, v * 2) : Lerp(yellow, red, (v - 0.5) * 2);
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    private IBrush GetTextBrush() =>
        this.TryFindResource("TextControlForeground", ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush
            : Brushes.Gray;

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static FormattedText MakeText(string text, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, brush);

    private static void DrawLabel(DrawingContext context, string text, double centerX, double top, IBrush brush)
    {
        var formatted = MakeText(text, brush);
        context.DrawText(formatted, new Point(centerX - formatted.Width / 2, top));
    }
}
