using AircraftSimManager.Data.Models;
using AircraftSimManager.Data.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static AircraftSimManager.Utils.Enumerators;

namespace AircraftSimManager.ViewModels
{
	public partial class LiveryManagerViewModel : ViewModelBase
	{
		private readonly LiveryScannerService _scannerService = new();
		private readonly PtpExtractorService _ptpExtractorService = new();
		private readonly PtpInstallerService _ptpInstallerService = new();

		[ObservableProperty]
		private ObservableCollection<SimulatorOption> simulators = new();

		[ObservableProperty]
		private SimulatorOption? selectedSimulator;

		[ObservableProperty]
		private string airplanesPath = string.Empty;

		[ObservableProperty]
		private ObservableCollection<AircraftGroup> aircraftGroups = new();

		[ObservableProperty]
		private AircraftGroup? selectedAircraftGroup;

		[ObservableProperty]
		private LiveryItem? selectedLivery;

		[ObservableProperty]
		private string editTitleText = string.Empty;

		[ObservableProperty]
		private string statusMessage = "Pronto";

		public LiveryManagerViewModel()
		{
			_ = InitializeAsync();
		}

		public async Task InitializeAsync()
		{
			StatusMessage = "Detectando simuladores instalados...";

			var availableSimulators = await Task.Run(LoadAvailableSimulators);
			Simulators = new ObservableCollection<SimulatorOption>(availableSimulators);

			var defaultSim = Simulators.FirstOrDefault(s => s.IsInstalled) ?? Simulators.FirstOrDefault();

			if (defaultSim != null)
			{
				SelectedSimulator = defaultSim;
			}
			else
			{
				StatusMessage = "Nenhum simulador encontrado.";
			}
		}

		private static List<SimulatorOption> LoadAvailableSimulators()
		{
			var list = new List<SimulatorOption>();

			var options = new (SimulatorType Type, string Name)[]
			{
				(SimulatorType.Prepar3Dv5, "Prepar3D v5"),
				(SimulatorType.Prepar3Dv4, "Prepar3D v4"),
				(SimulatorType.MSFS2020, "Microsoft Flight Simulator 2020"),
				(SimulatorType.MSFS2024, "Microsoft Flight Simulator 2024"),
				(SimulatorType.FSX, "Flight Simulator X"),
				(SimulatorType.FSX_SE, "FSX: Steam Edition")
			};

			foreach (var opt in options)
			{
				string path = SimulatorPathResolver.ResolvePath(opt.Type);
				bool isInstalled = !string.IsNullOrEmpty(path) && Directory.Exists(path);

				list.Add(new SimulatorOption
				{
					Type = opt.Type,
					Name = opt.Name,
					Path = path,
					IsInstalled = isInstalled
				});
			}

			return list;
		}

		partial void OnSelectedSimulatorChanged(SimulatorOption? value)
		{
			if (value == null) return;

			string path = !string.IsNullOrEmpty(value.Path)
				? value.Path
				: SimulatorPathResolver.ResolvePath(value.Type);

			AirplanesPath = path;
			_ = LoadLiveriesAsync();
		}

		partial void OnSelectedLiveryChanged(LiveryItem? value)
		{
			EditTitleText = value?.Title ?? string.Empty;
		}

		[RelayCommand]
		private async Task LoadLiveriesAsync()
		{
			if (SelectedSimulator == null || string.IsNullOrWhiteSpace(AirplanesPath))
				return;

			StatusMessage = "Escaneando liveries...";

			string effectivePath = ResolveEffectivePath(AirplanesPath, SelectedSimulator.Type);

			var groups = await Task.Run(() =>
				_scannerService.ScanLiveries(effectivePath, SelectedSimulator.Type)
			);

			AircraftGroups = new ObservableCollection<AircraftGroup>(groups);
			StatusMessage = $"{AircraftGroups.Sum(g => g.Liveries.Count)} liveries encontradas.";
		}

