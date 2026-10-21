using MudBlazor;

namespace Zeitwerk.Web.Components.Layout;

public static class ZeitwerkTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#D4C602",
            PrimaryContrastText = "#0A0A0A",
            Secondary = "#4DB6AC",
            Background = "#0D0D0D",
            Surface = "#141414",
            AppbarBackground = "#141414",
            AppbarText = "#E6E6E6",
            DrawerBackground = "#141414",
            TextPrimary = "#E6E6E6",
            TextSecondary = "#9A9A9A",
            LinesDefault = "#262626",
            TableLines = "#262626",
            Divider = "#262626",
            ActionDefault = "#9A9A9A",
            Warning = "#FFB74D",
            Error = "#E57373",
            Success = "#81C784",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" },
    };
}
