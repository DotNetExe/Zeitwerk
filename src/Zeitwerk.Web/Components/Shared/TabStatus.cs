using System.Text.RegularExpressions;
using Zeitwerk.Web.Domain;

namespace Zeitwerk.Web.Components.Shared;

/// <summary>
/// Timer-Zustand für den Browser-Tab: Titel und Favicon (SVG-Data-URL).
/// Ändert sich höchstens minütlich oder bei Zustandswechsel, damit der Tab nicht flackert.
/// </summary>
public sealed partial record TabStatus(string Title, string Icon)
{
    /// <summary><c>null</c> im Leerlauf – dann gelten Seitentitel und normales Logo.</summary>
    public static TabStatus? For(TimeEntry? open, DateTimeOffset now, Func<DateTimeOffset, DateTime> toLocal)
    {
        if (open is null)
            return null;

        var color = HexColor().IsMatch(open.Project.Color) ? open.Project.Color : ProjectColors.Palette[0];
        return open.IsOnHold(now)
            ? new($"⏸ ab {toLocal(open.Start):HH:mm} · {open.Project.DisplayName} – Zeitwerk", Svg(HoldIcon(color)))
            : new($"● {Format.Duration(open.Duration(now))} · {open.Project.DisplayName} – Zeitwerk", Svg(RunningIcon(color)));
    }

    private static string RunningIcon(string color) =>
        $"""<circle cx="32" cy="32" r="21" fill="{color}"/>""";

    private static string HoldIcon(string color) =>
        $"""<circle cx="32" cy="32" r="18" fill="none" stroke="{color}" stroke-width="6"/><path d="M27 24v16M37 24v16" stroke="{color}" stroke-width="5" stroke-linecap="round"/>""";

    private static string Svg(string content) =>
        "data:image/svg+xml," + Uri.EscapeDataString(
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" rx="14" fill="#141414"/>{content}</svg>""");

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
