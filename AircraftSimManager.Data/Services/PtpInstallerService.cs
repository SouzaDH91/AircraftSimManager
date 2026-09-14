using AircraftSimManager.Data.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static AircraftSimManager.Utils.Enumerators;

namespace AircraftSimManager.Data.Services
{
	public class PtpInstallerService
	{
		private readonly PtpExtractorService _extractorService = new();

		public async Task<bool> InstallPackageAsync(string packageFilePath, string rootPath, SimulatorType simType, IProgress<string>? progress = null)
		{
			if (!File.Exists(packageFilePath)) return false;

			string ext = Path.GetExtension(packageFilePath);
			if (ext.Equals(".ptp", StringComparison.OrdinalIgnoreCase))
			{
				return await InstallPtpAsync(packageFilePath, rootPath, simType, progress);
			}
			else if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
			{
				return await InstallZipAsync(packageFilePath, rootPath, simType, progress);
			}

			return false;
		}

		public async Task<bool> InstallPtpAsync(string ptpFilePath, string rootPath, SimulatorType simType, IProgress<string>? progress = null)
		{
			if (!File.Exists(ptpFilePath) || !Directory.Exists(rootPath))
				return false;

			return simType switch
			{
				SimulatorType.MSFS2020 or SimulatorType.MSFS2024 => await InstallPtpForMsfsAsync(ptpFilePath, rootPath, progress),
				SimulatorType.FSX or SimulatorType.FSX_SE or SimulatorType.Prepar3Dv4 or SimulatorType.Prepar3Dv5 => await InstallPtpForFsxAsync(ptpFilePath, rootPath, simType, progress),
				_ => false
			};
		}

		public async Task<bool> InstallZipAsync(string zipFilePath, string rootPath, SimulatorType simType, IProgress<string>? progress = null)
		{
			if (!File.Exists(zipFilePath) || !Directory.Exists(rootPath))
				return false;

			return simType switch
			{
				SimulatorType.MSFS2020 or SimulatorType.MSFS2024 => await InstallZipForMsfsAsync(zipFilePath, rootPath, progress),
				SimulatorType.FSX or SimulatorType.FSX_SE or SimulatorType.Prepar3Dv4 or SimulatorType.Prepar3Dv5 => await InstallZipForFsxAsync(zipFilePath, rootPath, simType, progress),
				_ => false
			};
		}

		#region Instalação no MSFS (2020 / 2024)

		private async Task<bool> InstallPtpForMsfsAsync(string ptpFilePath, string communityPath, IProgress<string>? progress = null)
		{
			string tempExtractPath = Path.Combine(Path.GetTempPath(), "PmdgPtp_" + Guid.NewGuid().ToString("N"));
			try
			{
				var extractResult = await _extractorService.ExtractPtpAsync(ptpFilePath, tempExtractPath, progress);
				if (!extractResult.Success)
				{
					progress?.Report($"Erro na extração: {extractResult.Message}");
					return false;
				}

				return await Task.Run(() => ProcessMsfsExtractedPackage(tempExtractPath, communityPath, progress));
			}
			catch (Exception ex)
			{
				progress?.Report($"Erro na instalação MSFS: {ex.Message}");
				return false;
			}
			finally
			{
				if (Directory.Exists(tempExtractPath))
				{
					try { Directory.Delete(tempExtractPath, true); } catch { }
				}
			}
		}

		private async Task<bool> InstallZipForMsfsAsync(string zipFilePath, string communityPath, IProgress<string>? progress = null)
		{
			string tempExtractPath = Path.Combine(Path.GetTempPath(), "PmdgZip_" + Guid.NewGuid().ToString("N"));
			try
			{
				progress?.Report($"Descompactando ZIP {Path.GetFileName(zipFilePath)}...");
				await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath));

