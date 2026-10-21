using System.Text.RegularExpressions;

namespace Zeitwerk.Web.Domain;

/// <summary>Wirksame Ticket-Link-Konfiguration eines Projekts.</summary>
public sealed record TicketLinkSettings(string UrlTemplate, string Pattern);

/// <summary>Stück einer Tätigkeit; <see cref="Url"/> ist gesetzt, wenn das Stück eine erkannte Ticketnummer ist.</summary>
public sealed record ActivitySegment(string Text, string? Url = null);

/// <summary>Erkennt Ticketnummern in Tätigkeiten und baut Links aus der URL-Vorlage.</summary>
public static class TicketLinks
{
    public const string Placeholder = "{ticket}";
    public const string DefaultPattern = @"[A-Z][A-Z0-9]+-\d+";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Fehlermeldung zur URL-Vorlage oder <c>null</c>, wenn sie gültig ist.</summary>
    public static string? ValidateTemplate(string template)
    {
        if (!template.Contains(Placeholder, StringComparison.Ordinal))
            return $"Die Ticket-URL muss den Platzhalter {Placeholder} enthalten.";
        return BuildUrl(template, "ABC-1") is null
            ? "Die Ticket-URL muss mit http:// oder https:// beginnen."
            : null;
    }

    /// <summary>Fehlermeldung zum Ticket-Muster oder <c>null</c>, wenn es ein gültiger regulärer Ausdruck ist.</summary>
    public static string? ValidatePattern(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
            return null;
        }
        catch (ArgumentException)
        {
            return "Das Ticket-Muster ist kein gültiger regulärer Ausdruck.";
        }
    }

    /// <summary>
    /// Zerlegt die Tätigkeit in Text- und Ticket-Stücke. Ohne Konfiguration, bei ungültiger Konfiguration oder
    /// Zeitüberschreitung des Musters ist das Ergebnis die ganze Tätigkeit als ein Text-Stück.
    /// Die Stücke enthalten Rohtext; die URL ist geprüft (nur http/https) und die Ticketnummer darin URL-kodiert.
    /// </summary>
    public static IReadOnlyList<ActivitySegment> Split(string? activity, TicketLinkSettings? settings)
    {
        if (string.IsNullOrEmpty(activity))
            return [];
        if (settings is null || ValidateTemplate(settings.UrlTemplate) is not null || ValidatePattern(settings.Pattern) is not null)
            return [new(activity)];

        // Ticketnummern nur als ganzes Wort erkennen, nicht als Teil von „xABC-1“ oder „ABC-12a“.
        var pattern = $@"(?<![\p{{L}}\p{{N}}_])(?:{settings.Pattern})(?![\p{{L}}\p{{N}}_])";
        MatchCollection matches;
        try
        {
            matches = Regex.Matches(activity, pattern, RegexOptions.CultureInvariant, MatchTimeout);
            _ = matches.Count; // wertet alle Treffer aus, damit eine Zeitüberschreitung hier auffällt
        }
        catch (RegexMatchTimeoutException)
        {
            return [new(activity)];
        }

        var segments = new List<ActivitySegment>();
        var position = 0;
        foreach (Match match in matches)
        {
            if (match.Length == 0 || BuildUrl(settings.UrlTemplate, match.Value) is not { } url)
                continue;
            if (match.Index > position)
                segments.Add(new(activity[position..match.Index]));
            segments.Add(new(match.Value, url));
            position = match.Index + match.Length;
        }
        if (position < activity.Length)
            segments.Add(new(activity[position..]));
        return segments;
    }

    /// <summary>Setzt die URL-kodierte Ticketnummer ein; <c>null</c>, wenn keine absolute http(s)-URL entsteht.</summary>
    private static string? BuildUrl(string template, string ticket)
    {
        var url = template.Replace(Placeholder, Uri.EscapeDataString(ticket), StringComparison.Ordinal);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;
    }
}
