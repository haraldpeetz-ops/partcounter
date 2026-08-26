using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Partcounter.Models;
using Partcounter.Services;
using Partcounter.ViewModels;

namespace Partcounter.Views;

public partial class LabelDesignerView : UserControl
{
    private const double PreviewPixelsPerMm = 4.0;
    private readonly LabelRenderService _renderer = new();

    private LabelDesignerViewModel? _viewModel;
    private LabelElementEditorRow? _dragRow;
    private FrameworkElement? _dragHost;
    private Point _dragStartPoint;
    private double _dragStartXmm;
    private double _dragStartYmm;
    private double _dragCurrentXmm;
    private double _dragCurrentYmm;
    private bool _dragMoved;

    public LabelDesignerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RefreshCanvas();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.DesignerChanged -= OnDesignerChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as LabelDesignerViewModel;
        if (_viewModel is not null)
        {
            _viewModel.DesignerChanged += OnDesignerChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RefreshCanvas();
    }

    private void OnDesignerChanged(object? sender, EventArgs e)
    {
        if (_dragRow is not null)
            return;
        Dispatcher.BeginInvoke(new Action(RefreshCanvas));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LabelDesignerViewModel.SelectedElement) && _dragRow is null)
            Dispatcher.BeginInvoke(new Action(RefreshCanvas));
    }

    private void RefreshCanvas()
    {
        if (_viewModel is null || DesignerCanvas is null)
            return;

        DesignerCanvas.Width = Math.Max(80, _viewModel.WidthMm * PreviewPixelsPerMm);
        DesignerCanvas.Height = Math.Max(80, _viewModel.HeightMm * PreviewPixelsPerMm);
        DesignerCanvas.Children.Clear();

        var sample = _viewModel.SampleRecord;
        foreach (var row in _viewModel.Elements.OrderBy(e => e.ZIndex))
        {
            var visual = _renderer.CreatePreviewVisual(row.Model, sample, PreviewPixelsPerMm);
            visual.IsHitTestVisible = false;

            var selected = ReferenceEquals(row, _viewModel.SelectedElement);
            var host = new Border
            {
                Width = Math.Max(2, row.WidthMm * PreviewPixelsPerMm),
                Height = Math.Max(2, row.HeightMm * PreviewPixelsPerMm),
                BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)) : Brushes.Transparent,
                BorderThickness = selected ? new Thickness(1.5) : new Thickness(0),
                Background = Brushes.Transparent,
                Child = visual,
                Tag = row,
                Cursor = Cursors.SizeAll,
                ToolTip = $"{row.DisplayName}\nX {row.Xmm:0.0} mm · Y {row.Ymm:0.0} mm · {row.WidthMm:0.0} × {row.HeightMm:0.0} mm"
            };

            host.MouseLeftButtonDown += OnElementMouseLeftButtonDown;
            host.MouseMove += OnElementMouseMove;
            host.MouseLeftButtonUp += OnElementMouseLeftButtonUp;

            Canvas.SetLeft(host, row.Xmm * PreviewPixelsPerMm);
            Canvas.SetTop(host, row.Ymm * PreviewPixelsPerMm);
            Panel.SetZIndex(host, row.ZIndex + (selected ? 1000 : 0));
            DesignerCanvas.Children.Add(host);
        }
    }

    private void OnElementMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement host || host.Tag is not LabelElementEditorRow row)
            return;

        _dragRow = row;
        _dragHost = host;
        _dragStartPoint = e.GetPosition(DesignerCanvas);
        _dragStartXmm = row.Xmm;
        _dragStartYmm = row.Ymm;
        _dragCurrentXmm = row.Xmm;
        _dragCurrentYmm = row.Ymm;
        _dragMoved = false;
        host.CaptureMouse();
        e.Handled = true;
    }

    private void OnElementMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel is null || _dragRow is null || _dragHost is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var point = e.GetPosition(DesignerCanvas);
        var dxMm = (point.X - _dragStartPoint.X) / PreviewPixelsPerMm;
        var dyMm = (point.Y - _dragStartPoint.Y) / PreviewPixelsPerMm;

        _dragCurrentXmm = Math.Round(Math.Clamp(
            _dragStartXmm + dxMm,
            0,
            Math.Max(0, _viewModel.WidthMm - _dragRow.WidthMm)), 1);
        _dragCurrentYmm = Math.Round(Math.Clamp(
            _dragStartYmm + dyMm,
            0,
            Math.Max(0, _viewModel.HeightMm - _dragRow.HeightMm)), 1);

        Canvas.SetLeft(_dragHost, _dragCurrentXmm * PreviewPixelsPerMm);
        Canvas.SetTop(_dragHost, _dragCurrentYmm * PreviewPixelsPerMm);
        _dragMoved = _dragMoved || Math.Abs(dxMm) > 0.3 || Math.Abs(dyMm) > 0.3;
        e.Handled = true;
    }

    private void OnElementMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || _dragRow is null)
            return;

        _dragHost?.ReleaseMouseCapture();
        var row = _dragRow;
        var moved = _dragMoved;
        var x = _dragCurrentXmm;
        var y = _dragCurrentYmm;

        _dragRow = null;
        _dragHost = null;
        _dragMoved = false;

        if (moved)
            _viewModel.MoveElement(row, x, y);
        _viewModel.SelectElement(row);
        RefreshCanvas();
        e.Handled = true;
    }
}
