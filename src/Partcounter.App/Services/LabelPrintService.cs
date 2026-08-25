using System.Printing;
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

public sealed class LabelPrintService
{
    public Task<bool> PrintAsync(PackagingUnitRecord record, string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return Task.FromResult(false);

        try
        {
            using var printServer = new LocalPrintServer();
            using var queue = printServer.GetPrintQueue(printerName.Trim());
            var writer = PrintQueue.CreateXpsDocumentWriter(queue);
            var document = BuildDocument(record);
            writer.Write(document.DocumentPaginator);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public FixedDocument BuildDocument(PackagingUnitRecord record)
    {
        const double width = 560;
        const double height = 400;

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(width, height);

        var page = new FixedPage
        {
            Width = width,
            Height = height,
            Background = Brushes.White
        };

        AddText(page, "PARTCOUNTER", 18, FontWeights.Bold, 18, 12);
        AddText(page, $"VE {record.VeNumber:0000} · Maschine {record.MachineNumber:00}", 16, FontWeights.Bold, 18, 43);
        AddText(page, $"Artikel: {record.ArticleNumber}", 15, FontWeights.Bold, 18, 78);
        AddText(page, record.ArticleDescription, 12, FontWeights.Normal, 18, 103, 330);
        AddText(page, $"Auftrag: {record.OrderNumber}", 12, FontWeights.Normal, 18, 132);
        AddText(page, $"Werkzeug: {record.ToolNumber} · Kavitäten: {record.Cavities}", 12, FontWeights.Normal, 18, 157);
        AddText(page, $"Menge: {record.ActualQuantity:N0} Stück", 22, FontWeights.Bold, 18, 192);
        AddText(page, $"VE-Soll: {record.TargetQuantity:N0} · zyklusbedingte Mehrmenge: {record.Overfill:N0}", 11, FontWeights.Normal, 18, 230);
        AddText(page, $"Fertig: {record.CompletedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss}", 11, FontWeights.Normal, 18, 254);
        AddText(page, $"VE-ID: {record.Id}", 9, FontWeights.Normal, 18, 279, 350);

        var qrPayload = BuildQrPayload(record);
        var qr = new Image
        {
            Source = CreateBarcode(qrPayload, BarcodeFormat.QR_CODE, 165, 165),
            Width = 165,
            Height = 165,
            Stretch = Stretch.Fill
        };
        FixedPage.SetLeft(qr, 380);
        FixedPage.SetTop(qr, 24);
        page.Children.Add(qr);

        var barcode = new Image
        {
            Source = CreateBarcode(record.Id, BarcodeFormat.CODE_128, 510, 72),
            Width = 510,
            Height = 72,
            Stretch = Stretch.Fill
        };
        FixedPage.SetLeft(barcode, 25);
        FixedPage.SetTop(barcode, 306);
        page.Children.Add(barcode);

        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(page);
        document.Pages.Add(pageContent);
        return document;
    }

    private static string BuildQrPayload(PackagingUnitRecord record) =>
        $"PC1|VE={record.Id}|M={record.MachineNumber:00}|A={record.ArticleNumber}|WZ={record.ToolNumber}|Q={record.ActualQuantity}|TS={record.CompletedAtUtc:O}";

    private static BitmapSource CreateBarcode(string text, BarcodeFormat format, int width, int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = format == BarcodeFormat.QR_CODE ? 1 : 4
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

    private static void AddText(FixedPage page, string text, double size, FontWeight weight,
        double left, double top, double width = 520)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            Width = width
        };
        FixedPage.SetLeft(block, left);
        FixedPage.SetTop(block, top);
        page.Children.Add(block);
    }
}
