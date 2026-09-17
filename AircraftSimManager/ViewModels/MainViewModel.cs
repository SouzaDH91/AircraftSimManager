using AircraftSimManager.Commands;
using AircraftSimManager.Data.Services;
using AircraftSimManager.Shared.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Windows.Input;

namespace AircraftSimManager.ViewModels
{
	public partial class MainViewModel : ViewModelBase
	{
		private readonly DashboardViewModel _dashboardViewModel = new();
		private readonly LiveryManagerViewModel _liveryManagerViewModel = new();
		private readonly FuelCalculatorViewModel _fuelCalculatorViewModel = new();
		private readonly ConfigService _configService;

		[ObservableProperty]
		private object? currentView;

		public ICommand ChangeLanguageCommand { get; }

		public bool IsEnglish => TranslationSource.Instance.CurrentCulture?.TwoLetterISOLanguageName.Equals("en", System.StringComparison.OrdinalIgnoreCase) ?? false;
		public bool IsPortuguese => TranslationSource.Instance.CurrentCulture?.TwoLetterISOLanguageName.Equals("pt", System.StringComparison.OrdinalIgnoreCase) ?? false;

		public MainViewModel()
		{
			// Define a tela inicial padrão como o Dashboard
			CurrentView = _dashboardViewModel;
			_configService = new ConfigService();

			ChangeLanguageCommand = new RelayCommand<string?>(SetLanguage);
		}

		// Método ou Command para mudar o idioma
		public void SetLanguage(string? cultureCode)
		{
			if (string.IsNullOrWhiteSpace(cultureCode)) return;

			// 1. Altera a cultura no Singleton (dispara a atualização visual instantânea)
			TranslationSource.Instance.CurrentCulture = new CultureInfo(cultureCode);

			// 2. Salva a preferência
			_configService.Language = cultureCode;
			_configService.SaveSettings();

			// 3. Notifica a checagem dos itens do menu
			OnPropertyChanged(nameof(IsEnglish));
			OnPropertyChanged(nameof(IsPortuguese));
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
			CurrentView = _fuelCalculatorViewModel;
		}

		[RelayCommand]
		private void NavigateToPerformanceCalculator()
		{
			CurrentView = new IncomingViewModel("Performance Calculator");
		}
	}
}