				return await Task.Run(() => ProcessMsfsExtractedPackage(tempExtractPath, communityPath, progress));
			}
			catch (Exception ex)
			{
				progress?.Report($"Erro na instalação MSFS via ZIP: {ex.Message}");
				return false;
			}
			finally
			{
				if (Directory.Exists(tempExtractPath))
				{
					try { Directory.Delete(tempExtractPath, true); } catch { }
				}
			}
		}

		private bool ProcessMsfsExtractedPackage(string tempExtractPath, string communityPath, IProgress<string>? progress = null)
		{
			string? layoutFile = Directory.GetFiles(tempExtractPath, "layout.json", SearchOption.AllDirectories).FirstOrDefault();

			if (layoutFile != null)
			{
				string packageSourceDir = Path.GetDirectoryName(layoutFile)!;
				string packageFolderName = new DirectoryInfo(packageSourceDir).Name;

				string targetDir = Path.Combine(communityPath, packageFolderName);

				progress?.Report($"Copiando arquivos para {targetDir}...");
				CopyDirectory(packageSourceDir, targetDir);

				progress?.Report("Atualizando layout.json...");
				RebuildMsfsLayoutJson(targetDir);
				return true;
			}

			return false;
		}

		public static void RebuildMsfsLayoutJson(string rootPackageFolder)
		{
			string current = rootPackageFolder;
			string layoutPath = string.Empty;

			while (current != null && Directory.Exists(current))
			{
				string check = Path.Combine(current, "layout.json");
				if (File.Exists(check))
				{
					layoutPath = check;
					break;
				}
				current = Directory.GetParent(current)?.FullName;
			}

			if (string.IsNullOrEmpty(layoutPath)) return;

			string packageRoot = Path.GetDirectoryName(layoutPath)!;
			var layout = new MsfsLayout();

			foreach (string file in Directory.GetFiles(packageRoot, "*.*", SearchOption.AllDirectories))
			{
				string relPath = Path.GetRelativePath(packageRoot, file).Replace('\\', '/');
				if (relPath.Equals("layout.json", StringComparison.OrdinalIgnoreCase) ||
					relPath.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
					continue;

				FileInfo fi = new FileInfo(file);
				layout.Content.Add(new MsfsLayoutFile
				{
					Path = relPath,
					Size = fi.Length,
					Date = fi.LastWriteTimeUtc.ToFileTimeUtc()
				});
			}

			var options = new JsonSerializerOptions { WriteIndented = true };
			string json = JsonSerializer.Serialize(layout, options);
			File.WriteAllText(layoutPath, json);
		}

		#endregion

		#region Instalação no FSX / Prepar3D (v4 / v5)

		private async Task<bool> InstallPtpForFsxAsync(string ptpFilePath, string airplanesPath, SimulatorType simType, IProgress<string>? progress = null)
		{
			string tempExtractPath = Path.Combine(Path.GetTempPath(), "PmdgPtpFsx_" + Guid.NewGuid().ToString("N"));
			try
			{
				var extractResult = await _extractorService.ExtractPtpAsync(ptpFilePath, tempExtractPath, progress);
				if (!extractResult.Success)
				{
					progress?.Report($"Erro na extração: {extractResult.Message}");
					return false;
				}

				return await Task.Run(() => ProcessFsxExtractedPackage(tempExtractPath, airplanesPath, progress));
			}
			catch (Exception ex)
			{
				progress?.Report($"Erro durante a instalação: {ex.Message}");
				return false;
			}
			finally
			{
				if (Directory.Exists(tempExtractPath))
				{
					try { Directory.Delete(tempExtractPath, true); } catch { }
				}
			}
		}

		private async Task<bool> InstallZipForFsxAsync(string zipFilePath, string airplanesPath, SimulatorType simType, IProgress<string>? progress = null)
		{
			string tempExtractPath = Path.Combine(Path.GetTempPath(), "PmdgZipFsx_" + Guid.NewGuid().ToString("N"));
			try
			{
				progress?.Report($"Descompactando ZIP {Path.GetFileName(zipFilePath)}...");
				await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath));

				return await Task.Run(() => ProcessFsxExtractedPackage(tempExtractPath, airplanesPath, progress));
			}
			catch (Exception ex)
			{
				progress?.Report($"Erro durante a instalação via ZIP: {ex.Message}");
				return false;
			}
			finally
			{
				if (Directory.Exists(tempExtractPath))
				{
					try { Directory.Delete(tempExtractPath, true); } catch { }
				}
			}
		}

		private bool ProcessFsxExtractedPackage(string tempExtractPath, string airplanesPath, IProgress<string>? progress = null)
		{
			var textureDirs = Directory.GetDirectories(tempExtractPath, "texture.*", SearchOption.AllDirectories);
			if (!textureDirs.Any())
			{
				progress?.Report("Nenhuma pasta de textura encontrada no pacote.");
				return false;
			}

			var pmdgAircraftFolders = GetPmdgAircraftFolders(airplanesPath);
			if (!pmdgAircraftFolders.Any())
			{
				progress?.Report("Nenhuma pasta de aeronave PMDG encontrada no diretório informado.");
				return false;
			}

			// 1. Tenta ler Settings.dat para saber a variante exata (ex: Variant=PMDG 737-800NGXu BW)
			string? variant = ReadVariantFromSettings(tempExtractPath);

			// 2. Localiza o arquivo de configuração (.cfg) extraído
			string? cfgSnippetPath = Directory.GetFiles(tempExtractPath, "Config.cfg", SearchOption.AllDirectories).FirstOrDefault()
								  ?? Directory.GetFiles(tempExtractPath, "*.cfg", SearchOption.AllDirectories).FirstOrDefault();

			// 3. Localiza a pasta da aeronave correspondente
			string? targetAircraftDir = FindTargetAircraftDirectory(variant, cfgSnippetPath, pmdgAircraftFolders);
			if (string.IsNullOrEmpty(targetAircraftDir))
			{
				targetAircraftDir = pmdgAircraftFolders.First();
			}

			progress?.Report($"Instalando na aeronave: {Path.GetFileName(targetAircraftDir)}");

			// 4. Copia as pastas de textura
			foreach (var texDir in textureDirs)
			{
				string folderName = new DirectoryInfo(texDir).Name;
				string destTexDir = Path.Combine(targetAircraftDir, folderName);

				progress?.Report($"Copiando texturas para {folderName}...");
				CopyDirectory(texDir, destTexDir);
			}

			// 5. Adiciona entrada no aircraft.cfg
			if (cfgSnippetPath != null && File.Exists(cfgSnippetPath))
			{
				string aircraftCfgPath = Path.Combine(targetAircraftDir, "aircraft.cfg");
				progress?.Report("Registrando livery no aircraft.cfg...");
				AppendFltsimToAircraftCfg(aircraftCfgPath, cfgSnippetPath);
			}

			// 6. Copia Aircraft.ini (se houver) para a pasta PMDG Aircraft (ex: E:\Simulador\Prepar3D v5\PMDG\PMDG 737 NGXu\Aircraft\PR-GTM.ini)
			TryInstallAircraftIni(tempExtractPath, airplanesPath, cfgSnippetPath, targetAircraftDir);

			progress?.Report("Instalação concluída com sucesso!");
			return true;
		}

		private string? ReadVariantFromSettings(string extractRoot)
		{
			try
			{
				string? settingsFile = Directory.GetFiles(extractRoot, "Settings.dat", SearchOption.AllDirectories).FirstOrDefault();
				if (settingsFile != null && File.Exists(settingsFile))
				{
					foreach (var line in File.ReadAllLines(settingsFile))
					{
						var trimmed = line.Trim();
						if (trimmed.StartsWith("Variant=", StringComparison.OrdinalIgnoreCase))
						{
							return trimmed.Substring("Variant=".Length).Trim();
						}
					}
				}
			}
			catch { }
			return null;
		}

		private string? FindTargetAircraftDirectory(string? variant, string? cfgSnippetPath, List<string> candidateAircraftDirs)
		{
			if (!string.IsNullOrEmpty(variant))
			{
				var exactMatch = candidateAircraftDirs.FirstOrDefault(d => new DirectoryInfo(d).Name.Equals(variant, StringComparison.OrdinalIgnoreCase));
				if (exactMatch != null) return exactMatch;

				var partialMatch = candidateAircraftDirs.FirstOrDefault(d => new DirectoryInfo(d).Name.IndexOf(variant, StringComparison.OrdinalIgnoreCase) >= 0
																		  || variant.IndexOf(new DirectoryInfo(d).Name, StringComparison.OrdinalIgnoreCase) >= 0);
				if (partialMatch != null) return partialMatch;
			}

			if (cfgSnippetPath != null && File.Exists(cfgSnippetPath))
			{
				try
				{
					string snippet = File.ReadAllText(cfgSnippetPath);
					var simMatch = Regex.Match(snippet, @"^\s*sim\s*=\s*(.+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
					if (simMatch.Success)
					{
						string simValue = simMatch.Groups[1].Value.Trim();
						foreach (var dir in candidateAircraftDirs)
						{
							string acCfgPath = Path.Combine(dir, "aircraft.cfg");
							if (!File.Exists(acCfgPath)) continue;

							string acCfg = File.ReadAllText(acCfgPath);
							if (Regex.IsMatch(acCfg, @"^\s*sim\s*=\s*" + Regex.Escape(simValue) + @"\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
							{
								return dir;
							}
						}
					}
				}
				catch { }
			}

			return candidateAircraftDirs.FirstOrDefault();
		}

		private List<string> GetPmdgAircraftFolders(string airplanesPath)
		{
			var resultsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				foreach (var dir in Directory.GetDirectories(airplanesPath, "*", SearchOption.TopDirectoryOnly))
				{
					if (File.Exists(Path.Combine(dir, "aircraft.cfg")))
					{
						if (dir.IndexOf("PMDG", StringComparison.OrdinalIgnoreCase) >= 0 ||
							File.ReadAllText(Path.Combine(dir, "aircraft.cfg")).IndexOf("PMDG", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							resultsSet.Add(dir);
						}
					}
				}

				if (!resultsSet.Any())
				{
					foreach (var dir in Directory.GetDirectories(airplanesPath, "*", SearchOption.TopDirectoryOnly))
					{
						if (File.Exists(Path.Combine(dir, "aircraft.cfg")))
							resultsSet.Add(dir);
					}
				}
			}
			catch { }

			return resultsSet.ToList();
		}

		private void AppendFltsimToAircraftCfg(string aircraftCfgPath, string snippetPath)
		{
			if (!File.Exists(aircraftCfgPath) || !File.Exists(snippetPath)) return;

			try
			{
				string snippetContent = File.ReadAllText(snippetPath);
				var acCfgLines = File.ReadAllLines(aircraftCfgPath).ToList();

				var titleMatch = Regex.Match(snippetContent, @"^\s*title\s*=\s*(.+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
				if (titleMatch.Success)
				{
					string titleVal = titleMatch.Groups[1].Value.Trim().Trim('"');
					bool alreadyInstalled = acCfgLines.Any(l =>
						l.Trim().StartsWith("title", StringComparison.OrdinalIgnoreCase) &&
						l.IndexOf(titleVal, StringComparison.OrdinalIgnoreCase) >= 0);

					if (alreadyInstalled)
					{
						return;
					}
				}

				int maxIndex = -1;
				int lastFltsimLineIndex = -1;

				for (int i = 0; i < acCfgLines.Count; i++)
				{
					var match = Regex.Match(acCfgLines[i].Trim(), @"^\[fltsim\.(\d+)\]", RegexOptions.IgnoreCase);
					if (match.Success)
					{
						if (int.TryParse(match.Groups[1].Value, out int idx))
						{
							if (idx > maxIndex) maxIndex = idx;
						}
						lastFltsimLineIndex = i;
					}
				}

				int nextIndex = maxIndex + 1;

				string formattedSnippet = Regex.Replace(snippetContent, @"^\[fltsim\.[^\]]+\]", $"[fltsim.{nextIndex}]", RegexOptions.IgnoreCase | RegexOptions.Multiline);

				int insertIndex = -1;
				if (lastFltsimLineIndex >= 0)
				{
					for (int i = lastFltsimLineIndex + 1; i < acCfgLines.Count; i++)
					{
						if (acCfgLines[i].Trim().StartsWith("[") && !acCfgLines[i].Trim().StartsWith("[fltsim.", StringComparison.OrdinalIgnoreCase))
						{
							insertIndex = i;
							break;
						}
					}
				}

				if (insertIndex >= 0)
				{
					acCfgLines.Insert(insertIndex, Environment.NewLine + formattedSnippet.Trim() + Environment.NewLine);
					File.WriteAllLines(aircraftCfgPath, acCfgLines, Encoding.UTF8);
				}
				else
				{
					File.AppendAllText(aircraftCfgPath, Environment.NewLine + Environment.NewLine + formattedSnippet.Trim() + Environment.NewLine, Encoding.UTF8);
				}
			}
			catch { }
		}

		private void TryInstallAircraftIni(string tempExtractPath, string airplanesPath, string? cfgSnippetPath, string targetAircraftDir)
		{
			try
			{
				string? iniFile = Directory.GetFiles(tempExtractPath, "Aircraft.ini", SearchOption.AllDirectories).FirstOrDefault();
				if (iniFile == null || !File.Exists(iniFile)) return;

				string? atcId = null;
				if (cfgSnippetPath != null && File.Exists(cfgSnippetPath))
				{
					string snippet = File.ReadAllText(cfgSnippetPath);
					var idMatch = Regex.Match(snippet, @"^\s*atc_id\s*=\s*(.+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
					if (idMatch.Success)
					{
						atcId = idMatch.Groups[1].Value.Trim().Trim('"');
					}
				}

				if (string.IsNullOrEmpty(atcId)) return;

				DirectoryInfo? airplanesDirInfo = new DirectoryInfo(airplanesPath);
				string? simRoot = airplanesDirInfo.Parent?.Parent?.FullName;
				if (string.IsNullOrEmpty(simRoot) || !Directory.Exists(simRoot)) return;

				string pmdgBase = Path.Combine(simRoot, "PMDG");
				if (!Directory.Exists(pmdgBase)) return;

				string aircraftFolderName = new DirectoryInfo(targetAircraftDir).Name;
				string? targetFamilyDir = null;

				foreach (var familyDir in Directory.GetDirectories(pmdgBase, "*", SearchOption.TopDirectoryOnly))
				{
					string fName = new DirectoryInfo(familyDir).Name;
					if (aircraftFolderName.IndexOf(fName, StringComparison.OrdinalIgnoreCase) >= 0 ||
						fName.IndexOf("737 NGXu", StringComparison.OrdinalIgnoreCase) >= 0 && aircraftFolderName.IndexOf("NGXu", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						string acDir = Path.Combine(familyDir, "Aircraft");
						if (Directory.Exists(acDir))
						{
							targetFamilyDir = acDir;
							break;
						}
					}
				}

				if (targetFamilyDir != null)
				{
					string destIniPath = Path.Combine(targetFamilyDir, $"{atcId}.ini");
					File.Copy(iniFile, destIniPath, true);
				}
			}
			catch { }
		}

		#endregion

		#region Auxiliares

		private void CopyDirectory(string sourceDir, string destinationDir)
		{
			Directory.CreateDirectory(destinationDir);

			foreach (string file in Directory.GetFiles(sourceDir))
			{
				string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
				File.Copy(file, destFile, true);
			}

			foreach (string subDir in Directory.GetDirectories(sourceDir))
			{
				string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
				CopyDirectory(subDir, destSubDir);
			}
		}

		#endregion
	}
}