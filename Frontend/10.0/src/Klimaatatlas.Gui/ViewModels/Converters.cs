using Avalonia.Data.Converters;

namespace Klimaatatlas.Gui.ViewModels;

public static class Converters
{
    /// <summary>RunFailed → symbol for the result banner.</summary>
    public static readonly IValueConverter ResultSymbol =
        new FuncValueConverter<bool, string>(failed => failed ? "✕" : "✓");

    /// <summary>RunFailed → title for the result banner.</summary>
    public static readonly IValueConverter ResultTitle =
        new FuncValueConverter<bool, string>(failed => failed ? "Klaar met fouten" : "Klaar – gelukt");
}
