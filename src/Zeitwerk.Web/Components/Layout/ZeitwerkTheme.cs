using MudBlazor;

namespace Zeitwerk.Web.Components.Layout;

public static class ZeitwerkTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#6EA8FE",
            Secondary = "#4DB6AC",
            Background = "#111317",
            Surface = "#1A1D23",
            AppbarBackground = "#16181D",
            AppbarText = "#E6E8EB",
            DrawerBackground = "#16181D",
            TextPrimary = "#E6E8EB",
            TextSecondary = "#9AA1AC",
            LinesDefault = "#2A2E36",
            TableLines = "#2A2E36",
            Divider = "#2A2E36",
            ActionDefault = "#9AA1AC",
            Warning = "#FFB74D",
            Error = "#E57373",
            Success = "#81C784",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" },
    };
}
