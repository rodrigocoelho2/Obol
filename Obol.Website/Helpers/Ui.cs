using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Obol.Sdk.Models;

namespace Obol.Website.Helpers;

/// <summary>
/// Small pieces of markup used on many screens.
/// </summary>
public static class Ui
{
    private static readonly HtmlEncoder Html = HtmlEncoder.Default;

    /// <summary>
    /// The badge of an instrument: its ticker in the colour of its asset class, with the company logo over it when logos
    /// are on (the logo removes itself when there is none, uncovering the ticker).
    /// </summary>
    /// <param name="instrument">The instrument.</param>
    /// <param name="logos">Whether logos are on.</param>
    /// <param name="size">"", "sm" or "lg".</param>
    /// <returns>The markup.</returns>
    public static IHtmlContent Asset(InstrumentDTO? instrument, bool logos, string size = "")
    {
        if (instrument is null)
        {
            return new HtmlString($"<span class=\"asset other {size}\" aria-hidden=\"true\">—</span>");
        }

        var symbol = instrument.Symbol.Length > 5 ? instrument.Symbol[..5] : instrument.Symbol;
        var css = Fmt.ClassCss(instrument.Class);
        var img = logos
            ? $"<img src=\"/logo/{Uri.EscapeDataString(instrument.Key)}?s={Uri.EscapeDataString(instrument.Symbol)}\" alt=\"\" loading=\"lazy\" decoding=\"async\" onerror=\"this.remove()\">"
            : string.Empty;
        return new HtmlString($"<span class=\"asset {css} {size}\" aria-hidden=\"true\">{Html.Encode(symbol)}{img}</span>");
    }

    /// <summary>
    /// The link to a position.
    /// </summary>
    /// <param name="instrument">The instrument.</param>
    /// <returns>/holdings/{key}.</returns>
    public static string Href(InstrumentDTO instrument) => "/holdings/" + Uri.EscapeDataString(instrument.Key);

    /// <summary>
    /// A change as a pill: lime up, coral down.
    /// </summary>
    /// <param name="share">The change (0.0058 = 0.58 %).</param>
    /// <param name="solid">A solid pill (the hero) instead of a soft one.</param>
    /// <param name="small">A small pill.</param>
    /// <returns>The markup.</returns>
    public static IHtmlContent Change(decimal? share, bool solid = false, bool small = false)
    {
        if (share is null)
        {
            return HtmlString.Empty;
        }

        var css = "pill" + (solid ? " solid" : " soft") + (share < 0 ? " loss" : string.Empty) + (small ? " sm" : string.Empty);
        return new HtmlString($"<span class=\"{css}\">{Html.Encode(Fmt.Arrow(share))}</span>");
    }

    /// <summary>
    /// A money amount with the cents lighter, for the big numbers.
    /// </summary>
    /// <param name="value">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The markup.</returns>
    public static IHtmlContent BigMoney(decimal value, string? currency)
    {
        var (whole, cents) = Fmt.MoneyParts(value, currency);
        return new HtmlString($"{Html.Encode(whole)}<span class=\"c\">{Html.Encode(cents)}</span>");
    }
}
