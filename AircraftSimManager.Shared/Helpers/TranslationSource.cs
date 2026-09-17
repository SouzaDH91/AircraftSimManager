using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;

namespace AircraftSimManager.Shared.Helpers
{
	public class TranslationSource : INotifyPropertyChanged
	{
		private static readonly TranslationSource instance = new();
		public static TranslationSource Instance => instance;

		private static readonly Dictionary<string, ResourceManager> _resourceManagers = new(StringComparer.OrdinalIgnoreCase)
		{
			["en"] = new("AircraftSimManager.Shared.Resources.AppResources", typeof(TranslationSource).Assembly),
			["pt"] = new("AircraftSimManager.Shared.Resources.AppResources_pt", typeof(TranslationSource).Assembly),
		};

		private CultureInfo currentCulture = new("pt-BR");

		public CultureInfo CurrentCulture
		{
			get => currentCulture;
			set
			{
				if (value == null) return;
				currentCulture = value;

				// 1. Atualiza as threads da aplicação
				Thread.CurrentThread.CurrentCulture = value;
				Thread.CurrentThread.CurrentUICulture = value;
				CultureInfo.DefaultThreadCurrentCulture = value;
				CultureInfo.DefaultThreadCurrentUICulture = value;

				// 2. Dispara a notificação indicando que os indexadores [key] mudaram
				OnPropertyChanged(string.Empty);
				OnPropertyChanged(Binding.IndexerName); // "Item[]"
				OnPropertyChanged("Item");
			}
		}

		public string this[string key]
		{
			get
			{
				if (string.IsNullOrEmpty(key)) return string.Empty;

				string langCode = currentCulture.TwoLetterISOLanguageName;
				if (_resourceManagers.TryGetValue(langCode, out var rm))
				{
					var val = rm.GetString(key);
					if (!string.IsNullOrEmpty(val)) return val;
				}

				// Fallback para inglês
				if (_resourceManagers.TryGetValue("en", out var rmDefault))
				{
					var val = rmDefault.GetString(key);
					if (!string.IsNullOrEmpty(val)) return val;
				}

				return key;
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
