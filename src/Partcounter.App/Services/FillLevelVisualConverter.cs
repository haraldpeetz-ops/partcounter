using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Partcounter.Services;

public sealed class FillLevelVisualConverter : IValueConverter
{
    public const double WarningThreshold = 80.0;
    public const double CriticalThreshold = 95.0;

    private static readonly Brush NormalTileBackground = CreateBrush(255, 255, 255);
    private static readonly Brush WarningTileBackground = CreateBrush(255, 251, 224);
    private static readonly Brush CriticalTileBackground = CreateBrush(255, 239, 218);

    private static readonly Brush NormalBorder = CreateBrush(216, 222, 230);
    private static readonly Brush WarningBorder = CreateBrush(214, 165, 0);
    private static readonly Brush CriticalBorder = CreateBrush(224, 106, 0);

    private static readonly Brush NormalStatusBackground = CreateBrush(229, 244, 220);
    private static readonly Brush WarningStatusBackground = CreateBrush(255, 236, 153);
    private static readonly Brush CriticalStatusBackground = CreateBrush(255, 190, 112);

    private static readonly Brush NormalProgress = CreateBrush(87, 150, 62);
    private static readonly Brush WarningProgress = CreateBrush(207, 158, 0);
    private static readonly Brush CriticalProgress = CreateBrush(218, 88, 0);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var fillPercent = value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            _ => 0.0
        };

        var mode = parameter?.ToString() ?? string.Empty;
        var isCritical = fillPercent >= CriticalThreshold;
        var isWarning = fillPercent >= WarningThreshold;

        return mode switch
        {
            "TileBackground" => isCritical ? CriticalTileBackground : isWarning ? WarningTileBackground : NormalTileBackground,
            "BorderBrush" => isCritical ? CriticalBorder : isWarning ? WarningBorder : NormalBorder,
            "BorderThickness" => isCritical ? new Thickness(3) : isWarning ? new Thickness(2) : new Thickness(1),
            "StatusBackground" => isCritical ? CriticalStatusBackground : isWarning ? WarningStatusBackground : NormalStatusBackground,
            "StatusText" => isCritical
                ? "WECHSEL STEHT BEVOR · Füllgrad ≥ 95 %"
                : isWarning
                    ? "VORWARNUNG · Füllgrad ≥ 80 %"
                    : "NORMAL · Füllgrad < 80 %",
            "ProgressForeground" => isCritical ? CriticalProgress : isWarning ? WarningProgress : NormalProgress,
            _ => Binding.DoNothing
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
