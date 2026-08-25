using System.Windows;
using Partcounter.ViewModels;

namespace Partcounter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
