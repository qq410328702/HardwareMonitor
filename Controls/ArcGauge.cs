using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HardwareMonitor.Controls;

public class ArcGauge : Control
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(ArcGauge),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(ArcGauge),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ArcGauge),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(ArcGauge),
            new FrameworkPropertyMetadata(Colors.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ArcGauge),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public Color AccentColor { get => (Color)GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public ArcGauge()
    {
        Services.ThemeService.ThemeChanged += (_, _) => InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth, h = ActualHeight;
        double size = Math.Min(w, h);
        double cx = w / 2, cy = h / 2;
        double radius = size / 2 - 8;
        double thickness = 6;

        // Background arc (270 degrees)
        var bgPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), thickness)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        double startAngle = 135;
        double sweepAngle = 270;
        DrawArc(dc, cx, cy, radius, startAngle, sweepAngle, bgPen);

        // Value arc
        double pct = MaxValue > 0 ? Math.Clamp(Value / MaxValue, 0, 1) : 0;
        if (pct > 0.001)
        {
            var accent = AccentColor;
            var accentPen = new Pen(new SolidColorBrush(accent), thickness)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            DrawArc(dc, cx, cy, radius, startAngle, sweepAngle * pct, accentPen);

            // Glow effect
            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B)), thickness + 6)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            DrawArc(dc, cx, cy, radius, startAngle, sweepAngle * pct, glowPen);
        }

        // Value text - use theme color
        var primaryColor = TryFindResource("TextPrimary") is Color pc ? pc : Color.FromRgb(0xE6, 0xED, 0xF3);
        var secondaryColor = TryFindResource("TextSecondary") is Color sc ? sc : Color.FromRgb(0x8B, 0x94, 0x9E);

        var valueText = new FormattedText(
            $"{Value:F0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size * 0.22, new SolidColorBrush(primaryColor),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(valueText, new Point(cx - valueText.Width / 2, cy - valueText.Height / 2 - 4));

        // Unit text
        var unitText = new FormattedText(
            Unit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size * 0.1, new SolidColorBrush(secondaryColor),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(unitText, new Point(cx - unitText.Width / 2, cy + valueText.Height / 2 - 4));

        // Label text
        if (!string.IsNullOrEmpty(Label))
        {
            var labelText = new FormattedText(
                Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                size * 0.09, new SolidColorBrush(secondaryColor),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(labelText, new Point(cx - labelText.Width / 2, cy + radius * 0.55));
        }
    }

    private static void DrawArc(DrawingContext dc, double cx, double cy, double r,
        double startDeg, double sweepDeg, Pen pen)
    {
        if (sweepDeg < 0.1) return;

        double startRad = startDeg * Math.PI / 180;
        double endRad = (startDeg + sweepDeg) * Math.PI / 180;

        var start = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        var end = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }
}
