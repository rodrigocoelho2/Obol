using Microsoft.AspNetCore.Html;

namespace Obol.Website.Helpers;

/// <summary>
/// The line icons (24×24, stroke = currentColor). Shipped with the app: nothing loads from the internet.
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal)
    {
        ["overview"] = "<path d=\"M3 17l5-5 4 3 8-8\"/><path d=\"M15 7h5v5\"/>",
        ["holdings"] = "<rect x=\"3.5\" y=\"4\" width=\"17\" height=\"6.5\" rx=\"2\"/><rect x=\"3.5\" y=\"13.5\" width=\"17\" height=\"6.5\" rx=\"2\"/>",
        ["insights"] = "<path d=\"M4 20V13\"/><path d=\"M10 20V8\"/><path d=\"M16 20v-5\"/><path d=\"M20.5 4.5l-5 5-3-3-4.5 4.5\"/>",
        ["income"] = "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><circle cx=\"12\" cy=\"12\" r=\"3.8\"/>",
        ["sync"] = "<path d=\"M19.5 10.5A7.8 7.8 0 0 0 5.6 7.4M4.5 4v4h4\"/><path d=\"M4.5 13.5a7.8 7.8 0 0 0 13.9 3.1M19.5 20v-4h-4\"/>",
        ["activity"] = "<path d=\"M4 7h16M4 12h16M4 17h10\"/>",
        ["settings"] = "<circle cx=\"12\" cy=\"12\" r=\"3\"/><path d=\"M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z\"/>",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"M16 16l4 4\"/>",
        ["sort"] = "<path d=\"M7 4v16M3 16l4 4 4-4M14 6h7M14 11h5M14 16h3\"/>",
        ["left"] = "<path d=\"M15 5l-7 7 7 7\"/>",
        ["right"] = "<path d=\"M9 5l7 7-7 7\"/>",
        ["down"] = "<path d=\"M6 9l6 6 6-6\"/>",
        ["arrow"] = "<path d=\"M5 12h14M13 6l6 6-6 6\"/>",
        ["check"] = "<path d=\"M5 12.5l4.5 4.5L19 7.5\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["minus"] = "<path d=\"M5 12h14\"/>",
        ["upload"] = "<path d=\"M12 15V4M7.5 8.5 12 4l4.5 4.5\"/><path d=\"M4 15v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3\"/>",
        ["logout"] = "<path d=\"M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3M10 16l-4-4 4-4M6 12h10\"/>",
        ["user"] = "<circle cx=\"12\" cy=\"8.5\" r=\"4\"/><path d=\"M4.5 20a7.5 7.5 0 0 1 15 0\"/>",
        ["lock"] = "<rect x=\"4.5\" y=\"10.5\" width=\"15\" height=\"10\" rx=\"3\"/><path d=\"M8 10.5V8a4 4 0 0 1 8 0v2.5\"/>",
        ["eye"] = "<path d=\"M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12Z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        ["info"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 11v6M12 7.5v.5\"/>",
        ["key"] = "<circle cx=\"8\" cy=\"15\" r=\"4\"/><path d=\"M11 12l8-8M16 7l2 2M14 9l2 2\"/>",
        ["shield"] = "<path d=\"M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6zM9 12l2 2 4-4\"/>",
        ["server"] = "<rect x=\"3.5\" y=\"4\" width=\"17\" height=\"7\" rx=\"2.5\"/><rect x=\"3.5\" y=\"13\" width=\"17\" height=\"7\" rx=\"2.5\"/><path d=\"M7.5 7.5h.01M7.5 16.5h.01\"/>",
        ["trash"] = "<path d=\"M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13\"/>",
        ["calendar"] = "<rect x=\"4\" y=\"5\" width=\"16\" height=\"15\" rx=\"3\"/><path d=\"M4 10h16M9 3v4M15 3v4\"/>",
        ["star"] = "<path d=\"M12 3.8l2.5 5.2 5.7.8-4.1 4 1 5.6L12 16.7l-5.1 2.7 1-5.6-4.1-4 5.7-.8z\"/>",
        ["file"] = "<path d=\"M6 3h9l4 4v14H6z\"/><path d=\"M14 3v5h5M9 13h7M9 17h5\"/>",
        ["bank"] = "<path d=\"M3 9.5L12 4l9 5.5M5 10v8M10 10v8M14 10v8M19 10v8M3 20h18\"/>",
        ["percent"] = "<path d=\"M19 5L5 19\"/><circle cx=\"7\" cy=\"7\" r=\"2.5\"/><circle cx=\"17\" cy=\"17\" r=\"2.5\"/>",
        ["sparkle"] = "<path d=\"M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z\"/>",
        ["link"] = "<path d=\"M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1\"/>",
        ["x"] = "<path d=\"M6 6l12 12M18 6L6 18\"/>",
        ["globe"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M3 12h18M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3z\"/>",
        ["phone"] = "<rect x=\"7\" y=\"3\" width=\"10\" height=\"18\" rx=\"2.5\"/><path d=\"M11 18h2\"/>",
        ["other"] = "<circle cx=\"12\" cy=\"12\" r=\"8\"/><path d=\"M9 12l2 2 4-4\"/>",
    };

    /// <summary>
    /// An icon.
    /// </summary>
    /// <param name="name">Its name.</param>
    /// <param name="cssClass">An extra class.</param>
    /// <returns>The SVG (decorative: hidden from screen readers).</returns>
    public static IHtmlContent Get(string name, string? cssClass = null) =>
        new HtmlString($"<svg class=\"ic{(cssClass is null ? string.Empty : " " + cssClass)}\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">{(Paths.TryGetValue(name, out var path) ? path : Paths["other"])}</svg>");

    /// <summary>
    /// The Obol mark: a lime coin with an engraved ring.
    /// </summary>
    /// <param name="size">The size in px.</param>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Logo(int size = 34) => new HtmlString(
        $"<svg class=\"logo-mark\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 40 40\" aria-hidden=\"true\"><circle cx=\"20\" cy=\"20\" r=\"19\" fill=\"#C6F36D\"/><circle cx=\"20\" cy=\"20\" r=\"12.5\" fill=\"none\" stroke=\"#0B0F0E\" stroke-width=\"1.4\" stroke-dasharray=\"1.6 2.2\"/><circle cx=\"20\" cy=\"20\" r=\"8\" fill=\"none\" stroke=\"#0B0F0E\" stroke-width=\"2.6\"/><circle cx=\"20\" cy=\"20\" r=\"2.4\" fill=\"#0B0F0E\"/></svg>");

    /// <summary>
    /// The engraved rings behind a "minted" card.
    /// </summary>
    /// <returns>The SVG.</returns>
    public static IHtmlContent Rings() => new HtmlString(
        "<svg class=\"rings\" viewBox=\"0 0 300 300\" aria-hidden=\"true\"><g fill=\"none\" stroke=\"currentColor\"><circle cx=\"150\" cy=\"150\" r=\"146\"/><circle cx=\"150\" cy=\"150\" r=\"128\"/><circle cx=\"150\" cy=\"150\" r=\"110\" stroke-dasharray=\"2 5\"/><circle cx=\"150\" cy=\"150\" r=\"92\"/><circle cx=\"150\" cy=\"150\" r=\"74\"/><circle cx=\"150\" cy=\"150\" r=\"56\" stroke-dasharray=\"2 5\"/><circle cx=\"150\" cy=\"150\" r=\"38\"/></g></svg>");
}
