using AircraftSimManager.Data.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AircraftSimManager.Utils.Enumerators;

namespace AircraftSimManager.Data.Services
{
	public class LiveryScannerService
	{
		public List<AircraftGroup> ScanLiveries(string rootPath, SimulatorType simType)
		{
			if (!Directory.Exists(rootPath))
				return new List<AircraftGroup>();

			return simType switch
			{
				SimulatorType.MSFS2020 or SimulatorType.MSFS2024 => ScanMsfsPackages(rootPath),
				SimulatorType.FSX or SimulatorType.FSX_SE or SimulatorType.Prepar3Dv4 or SimulatorType.Prepar3Dv5 => ScanFsxAirplanes(rootPath),
				_ => new List<AircraftGroup>()
			};
		}

		#region MSFS Scanner Strategy

		private List<AircraftGroup> ScanMsfsPackages(string communityPath)
		{
			var groups = new Dictionary<string, AircraftGroup>();

			var pmdgDirectories = Directory.GetDirectories(communityPath, "pmdg-aircraft-*", SearchOption.TopDirectoryOnly);

			foreach (var dir in pmdgDirectories)
			{
				string modelName = new DirectoryInfo(dir).Name.Replace("pmdg-aircraft-", "").ToUpper();

				if (!groups.ContainsKey(modelName))
				{
					groups[modelName] = new AircraftGroup { ModelName = $"PMDG {modelName}" };
				}

				string simObjectsDir = Path.Combine(dir, "SimObjects", "Airplanes");
				if (Directory.Exists(simObjectsDir))
				{
					foreach (var aircraftDir in Directory.GetDirectories(simObjectsDir))
					{
						var textureDirs = Directory.GetDirectories(aircraftDir, "texture.*", SearchOption.TopDirectoryOnly);
						foreach (var texDir in textureDirs)
						{
							string texFolderName = new DirectoryInfo(texDir).Name;

							string? thumb = Directory.GetFiles(texDir, "*.*")
													 .FirstOrDefault(f => (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
																		   f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) &&
																		  (Path.GetFileName(f).StartsWith("thumbnail", StringComparison.OrdinalIgnoreCase) ||
																		   Path.GetFileName(f).StartsWith("preview", StringComparison.OrdinalIgnoreCase)));

							groups[modelName].Liveries.Add(new LiveryItem
							{
								Title = ReadTitleFromAircraftCfg(aircraftDir, texFolderName) ?? texFolderName.Replace("texture.", ""),
								TextureFolder = texFolderName,
								AircraftPath = aircraftDir,
								ThumbnailPath = thumb
							});
						}
					}
				}
			}

			return groups.Values.Where(g => g.Liveries.Any()).ToList();
		}

		#endregion

		#region FSX / P3D Scanner Strategy

		private List<AircraftGroup> ScanFsxAirplanes(string airplanesPath)
		{
			var groups = new Dictionary<string, AircraftGroup>();

			var pmdgFolders = Directory.GetDirectories(airplanesPath, "*PMDG*", SearchOption.TopDirectoryOnly);

			foreach (var aircraftDir in pmdgFolders)
			{
				string modelName = new DirectoryInfo(aircraftDir).Name;
				var group = new AircraftGroup { ModelName = modelName };

				string cfgPath = Path.Combine(aircraftDir, "aircraft.cfg");
				if (File.Exists(cfgPath))
				{
					var liveries = ParseFsxAircraftCfg(cfgPath, aircraftDir);
					foreach (var livery in liveries)
					{
						group.Liveries.Add(livery);
					}
				}

				if (group.Liveries.Any())
				{
					groups[modelName] = group;
				}
			}

			return groups.Values.ToList();
		}

