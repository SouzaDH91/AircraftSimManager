using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftSimManager.Data.Services
{
	public class ConfigService
	{
		private readonly string _configFilePath;

		public string WeightUnit { get; set; } = "KG"; // Padrão

		public ConfigService()
		{
			string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AircraftSimManager");
			Directory.CreateDirectory(appDataFolder);
			_configFilePath = Path.Combine(appDataFolder, "settings.cfg");
			LoadSettings();
		}

		public void LoadSettings()
		{
			if (File.Exists(_configFilePath))
			{
				var lines = File.ReadAllLines(_configFilePath);
				foreach (var line in lines)
				{
					var parts = line.Split('=');
					if (parts.Length == 2 && parts[0].Trim() == "WeightUnit")
					{
						WeightUnit = parts[1].Trim().ToUpper() == "LBS" ? "LBS" : "KG";
					}
				}
			}
		}

		public void SaveSettings()
		{
			string[] lines = { $"WeightUnit={WeightUnit}" };
			File.WriteAllLines(_configFilePath, lines);
		}
	}
}