		[RelayCommand]
		private async Task SaveTitleAsync()
		{
			if (SelectedLivery == null)
			{
				StatusMessage = "Nenhuma livery selecionada para alterar o título.";
				return;
			}

			if (string.IsNullOrWhiteSpace(EditTitleText))
			{
				MessageBox.Show("O título não pode ficar em branco.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			bool success = await Task.Run(() =>
				_scannerService.UpdateLiveryTitle(SelectedLivery.AircraftPath, SelectedLivery.TextureFolder, EditTitleText)
			);

			if (success)
			{
				SelectedLivery.Title = EditTitleText;
				StatusMessage = $"Título atualizado para '{EditTitleText}' com sucesso!";
				await LoadLiveriesAsync();
			}
			else
			{
				StatusMessage = "Falha ao salvar o novo título no aircraft.cfg.";
			}
		}

		[RelayCommand]
		private async Task DeleteSelectedLiveryAsync()
		{
			if (SelectedLivery == null)
			{
				StatusMessage = "Nenhuma livery selecionada para remoção.";
				return;
			}

			var result = MessageBox.Show(
				$"Tem certeza que deseja remover a livery '{SelectedLivery.Title}'?\n\nA pasta '{SelectedLivery.TextureFolder}' será excluída permanentemente.",
				"Confirmar Exclusão",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question
			);

			if (result != MessageBoxResult.Yes) return;

			StatusMessage = $"Removendo '{SelectedLivery.Title}'...";

			bool success = await Task.Run(() =>
				_scannerService.DeleteLivery(SelectedLivery.AircraftPath, SelectedLivery.TextureFolder)
			);

			if (success)
			{
				SelectedLivery = null;
				EditTitleText = string.Empty;
				StatusMessage = "Livery removida com sucesso!";
				await LoadLiveriesAsync();
			}
			else
			{
				StatusMessage = "Erro ao tentar remover a livery.";
			}
		}

		[RelayCommand]
		private async Task ConvertPtpToZipAsync()
		{
			var openDlg = new OpenFileDialog
			{
				Title = "Selecione o arquivo .PTP da PMDG",
				Filter = "Arquivo PMDG Livery (*.ptp)|*.ptp|Todos os arquivos (*.*)|*.*"
			};

			if (openDlg.ShowDialog() != true) return;

			string ptpPath = openDlg.FileName;
			string defaultZipName = Path.GetFileNameWithoutExtension(ptpPath) + ".zip";

			var saveDlg = new SaveFileDialog
			{
				Title = "Salvar arquivo .ZIP convertido",
				Filter = "Arquivo ZIP (*.zip)|*.zip",
				FileName = defaultZipName
			};

			if (saveDlg.ShowDialog() != true) return;

			string zipPath = saveDlg.FileName;
			StatusMessage = $"Convertendo {Path.GetFileName(ptpPath)} para .ZIP...";

			var progress = new Progress<string>(msg => StatusMessage = msg);
			var (success, message) = await _ptpExtractorService.ConvertPtpToZipAsync(ptpPath, zipPath, progress);

			if (success)
			{
				StatusMessage = $"Conversão concluída: {Path.GetFileName(zipPath)} criado!";
				MessageBox.Show($"Arquivo .ZIP criado com sucesso em:\n{zipPath}", "Conversão Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			else
			{
				StatusMessage = $"Erro na conversão: {message}";
				MessageBox.Show(message, "Falha na Conversão", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private static string ResolveEffectivePath(string basePath, SimulatorType simType)
		{
			if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
				return basePath;

			return simType switch
			{
				SimulatorType.FSX or SimulatorType.FSX_SE or SimulatorType.Prepar3Dv4 or SimulatorType.Prepar3Dv5 =>
					basePath.EndsWith(@"SimObjects\Airplanes", StringComparison.OrdinalIgnoreCase)
						? basePath
						: Path.Combine(basePath, "SimObjects", "Airplanes"),

				SimulatorType.MSFS2020 or SimulatorType.MSFS2024 =>
					basePath,

				_ => basePath
			};
		}

		public async Task InstallPackageFileAsync(string packagePath)
		{
			if (SelectedSimulator == null || string.IsNullOrWhiteSpace(AirplanesPath))
			{
				StatusMessage = "Erro: Nenhum simulador ou caminho selecionado.";
				return;
			}

			string fileName = Path.GetFileName(packagePath);
			StatusMessage = $"Instalando {fileName}...";

			string effectivePath = ResolveEffectivePath(AirplanesPath, SelectedSimulator.Type);
			var progress = new Progress<string>(msg => StatusMessage = msg);

			bool success = await _ptpInstallerService.InstallPackageAsync(packagePath, effectivePath, SelectedSimulator.Type, progress);

			if (success)
			{
				StatusMessage = $"Livery '{fileName}' instalada com sucesso!";
				await LoadLiveriesAsync();
			}
			else
			{
				StatusMessage = $"Falha ao instalar a livery '{fileName}'. Verifique o arquivo.";
			}
		}

		public async Task InstallZipFileAsync(string zipPath) => await InstallPackageFileAsync(zipPath);

		public async Task InstallPtpFileAsync(string ptpPath) => await InstallPackageFileAsync(ptpPath);
	}
}