		private List<LiveryItem> ParseFsxAircraftCfg(string cfgPath, string aircraftDir)
		{
			var liveries = new List<LiveryItem>();
			var lines = File.ReadAllLines(cfgPath);

			string currentTitle = string.Empty;
			string currentTexture = string.Empty;
			bool isFltsimSection = false;

			foreach (var line in lines)
			{
				string trimmed = line.Trim();

				if (trimmed.StartsWith("[fltsim.", StringComparison.OrdinalIgnoreCase))
				{
					if (isFltsimSection && !string.IsNullOrEmpty(currentTitle))
					{
						var liveryItem = CreateFsxLiveryItem(aircraftDir, currentTitle, currentTexture);
						if (liveryItem != null) liveries.Add(liveryItem);
					}

					isFltsimSection = true;
					currentTitle = string.Empty;
					currentTexture = string.Empty;
				}
				else if (trimmed.StartsWith("[", StringComparison.OrdinalIgnoreCase))
				{
					// Sai de uma seção [fltsim.X] e entra em outra [main], [General], etc.
					if (isFltsimSection && !string.IsNullOrEmpty(currentTitle))
					{
						var liveryItem = CreateFsxLiveryItem(aircraftDir, currentTitle, currentTexture);
						if (liveryItem != null) liveries.Add(liveryItem);
					}
					isFltsimSection = false;
				}
				else if (isFltsimSection)
				{
					if (trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
					{
						currentTitle = trimmed.Split('=')[1].Trim().Trim('"');
					}
					else if (trimmed.StartsWith("texture", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
					{
						string texValue = trimmed.Split('=')[1].Trim().Trim('"');
						// Se estiver vazio (texture=), usa "texture", senão usa "texture.VALOR"
						currentTexture = string.IsNullOrWhiteSpace(texValue) ? "texture" : $"texture.{texValue}";
					}
				}
			}

			if (isFltsimSection && !string.IsNullOrEmpty(currentTitle))
			{
				var liveryItem = CreateFsxLiveryItem(aircraftDir, currentTitle, currentTexture);
				if (liveryItem != null) liveries.Add(liveryItem);
			}

			return liveries;
		}

		private LiveryItem? CreateFsxLiveryItem(string aircraftDir, string title, string textureFolder)
		{
			// Se o atributo texture não foi informado na seção, o FSX usa a pasta "texture" padrão
			if (string.IsNullOrEmpty(textureFolder))
			{
				textureFolder = "texture";
			}

			string texPath = Path.Combine(aircraftDir, textureFolder);

			// Ignora se for uma pasta que não existe ou se referir a utilitários de câmera
			if (!Directory.Exists(texPath) || textureFolder.Contains("camera", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			string? thumb = Directory.GetFiles(texPath, "*.*")
									 .FirstOrDefault(f => (f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
														   f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
														   f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) &&
														  Path.GetFileName(f).StartsWith("thumbnail", StringComparison.OrdinalIgnoreCase));

			return new LiveryItem
			{
				Title = title,
				TextureFolder = textureFolder,
				AircraftPath = aircraftDir,
				ThumbnailPath = thumb
			};
		}

		private string? ReadTitleFromAircraftCfg(string aircraftDir, string textureFolderName)
		{
			string cfgPath = Path.Combine(aircraftDir, "aircraft.cfg");
			if (!File.Exists(cfgPath)) return null;

			string textureSuffix = textureFolderName.Replace("texture.", "");
			var lines = File.ReadAllLines(cfgPath);

			string? lastTitle = null;
			foreach (var line in lines)
			{
				string trimmed = line.Trim();
				if (trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
				{
					lastTitle = trimmed.Split('=')[1].Trim().Trim('"');
				}
				if (trimmed.StartsWith("texture", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
				{
					string tex = trimmed.Split('=')[1].Trim().Trim('"');
					if (tex.Equals(textureSuffix, StringComparison.OrdinalIgnoreCase))
					{
						return lastTitle;
					}
				}
			}

			return lastTitle;
		}

		public bool UpdateLiveryTitle(string aircraftDir, string textureFolder, string newTitle)
		{
			string cfgPath = Path.Combine(aircraftDir, "aircraft.cfg");
			if (!File.Exists(cfgPath) || string.IsNullOrWhiteSpace(newTitle)) return false;

			try
			{
				string textureSuffix = textureFolder.StartsWith("texture.", StringComparison.OrdinalIgnoreCase)
					? textureFolder.Substring("texture.".Length)
					: (textureFolder.Equals("texture", StringComparison.OrdinalIgnoreCase) ? "" : textureFolder);

				var lines = File.ReadAllLines(cfgPath).ToList();
				bool inTargetSection = false;
				int titleLineIndex = -1;

				for (int i = 0; i < lines.Count; i++)
				{
					string trimmed = lines[i].Trim();
					if (trimmed.StartsWith("[fltsim.", StringComparison.OrdinalIgnoreCase))
					{
						inTargetSection = true;
						titleLineIndex = -1;
					}
					else if (trimmed.StartsWith("[") && inTargetSection)
					{
						inTargetSection = false;
					}
					else if (inTargetSection)
					{
						if (trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
						{
							titleLineIndex = i;
						}
						else if (trimmed.StartsWith("texture", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
						{
							string texVal = trimmed.Split('=')[1].Trim().Trim('"');
							if (texVal.Equals(textureSuffix, StringComparison.OrdinalIgnoreCase))
							{
								if (titleLineIndex >= 0)
								{
									lines[titleLineIndex] = $"title={newTitle}";
									File.WriteAllLines(cfgPath, lines, Encoding.UTF8);
									return true;
								}
							}
						}
					}
				}
			}
			catch { }
			return false;
		}

		public bool DeleteLivery(string aircraftDir, string textureFolder)
		{
			try
			{
				// 1. Apaga a pasta de textura
				string texPath = Path.Combine(aircraftDir, textureFolder);
				if (Directory.Exists(texPath))
				{
					Directory.Delete(texPath, true);
				}

				// 2. Remove a seção do aircraft.cfg e renumera [fltsim.0], [fltsim.1], etc.
				string cfgPath = Path.Combine(aircraftDir, "aircraft.cfg");
				if (File.Exists(cfgPath))
				{
					string textureSuffix = textureFolder.StartsWith("texture.", StringComparison.OrdinalIgnoreCase)
						? textureFolder.Substring("texture.".Length)
						: (textureFolder.Equals("texture", StringComparison.OrdinalIgnoreCase) ? "" : textureFolder);

					var lines = File.ReadAllLines(cfgPath).ToList();
					var sections = new List<(string Header, List<string> Lines)>();
					List<string> currentSection = new List<string>();
					string currentHeader = "";

					foreach (var line in lines)
					{
						string trimmed = line.Trim();
						if (trimmed.StartsWith("["))
						{
							if (!string.IsNullOrEmpty(currentHeader) || currentSection.Count > 0)
							{
								sections.Add((currentHeader, currentSection));
							}
							currentHeader = trimmed;
							currentSection = new List<string>();
						}
						else
						{
							currentSection.Add(line);
						}
					}
					if (!string.IsNullOrEmpty(currentHeader) || currentSection.Count > 0)
					{
						sections.Add((currentHeader, currentSection));
					}

					int fltsimIndex = 0;
					var newSections = new List<(string Header, List<string> Lines)>();

					foreach (var sec in sections)
					{
						if (sec.Header.StartsWith("[fltsim.", StringComparison.OrdinalIgnoreCase))
						{
							bool isTarget = false;
							foreach (var l in sec.Lines)
							{
								string t = l.Trim();
								if (t.StartsWith("texture", StringComparison.OrdinalIgnoreCase) && t.Contains('='))
								{
									string val = t.Split('=')[1].Trim().Trim('"');
									if (val.Equals(textureSuffix, StringComparison.OrdinalIgnoreCase))
									{
										isTarget = true;
										break;
									}
								}
							}

							if (isTarget)
							{
								continue;
							}

							newSections.Add(($"[fltsim.{fltsimIndex}]", sec.Lines));
							fltsimIndex++;
						}
						else
						{
							newSections.Add(sec);
						}
					}

					var outLines = new List<string>();
					foreach (var sec in newSections)
					{
						if (!string.IsNullOrEmpty(sec.Header))
						{
							outLines.Add(sec.Header);
						}
						outLines.AddRange(sec.Lines);
					}

					File.WriteAllLines(cfgPath, outLines, Encoding.UTF8);
				}

				return true;
			}
			catch
			{
				return false;
			}
		}

		#endregion
	}
}
