using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
///     3D-lijnsegment
/// </summary>
public class JZLine
{
    /// <summary>
    ///     Constructor
    /// </summary>
    public JZLine()
    {
    }

    /// <summary>
    ///     Constructor
    /// </summary>
    public JZLine(JZPoint p0, JZPoint p1)
    {
        P0 = p0;
        P1 = p1;
    }

    /// <summary>
    ///     Constructor met vier doubles als parameters
    /// </summary>
    /// <param name="x0">Beginpunt X-coördinaat</param>
    /// <param name="y0">Beginpunt Y-coördinaat</param>
    /// <param name="z0">Beginpunt Z-coördinaat</param>
    /// <param name="x1">Eindpunt X-coördinaat</param>
    /// <param name="y1">Eindpunt Y-coördinaat</param>
    /// <param name="z1">Eindpunt Z-coördinaat</param>
    public JZLine(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        P0 = new JZPoint(x0, y0, z0);
        P1 = new JZPoint(x1, y1, z1);
    }

    /// <summary>
    ///     Constructor met vier doubles als parameters
    /// </summary>
    /// <param name="x0">Beginpunt X-coördinaat</param>
    /// <param name="y0">Beginpunt Y-coördinaat</param>
    /// <param name="z0">Beginpunt Z-coördinaat</param>
    /// <param name="x1">Eindpunt X-coördinaat</param>
    /// <param name="y1">Eindpunt Y-coördinaat</param>
    /// <param name="z1">Eindpunt Z-coördinaat</param>
    public JZLine(double x0, double y0, double x1, double y1)
    {
        P0 = new JZPoint(x0, y0, 0);
        P1 = new JZPoint(x1, y1, 0);
    }

    /// <summary>
    ///     Beginpunt
    /// </summary>
    [JsonProperty("p0")]
    public JZPoint P0 { get; set; }

    /// <summary>
    ///     Eindpunt
    /// </summary>
    [JsonProperty("p1")]
    public JZPoint P1 { get; set; }

    /// <summary>
    ///     Haalt de lengte van het lijnsegment op
    /// </summary>
    public double GetLength()
    {
        if (P0 == null || P1 == null)
            throw new InvalidOperationException("JZLine must have both P0 and P1 defined to calculate length.");

        // Bereken de afstand tussen de 3D-punten
        var dx = P1.X - P0.X;
        var dy = P1.Y - P0.Y;
        var dz = P1.Z - P0.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    ///     Haalt de richting van het lijnsegment op
    ///     Retourneert een genormaliseerd JZPoint-object als richtingsvector
    /// </summary>
    public JZPoint GetDirection()
    {
        if (P0 == null || P1 == null)
            throw new InvalidOperationException("JZLine must have both P0 and P1 defined to calculate direction.");

        // Bereken de richtingsvector
        var dx = P1.X - P0.X;
        var dy = P1.Y - P0.Y;
        var dz = P1.Z - P0.Z;

        // Bereken de lengte (norm) van de vector
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        if (length == 0)
            throw new InvalidOperationException("Cannot determine direction for a line with zero length.");

        // Retourneer de genormaliseerde vector
        return new JZPoint(dx / length, dy / length, dz / length);
    }

    /// <summary>
    ///     Omzetten naar een Revit Line
    ///     Eenheidsconversie: mm -> ft
    /// </summary>
    public static Line ToLine(JZLine jzLine)
    {
        if (jzLine.P0 == null || jzLine.P1 == null) return null;

        return Line.CreateBound(JZPoint.ToXYZ(jzLine.P0), JZPoint.ToXYZ(jzLine.P1));
    }
}