using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Obol.Website.Helpers;

/// <summary>A point of a chart, as the page script reads it for the cursor.</summary>
/// <param name="X">Across, 0–100 (% of the width).</param>
/// <param name="Y">Down, 0–100 (% of the height).</param>
/// <param name="Label">The day ("14 Jun").</param>
/// <param name="Value">The value ("€11,212.40").</param>
/// <param name="Note">A second line ("Deposited €9,820"), or empty.</param>
public sealed record ChartPoint(double X, double Y, string Label, string Value, string Note);

/// <summary>An SVG line chart: the line, the area under it, an optional second line, and the points.</summary>
public sealed class LineChart
{
    /// <summary>Gets the width of the viewBox.</summary>
    public double Width { get; init; }

    /// <summary>Gets the height of the viewBox.</summary>
    public double Height { get; init; }

    /// <summary>Gets the path of the line.</summary>
    public string Line { get; init; } = string.Empty;

    /// <summary>Gets the path of the area under the line.</summary>
    public string Area { get; init; } = string.Empty;

    /// <summary>Gets the path of the second line (deposits), or empty.</summary>
    public string Second { get; init; } = string.Empty;

    /// <summary>Gets the height of the guide line (average price), or null.</summary>
    public double? GuideY { get; init; }

    /// <summary>Gets the points.</summary>
    public IReadOnlyList<ChartPoint> Points { get; init; } = [];

    /// <summary>Gets the points as JSON for the page script.</summary>
    public string PointsJson => JsonSerializer.Serialize(Points.Select(static point => new object[]
    {
        Math.Round(point.X, 2), Math.Round(point.Y, 2), point.Label, point.Value, point.Note,
    }));

    /// <summary>Gets the last point (the dot at the end of the line).</summary>
    public ChartPoint? Last => Points.Count > 0 ? Points[^1] : null;

    /// <summary>Gets a value indicating whether there is a line to draw.</summary>
    public bool HasLine => Points.Count > 1;
}

/// <summary>
/// SVG paths for the charts, drawn on the server (the page script only adds the cursor).
/// </summary>
public static class Charts
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// A smooth line chart.
    /// </summary>
    /// <param name="values">The values, oldest first.</param>
    /// <param name="labels">The label of each value (the day).</param>
    /// <param name="texts">The value as text, for the cursor.</param>
    /// <param name="height">The viewBox height (the width is 1000).</param>
    /// <param name="second">A second series on the same scale (deposits), or null.</param>
    /// <param name="notes">The second line of the cursor, or null.</param>
    /// <param name="guide">A horizontal guide on the same scale (average price), or null.</param>
    /// <returns>The chart.</returns>
    public static LineChart Line(
        IReadOnlyList<decimal> values, IReadOnlyList<string> labels, IReadOnlyList<string> texts, double height,
        IReadOnlyList<decimal>? second = null, IReadOnlyList<string>? notes = null, decimal? guide = null)
    {
        const double width = 1000;
        const double top = 14;
        const double bottom = 8;
        if (values.Count == 0)
        {
            return new LineChart { Width = width, Height = height };
        }

        var all = values.Select(static value => (double)value).ToList();
        if (second is not null)
        {
            all.AddRange(second.Select(static value => (double)value));
        }

        if (guide is { } g)
        {
            all.Add((double)g);
        }

        var min = all.Min();
        var max = all.Max();
        if (max - min < 1e-9)
        {
            max += 1;
            min -= 1;
        }

        var pad = (max - min) * 0.06;
        min -= pad;
        max += pad;

        double X(int index) => values.Count == 1 ? width : index * width / (values.Count - 1);
        double Y(double value) => top + ((max - value) / (max - min) * (height - top - bottom));

        var points = values.Select((value, index) => (X: X(index), Y: Y((double)value))).ToList();
        var line = Smooth(points);
        var area = points.Count > 1 ? $"{line} L{N(width)},{N(height)} L0,{N(height)} Z" : string.Empty;
        var secondPath = second is { Count: > 1 } s ? Polyline(s.Select((value, index) => (X(index), Y((double)value))).ToList()) : string.Empty;

        return new LineChart
        {
            Width = width,
            Height = height,
            Line = line,
            Area = area,
            Second = secondPath,
            GuideY = guide is { } level ? Y((double)level) : null,
            Points = points.Select((point, index) => new ChartPoint(
                point.X / width * 100, point.Y / height * 100, labels[index], texts[index], notes is null ? string.Empty : notes[index])).ToList(),
        };
    }

    /// <summary>
    /// A sparkline path in a box.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="width">The box width.</param>
    /// <param name="height">The box height.</param>
    /// <returns>The path, or empty for fewer than two values.</returns>
    public static string Spark(IReadOnlyList<decimal> values, double width = 112, double height = 30)
    {
        if (values.Count < 2)
        {
            return string.Empty;
        }

        var min = (double)values.Min();
        var max = (double)values.Max();
        if (max - min < 1e-9)
        {
            max += 1;
            min -= 1;
        }

        var points = values.Select((value, index) => (
            X: 2 + (index * (width - 4) / (values.Count - 1)),
            Y: 3 + ((max - (double)value) / (max - min) * (height - 6)))).ToList();
        return Polyline(points);
    }

    /// <summary>
    /// The dash pattern of a progress ring.
    /// </summary>
    /// <param name="fraction">0–1.</param>
    /// <param name="radius">The radius.</param>
    /// <returns>"filled gap" for stroke-dasharray.</returns>
    public static string Ring(double fraction, double radius)
    {
        var circumference = 2 * Math.PI * radius;
        var filled = Math.Clamp(fraction, 0, 1) * circumference;
        return $"{N(filled)} {N(circumference)}";
    }

    /// <summary>A number for SVG (invariant, two decimals).</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string N(double value) => Math.Round(value, 2).ToString("0.##", Invariant);

    private static string Polyline(IReadOnlyList<(double X, double Y)> points)
    {
        var text = new StringBuilder();
        for (var i = 0; i < points.Count; i++)
        {
            text.Append(i == 0 ? 'M' : 'L').Append(N(points[i].X)).Append(',').Append(N(points[i].Y)).Append(' ');
        }

        return text.ToString().TrimEnd();
    }

    // Catmull-Rom through the points, as cubic Béziers: smooth, and it never overshoots much with daily points.
    private static string Smooth(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 3)
        {
            return points.Count == 1 ? $"M0,{N(points[0].Y)} L{N(points[0].X)},{N(points[0].Y)}" : Polyline(points);
        }

        var text = new StringBuilder();
        text.Append('M').Append(N(points[0].X)).Append(',').Append(N(points[0].Y));
        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(points.Count - 1, i + 2)];
            var c1 = (X: p1.X + ((p2.X - p0.X) / 6), Y: p1.Y + ((p2.Y - p0.Y) / 6));
            var c2 = (X: p2.X - ((p3.X - p1.X) / 6), Y: p2.Y - ((p3.Y - p1.Y) / 6));
            text.Append(" C").Append(N(c1.X)).Append(',').Append(N(c1.Y)).Append(' ')
                .Append(N(c2.X)).Append(',').Append(N(c2.Y)).Append(' ')
                .Append(N(p2.X)).Append(',').Append(N(p2.Y));
        }

        return text.ToString();
    }
}
