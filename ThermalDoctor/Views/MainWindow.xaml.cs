using System.ComponentModel;
using System.Windows;
using ThermalDoctor.ViewModels;

namespace ThermalDoctor.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.Initialize();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _viewModel?.Dispose();
    }
}
