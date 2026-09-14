using AircraftSimManager.Commands;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AircraftSimManager.ViewModels
{
	public partial class MainViewModel : ViewModelBase
	{
		private readonly DashboardViewModel _dashboardViewModel = new();
		private readonly LiveryManagerViewModel _liveryManagerViewModel = new();

		[ObservableProperty]
		private object? currentView;

		public MainViewModel()
		{
			// Define a tela inicial padrão como o Dashboard
			CurrentView = _dashboardViewModel;
		}

		[RelayCommand]
		private void NavigateToDashboard()
		{
			CurrentView = _dashboardViewModel;
		}

		[RelayCommand]
		private void NavigateToLiveryManager()
		{
			CurrentView = _liveryManagerViewModel;
		}

		[RelayCommand]
		private void NavigateToFuelCalculator()
		{
			CurrentView = new IncomingViewModel("Fuel Calculator");
		}

		[RelayCommand]
		private void NavigateToPerformanceCalculator()
		{
			CurrentView = new IncomingViewModel("Performance Calculator");
		}
	}
}
