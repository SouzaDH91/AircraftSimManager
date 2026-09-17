using AircraftSimManager.Shared.Helpers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace AircraftSimManager.Data.Services
{
	public class PtpExtractorService
	{
		public string? FindPtpUnpackExecutable()
		{
			// 1. Check relative to current base directory (bin/Debug/...)
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string candidate1 = Path.Combine(baseDir, "Tools", "PtpUnpacker", "PtpUnpack.exe");
			if (File.Exists(candidate1)) return candidate1;

			// 2. Check parent directories (in case working directory is project root)
			string candidate2 = Path.Combine(baseDir, "..", "..", "..", "Tools", "PtpUnpacker", "PtpUnpack.exe");
			if (File.Exists(candidate2)) return Path.GetFullPath(candidate2);

			// 3. Check known development project path
			string candidate3 = @"D:\Projetos\Pessoal\AircraftSimManager\AircraftSimManager\Tools\PtpUnpacker\PtpUnpack.exe";
			if (File.Exists(candidate3)) return candidate3;

			return null;
		}

		/// <summary>
		/// Extrai o arquivo .ptp criptografado para o diretório de destino especificado.
		/// </summary>
		public async Task<(bool Success, string Message)> ExtractPtpAsync(string ptpFilePath, string targetDirectory, IProgress<string>? progress = null)
		{
			if (!File.Exists(ptpFilePath))
			{
				string errorMsg = $"{TranslationSource.Instance["ErrorPtpNotFound"]}: {ptpFilePath}";
				return (false, errorMsg);
			}

			string? unpackerExe = FindPtpUnpackExecutable();
			if (string.IsNullOrEmpty(unpackerExe) || !File.Exists(unpackerExe))
			{
				return (false, TranslationSource.Instance["ErrorUnpackerNotFound"]);
			}

			Directory.CreateDirectory(targetDirectory);

			string fileName = Path.GetFileName(ptpFilePath);
			string startingMsg = string.Format(TranslationSource.Instance["ProgressStartingUnpack"], fileName);
			progress?.Report(startingMsg);

			return await Task.Run(() =>
			{
				try
				{
					var startInfo = new ProcessStartInfo
					{
						FileName = unpackerExe,
						Arguments = $"\"{ptpFilePath}\" \"{targetDirectory}\"",
						WorkingDirectory = Path.GetDirectoryName(unpackerExe)!,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true
					};

					using var process = new Process { StartInfo = startInfo };

					process.OutputDataReceived += (s, e) =>
					{
						if (!string.IsNullOrEmpty(e.Data))
						{
							progress?.Report(e.Data);
						}
					};

					process.Start();
					process.BeginOutputReadLine();
					string stderr = process.StandardError.ReadToEnd();
					process.WaitForExit();

					if (process.ExitCode == 0)
					{
						return (true, TranslationSource.Instance["SuccessExtraction"]);
					}
					else
					{
						string failedMsg = string.Format(TranslationSource.Instance["ErrorExtractionFailed"], process.ExitCode, stderr);
						return (false, failedMsg);
					}
				}
				catch (Exception ex)
				{
					string exceptionMsg = string.Format(TranslationSource.Instance["ErrorExecutingExtractor"], ex.Message);
					return (false, exceptionMsg);
				}
			});
		}
		/*public async Task<(bool Success, string Message)> ExtractPtpAsync(string ptpFilePath, string targetDirectory, IProgress<string>? progress = null)
		{
			if (!File.Exists(ptpFilePath))
				return (false, $"Arquivo .ptp não encontrado: {ptpFilePath}");

			string? unpackerExe = FindPtpUnpackExecutable();
			if (string.IsNullOrEmpty(unpackerExe) || !File.Exists(unpackerExe))
				return (false, "Executável PtpUnpack.exe não foi encontrado na pasta Tools/PtpUnpacker.");

			Directory.CreateDirectory(targetDirectory);

			progress?.Report($"Iniciando descompactação de {Path.GetFileName(ptpFilePath)}...");

			return await Task.Run(() =>
			{
				try
				{
					var startInfo = new ProcessStartInfo
					{
						FileName = unpackerExe,
						Arguments = $"\"{ptpFilePath}\" \"{targetDirectory}\"",
						WorkingDirectory = Path.GetDirectoryName(unpackerExe)!,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true
					};

					using var process = new Process { StartInfo = startInfo };

					process.OutputDataReceived += (s, e) =>
					{
						if (!string.IsNullOrEmpty(e.Data))
						{
							progress?.Report(e.Data);
						}
					};

					process.Start();
					process.BeginOutputReadLine();
					string stderr = process.StandardError.ReadToEnd();
					process.WaitForExit();

					if (process.ExitCode == 0)
					{
						return (true, "Extração concluída com sucesso.");
					}
					else
					{
						return (false, $"Falha na extração (código {process.ExitCode}): {stderr}");
					}
				}
				catch (Exception ex)
				{
					return (false, $"Erro ao executar extrator PTP: {ex.Message}");
				}
			});
		}*/

		/// <summary>
		/// Converte um arquivo .ptp em um arquivo .zip comum, permitindo inspecionar ou instalar manualmente.
		/// </summary>
		public async Task<(bool Success, string Message)> ConvertPtpToZipAsync(string ptpFilePath, string outputZipPath, IProgress<string>? progress = null)
		{
			string tempFolder = Path.Combine(Path.GetTempPath(), "PtpConvert_" + Guid.NewGuid().ToString("N"));
			try
			{
				var extractResult = await ExtractPtpAsync(ptpFilePath, tempFolder, progress);
				if (!extractResult.Success)
				{
					return extractResult;
				}

				progress?.Report(TranslationSource.Instance["ProgressGeneratingZip"]);

				if (File.Exists(outputZipPath))
				{
					File.Delete(outputZipPath);
				}

				string? zipDir = Path.GetDirectoryName(outputZipPath);
				if (!string.IsNullOrEmpty(zipDir) && !Directory.Exists(zipDir))
				{
					Directory.CreateDirectory(zipDir);
				}

				await Task.Run(() => ZipFile.CreateFromDirectory(tempFolder, outputZipPath, CompressionLevel.Optimal, false));

				string progressMsg = string.Format(TranslationSource.Instance["ProgressZipCreated"], outputZipPath);
				progress?.Report(progressMsg);

				string successMsg = string.Format(TranslationSource.Instance["SuccessZipCreated"], outputZipPath);
				return (true, successMsg);
			}
			catch (Exception ex)
			{
				string errorMsg = string.Format(TranslationSource.Instance["ErrorZipCompression"], ex.Message);
				return (false, errorMsg);
			}
			finally
			{
				if (Directory.Exists(tempFolder))
				{
					try { Directory.Delete(tempFolder, true); } catch { }
				}
			}
		}

		/*public async Task<(bool Success, string Message)> ConvertPtpToZipAsync(string ptpFilePath, string outputZipPath, IProgress<string>? progress = null)
		{
			string tempFolder = Path.Combine(Path.GetTempPath(), "PtpConvert_" + Guid.NewGuid().ToString("N"));
			try
			{
				var extractResult = await ExtractPtpAsync(ptpFilePath, tempFolder, progress);
				if (!extractResult.Success)
				{
					return extractResult;
				}

				progress?.Report("Gerando arquivo .ZIP...");

				if (File.Exists(outputZipPath))
				{
					File.Delete(outputZipPath);
				}

				string? zipDir = Path.GetDirectoryName(outputZipPath);
				if (!string.IsNullOrEmpty(zipDir) && !Directory.Exists(zipDir))
				{
					Directory.CreateDirectory(zipDir);
				}

				await Task.Run(() => ZipFile.CreateFromDirectory(tempFolder, outputZipPath, CompressionLevel.Optimal, false));

				progress?.Report($"Arquivo .ZIP gerado com sucesso: {outputZipPath}");
				return (true, $"Arquivo .ZIP criado com sucesso em: {outputZipPath}");
			}
			catch (Exception ex)
			{
				return (false, $"Erro ao compactar para ZIP: {ex.Message}");
			}
			finally
			{
				if (Directory.Exists(tempFolder))
				{
					try { Directory.Delete(tempFolder, true); } catch { }
				}
			}
		}*/
	}
}
