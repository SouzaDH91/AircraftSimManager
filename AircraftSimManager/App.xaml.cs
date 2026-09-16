using AircraftSimManager.Data.Services;
using AircraftSimManager.Helpers;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;

namespace AircraftSimManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			var configService = new ConfigService();
			if (!string.IsNullOrEmpty(configService.Language))
			{
				TranslationSource.Instance.CurrentCulture = new System.Globalization.CultureInfo(configService.Language);
			}
		}
	}

}
