using System.Windows;
using Partcounter.ViewModels;

namespace Partcounter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Partcounter konnte nicht initialisiert werden:\n\n{ex.Message}",
                "Partcounter R001",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        await _viewModel.DisposeAsync();
    }
}
