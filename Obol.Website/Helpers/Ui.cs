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

    /// <summary>
    /// A gain as a percentage pill and money, both lime when up and coral when down: "▲ 18.3% +€399.30".
    /// </summary>
    /// <param name="gain">The gain.</param>
    /// <param name="share">The gain as a share of the cost.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="decimals">Decimals of the money.</param>
    /// <returns>The markup.</returns>
    public static IHtmlContent Return(decimal gain, decimal? share, string? currency, int decimals = 2)
    {
        var css = Math.Round(gain, decimals) < 0 ? "down" : Math.Round(gain, decimals) > 0 ? "up" : "flat";
        var pill = share is null ? string.Empty : $"<i>{Html.Encode(Fmt.Arrow(share, 1))}</i>";
        return new HtmlString($"<span class=\"ret {css}\">{pill}<b>{Html.Encode(Fmt.SignedMoney(gain, currency, decimals))}</b></span>");
    }

    /// <summary>
    /// The icon of a Trading 212 pie: a ring split by the share of each instrument.
    /// </summary>
    /// <param name="shares">The shares of the instruments (any scale), biggest first.</param>
    /// <param name="size">"sm" for the computer table, "" for the phone list.</param>
    /// <returns>The markup.</returns>
    public static IHtmlContent PieIcon(IEnumerable<decimal> shares, string size = "")
    {
        var parts = shares.Where(static share => share > 0).ToList();
        var total = parts.Sum();
        const double Ring = 2 * Math.PI * 14;
        var svg = new System.Text.StringBuilder("<circle class=\"track\" cx=\"19\" cy=\"19\" r=\"14\"/>");
        var offset = 0d;
        for (var i = 0; i < parts.Count && total > 0; i++)
        {
            // Six colours; past the sixth the rest of the ring is one grey slice.
            var share = i < 5 || parts.Count == 6 ? (double)(parts[i] / total) : 1 - offset;
            var css = i < 5 || parts.Count == 6 ? "p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) : "rest";
            svg.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"<circle class=\"{css}\" cx=\"19\" cy=\"19\" r=\"14\" stroke-dasharray=\"{Math.Max(share * Ring - 0.8, 0.1):0.##} {Ring:0.##}\" stroke-dashoffset=\"{-offset * Ring:0.##}\" transform=\"rotate(-90 19 19)\"/>");
            offset += share;
            if (css == "rest")
            {
                break;
            }
        }

        return new HtmlString($"<span class=\"pie-ico {size}\" aria-hidden=\"true\"><svg viewBox=\"0 0 38 38\">{svg}</svg></span>");
    }
}
