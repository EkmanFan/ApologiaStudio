namespace ApologiaStudio.Application.Knowledge.Ingestion;

/// <summary>
/// Axis-aligned bounding box in PDF user-space coordinates.
/// </summary>
public sealed record PdfBoundingBox(
    double Left,
    double Bottom,
    double Right,
    double Top)
{
    public double Width => Right - Left;
    public double Height => Top - Bottom;
}
