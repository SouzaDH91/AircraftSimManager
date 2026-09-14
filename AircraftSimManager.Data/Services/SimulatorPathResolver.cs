using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AircraftSimManager.Utils.Enumerators;

namespace AircraftSimManager.Data.Services
{
	public static class SimulatorPathResolver
	{
		public static string ResolvePath(SimulatorType type)
		{
			return type switch
			{
				SimulatorType.MSFS2024 => GetMsfsCommunityFolder("Microsoft.Limitless_8wekyb3d8bbwe"),
				SimulatorType.MSFS2020 => GetMsfsCommunityFolder("Microsoft.FlightSimulator_8wekyb3d8bbwe"),
				SimulatorType.FSX_SE => GetFsxPath(isSteam: true),
				SimulatorType.FSX => GetFsxPath(isSteam: false),
				SimulatorType.Prepar3Dv4 => GetP3dPath("v4"),
				SimulatorType.Prepar3Dv5 => GetP3dPath("v5"),
				_ => string.Empty
			};
		}

		private static string GetMsfsCommunityFolder(string packageFolder)
		{
			string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

			string userCfgPath = Path.Combine(localAppData, "Packages", packageFolder, "LocalCache", "UserCfg.opt");

			if (!File.Exists(userCfgPath))
			{
				string steamFolderName = packageFolder.Contains("Limitless") ? "Microsoft Flight Simulator 2024" : "Microsoft Flight Simulator";
				userCfgPath = Path.Combine(appData, steamFolderName, "UserCfg.opt");
			}

			if (File.Exists(userCfgPath))
			{
				string? customPath = ReadInstalledPackagesPathFromCfg(userCfgPath);
				if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
				{
					string communityFolder = Path.Combine(customPath, "Community");
					if (Directory.Exists(communityFolder))
						return communityFolder;
				}
			}

			return Path.Combine(localAppData, "Packages", packageFolder, "LocalCache", "Packages", "Community");
		}

		private static string? ReadInstalledPackagesPathFromCfg(string cfgPath)
		{
			try
			{
				var lines = File.ReadAllLines(cfgPath);
				foreach (var line in lines)
				{
					if (line.TrimStart().StartsWith("InstalledPackagesPath", StringComparison.OrdinalIgnoreCase))
					{
						int firstQuote = line.IndexOf('"');
						int lastQuote = line.LastIndexOf('"');
						if (firstQuote != -1 && lastQuote > firstQuote)
						{
							return line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
						}
					}
				}
			}
			catch { }
			return null;
		}

		private static string GetFsxPath(bool isSteam)
		{
			string keyPath = isSteam
				? @"Software\Microsoft\Microsoft Games\Flight Simulator - Steam Edition\10.0"
				: @"SOFTWARE\WOW6432Node\Microsoft\Microsoft Games\Flight Simulator\10.0";

			string valueName = isSteam ? "AppPath" : "SetupPath";
			string? registryPath = GetRegistryValue(keyPath, valueName);

			if (!string.IsNullOrEmpty(registryPath) && Directory.Exists(registryPath))
			{
				string simObjectsPath = Path.Combine(registryPath, "SimObjects", "Airplanes");
				if (Directory.Exists(simObjectsPath))
					return simObjectsPath;
			}

			string defaultSteam = @"C:\Program Files (x86)\Steam\steamapps\common\FSX\SimObjects\Airplanes";
			string defaultBoxed = @"C:\Program Files (x86)\Microsoft Games\Microsoft Flight Simulator X\SimObjects\Airplanes";

			return isSteam ? defaultSteam : defaultBoxed;
		}

		private static string GetP3dPath(string version)
		{
			string keyPath = $@"SOFTWARE\Lockheed Martin\Prepar3D {version}";
			string keyPathWow = $@"SOFTWARE\WOW6432Node\Lockheed Martin\Prepar3D {version}";

			// Tenta buscar 'SetupPath' (ou 'AppPath' como fallback) nas chaves nativa e WOW6432Node
			string? registryPath = GetRegistryValue(keyPath, "SetupPath")
								?? GetRegistryValue(keyPath, "AppPath")
								?? GetRegistryValue(keyPathWow, "SetupPath")
								?? GetRegistryValue(keyPathWow, "AppPath");

			if (!string.IsNullOrEmpty(registryPath) && Directory.Exists(registryPath))
			{
				string simObjectsPath = Path.Combine(registryPath, "SimObjects", "Airplanes");
				if (Directory.Exists(simObjectsPath))
					return simObjectsPath;

				return registryPath; // Se não houver /SimObjects/Airplanes, retorna a raiz
			}

			return string.Empty;
		}

		private static string? GetRegistryValue(string keyPath, string valueName)
		{
			try
			{
				// Busca em HKLM (64-bit native), depois HKCU e depois de forma padrão
				using (var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
				using (var key = hklm64.OpenSubKey(keyPath))
				{
					if (key?.GetValue(valueName) is string val && !string.IsNullOrEmpty(val))
						return val;
				}

				using var keyFallback = Registry.LocalMachine.OpenSubKey(keyPath) ?? Registry.CurrentUser.OpenSubKey(keyPath);
				return keyFallback?.GetValue(valueName)?.ToString();
			}
			catch
			{
				return null;
			}
		}

		public static string GetAirplanesFolder(string baseOrSimPath, SimulatorType simType)
		{
			if (string.IsNullOrEmpty(baseOrSimPath))
				return string.Empty;

			return simType switch
			{
				SimulatorType.FSX or SimulatorType.FSX_SE or SimulatorType.Prepar3Dv4 or SimulatorType.Prepar3Dv5 =>
					// Se a pasta informada já for a SimObjects\Airplanes, mantém. Caso contrário, junta com SimObjects\Airplanes.
					baseOrSimPath.EndsWith(@"SimObjects\Airplanes", StringComparison.OrdinalIgnoreCase)
						? baseOrSimPath
						: Path.Combine(baseOrSimPath, "SimObjects", "Airplanes"),

				SimulatorType.MSFS2020 or SimulatorType.MSFS2024 =>
					// No MSFS, geralmente é a pasta Community
					baseOrSimPath,

				_ => baseOrSimPath
			};
		}

		/// <summary>
		/// Localiza a pasta raiz instalada do P3D/FSX no Registro do Windows (se o usuário não informou manualmente)
		/// </summary>
		public static string? DetectSimulatorInstallPath(SimulatorType simType)
		{
			string registryKey = simType switch
			{
				SimulatorType.Prepar3Dv5 => @"SOFTWARE\Lockheed Martin\Prepar3D v5",
				SimulatorType.Prepar3Dv4 => @"SOFTWARE\Lockheed Martin\Prepar3D v4",
				SimulatorType.FSX => @"SOFTWARE\Microsoft\Microsoft Games\Flight Simulator\10.0",
				SimulatorType.FSX_SE => @"SOFTWARE\DovetailGames\FSX",
				_ => string.Empty
			};

			if (string.IsNullOrEmpty(registryKey)) return null;

			using var key = Registry.LocalMachine.OpenSubKey(registryKey);
			if (key != null)
			{
				var path = key.GetValue("SetupPath") as string ?? key.GetValue("InstallDir") as string;
				return path;
			}

			return null;
		}
	}
}
