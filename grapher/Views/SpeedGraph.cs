using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using grapher.ViewModels;

namespace grapher.Views;

public sealed class SpeedGraph : Control
{
    private const double PixelsPerBin = 2;
    private const double LabelFontSize = 11;

    private double[] bins = Array.Empty<double>();
    private Color foreground = Colors.Black;
    private Color line = Colors.Red;

    public SpeedOverlayViewModel? Source { get; set; }

    public void SetColors(Color foregroundColor, Color lineColor)
    {
        foreground = foregroundColor;
        line = lineColor;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;

        if (Source is null || width <= 0 || height <= 0)
        {
            return;
        }

        int binCount = Math.Max(2, (int)(width / PixelsPerBin));

        if (bins.Length != binCount)
        {
            bins = new double[binCount];
        }

        double scale = Source.FillGraph(bins);
        double middle = Math.Round(height / 2) + 0.5;

        var gridPen = new Pen(new SolidColorBrush(foreground, 0.25), 1, DashStyle.Dot);
        context.DrawLine(gridPen, new Point(0, 0.5), new Point(width, 0.5));
        context.DrawLine(gridPen, new Point(0, middle), new Point(width, middle));
        context.DrawLine(new Pen(new SolidColorBrush(foreground, 0.35), 1), new Point(0, height - 0.5), new Point(width, height - 0.5));

        var area = new StreamGeometry();
        var trace = new StreamGeometry();

        using (var areaContext = area.Open())
        using (var traceContext = trace.Open())
        {
            for (int bin = 0; bin < binCount; bin++)
            {
                var point = new Point(
                    (bin + 0.5) * width / binCount,
                    height - Math.Min(bins[bin], scale) / scale * height);

                if (bin == 0)
                {
                    areaContext.BeginFigure(new Point(point.X, height), isFilled: true);
                    traceContext.BeginFigure(point, isFilled: false);
                }
                else
                {
                    traceContext.LineTo(point);
                }

                areaContext.LineTo(point);
            }

            areaContext.LineTo(new Point((binCount - 0.5) * width / binCount, height));
            areaContext.EndFigure(isClosed: true);
            traceContext.EndFigure(isClosed: false);
        }

        context.DrawGeometry(new SolidColorBrush(line, 0.3), null, area);
        context.DrawGeometry(null, new Pen(new SolidColorBrush(line), 1.5, lineJoin: PenLineJoin.Round), trace);

        DrawLabel(context, SpeedOverlayViewModel.Format(scale), 2);
        DrawLabel(context, SpeedOverlayViewModel.Format(scale / 2), middle + 1.5);
    }

    private void DrawLabel(DrawingContext context, string text, double top)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            LabelFontSize,
            new SolidColorBrush(foreground, 0.65));

        context.DrawText(formatted, new Point(3, top));
    }
}
