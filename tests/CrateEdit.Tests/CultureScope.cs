using System.Globalization;

namespace CrateEdit.Tests;

/// <summary>Switches the current culture until disposed: <c>using var _ = CultureScope.Ui("de-DE");</c>.</summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    private CultureScope()
    {
    }

    /// <summary>The display language, which selects the resource strings.</summary>
    public static CultureScope Ui(string name)
    {
        var scope = new CultureScope();
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        return scope;
    }

    /// <summary>The format culture, which formats numbers and dates.</summary>
    public static CultureScope Formatting(string name)
    {
        var scope = new CultureScope();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        return scope;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }
}
