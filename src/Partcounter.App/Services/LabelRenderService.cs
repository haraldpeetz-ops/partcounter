using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Partcounter.Models;
using ZXing;
using ZXing.Common;

namespace Partcounter.Services;

public sealed class LabelRenderService
{
    public const double PrintPixelsPerMm = 96.0 / 25.4;

    public FixedDocument BuildDocument(PackagingUnitRecord record, LabelTemplateDefinition template)
    {
        var width = MmToPixels(template.WidthMm, PrintPixelsPerMm);
        var height = MmToPixels(template.HeightMm, PrintPixelsPerMm);

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(width, height);

        var page = new FixedPage
        {
            Width = width,
            Height = height,
            Background = Brushes.White
        };

        foreach (var definition in template.Elements.OrderBy(e => e.ZIndex))
        {
            var element = CreateVisual(definition, record, PrintPixelsPerMm);
            FixedPage.SetLeft(element, MmToPixels(definition.Xmm, PrintPixelsPerMm));
            FixedPage.SetTop(element, MmToPixels(definition.Ymm, PrintPixelsPerMm));
            Panel.SetZIndex(element, definition.ZIndex);
            page.Children.Add(element);
        }

        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(page);
        document.Pages.Add(pageContent);
        return document;
    }

    public FrameworkElement CreatePreviewVisual(
        LabelElementDefinition definition,
        PackagingUnitRecord record,
        double pixelsPerMm) => CreateVisual(definition, record, pixelsPerMm);

    private static FrameworkElement CreateVisual(
        LabelElementDefinition definition,
        PackagingUnitRecord record,
        double pixelsPerMm)
    {
        var width = Math.Max(1, MmToPixels(definition.WidthMm, pixelsPerMm));
        var height = Math.Max(1, MmToPixels(definition.HeightMm, pixelsPerMm));
        var content = LabelTemplateService.ResolveContent(definition.Content, record);

        return definition.Type switch
        {
            LabelElementType.Text or LabelElementType.DataText => CreateText(definition, content, width, height),
            LabelElementType.QrCode => CreateBarcode(content, BarcodeFormat.QR_CODE, width, height),
            LabelElementType.Code128 => CreateBarcode(content, BarcodeFormat.CODE_128, width, height),
            LabelElementType.Image => CreateImage(definition, width, height),
            LabelElementType.Rectangle => new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(Math.Max(0.5, definition.BorderThickness))
            },
            LabelElementType.Line => new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.Black
            },
            _ => new Border { Width = width, Height = height }
        };
    }

    private static FrameworkElement CreateText(
        LabelElementDefinition definition,
        string content,
        double width,
        double height)
    {
        FontFamily family;
        try
        {
            family = new FontFamily(string.IsNullOrWhiteSpace(definition.FontFamily) ? "Segoe UI" : definition.FontFamily);
        }
        catch
        {
            family = new FontFamily("Segoe UI");
        }

        var block = new TextBlock
        {
            Text = content,
            Width = width,
            Height = height,
            FontFamily = family,
            FontSize = Math.Max(4, definition.FontSizePt) * 96.0 / 72.0,
            FontWeight = definition.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = definition.Italic ? FontStyles.Italic : FontStyles.Normal,
            Foreground = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Top,
            TextAlignment = definition.Alignment switch
            {
                LabelTextAlignment.Center => TextAlignment.Center,
                LabelTextAlignment.Right => TextAlignment.Right,
                _ => TextAlignment.Left
            }
        };

        if (definition.Underline)
            block.TextDecorations = TextDecorations.Underline;

        return block;
    }

    private static FrameworkElement CreateImage(LabelElementDefinition definition, double width, double height)
    {
        if (string.IsNullOrWhiteSpace(definition.ImageDataBase64))
            return CreateImagePlaceholder(width, height, "Bild auswählen");

        try
        {
            var bytes = Convert.FromBase64String(definition.ImageDataBase64);
            using var stream = new MemoryStream(bytes, writable: false);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = definition.PreserveAspectRatio ? Stretch.Uniform : Stretch.Fill,
                SnapsToDevicePixels = true
            };
        }
        catch
        {
            return CreateImagePlaceholder(width, height, "Bild ungültig", Brushes.Red);
        }
    }

    private static FrameworkElement CreateImagePlaceholder(double width, double height, string text, Brush? brush = null)
    {
        brush ??= Brushes.Gray;
        return new Border
        {
            Width = width,
            Height = height,
            BorderBrush = brush,
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static FrameworkElement CreateBarcode(string content, BarcodeFormat format, double width, double height)
    {
        if (string.IsNullOrWhiteSpace(content))
            content = "–";

        try
        {
            var bitmap = CreateBarcodeBitmap(
                content,
                format,
                Math.Max(16, (int)Math.Round(width)),
                Math.Max(16, (int)Math.Round(height)));
            return new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            };
        }
        catch
        {
            return new Border
            {
                Width = width,
                Height = height,
                BorderBrush = Brushes.Red,
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "Barcode ungültig",
                    FontSize = 10,
                    Foreground = Brushes.Red,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }
    }

    private static BitmapSource CreateBarcodeBitmap(string text, BarcodeFormat format, int width, int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = format == BarcodeFormat.QR_CODE ? 1 : 4,
                PureBarcode = format == BarcodeFormat.CODE_128
            }
        };

        var pixelData = writer.Write(text);
        var bitmap = BitmapSource.Create(
            pixelData.Width,
            pixelData.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixelData.Pixels,
            pixelData.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    public static double MmToPixels(double millimeters, double pixelsPerMm) => millimeters * pixelsPerMm;
}
