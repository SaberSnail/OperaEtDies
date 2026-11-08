using System.Windows;

namespace OperaEtDies.Windows.StartUp;

public partial class StartUpWindowView : Window
{
	public StartUpWindowView(StartUpWindowViewModel? viewModel)
	{
		ViewModel = viewModel;
		InitializeComponent();
	}

	public StartUpWindowViewModel? ViewModel { get; }
}
