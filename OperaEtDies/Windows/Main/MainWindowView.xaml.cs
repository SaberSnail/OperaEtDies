using System.Windows;

namespace OperaEtDies.Windows.Main;

public partial class MainWindowView : Window
{
	public MainWindowView(MainWindowViewModel? viewModel)
	{
		ViewModel = viewModel;
		InitializeComponent();
	}

	public MainWindowViewModel? ViewModel { get; }
}