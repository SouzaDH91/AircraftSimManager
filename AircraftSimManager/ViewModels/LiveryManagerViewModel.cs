using AircraftSimManager.Data.Models;
using AircraftSimManager.Data.Services;
using AircraftSimManager.Shared.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
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
		private string statusMessage = TranslationSource.Instance["StatusReady"];

		public LiveryManagerViewModel()
		{
			_ = InitializeAsync();
		}

		public async Task InitializeAsync()
		{
			StatusMessage = TranslationSource.Instance["StatusDetectingSimulators"];

			var availableSimulators = await Task.Run(LoadAvailableSimulators);
			Simulators = new ObservableCollection<SimulatorOption>(availableSimulators);

			var defaultSim = Simulators.FirstOrDefault(s => s.IsInstalled) ?? Simulators.FirstOrDefault();

			if (defaultSim != null)
			{
				SelectedSimulator = defaultSim;
			}
			else
			{
				StatusMessage = TranslationSource.Instance["StatusNoSimulatorFound"];
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

			StatusMessage = TranslationSource.Instance["StatusScanningLiveries"];

			string effectivePath = ResolveEffectivePath(AirplanesPath, SelectedSimulator.Type);

			var groups = await Task.Run(() =>
				_scannerService.ScanLiveries(effectivePath, SelectedSimulator.Type)
			);

			AircraftGroups = new ObservableCollection<AircraftGroup>(groups);
			int count = AircraftGroups.Sum(g => g.Liveries.Count);
			StatusMessage = string.Format(TranslationSource.Instance["StatusLiveriesFound"], count);
		}

		[RelayCommand]
		private async Task SaveTitleAsync()
		{
			if (SelectedLivery == null)
			{
				StatusMessage = TranslationSource.Instance["StatusNoLiverySelectedForTitle"];
				return;
			}

			if (string.IsNullOrWhiteSpace(EditTitleText))
			{
				MessageBox.Show(
					TranslationSource.Instance["MsgBlankTitleError"],
					TranslationSource.Instance["DialogTitleWarning"],
					MessageBoxButton.OK,
					MessageBoxImage.Warning
				);
				return;
			}

			bool success = await Task.Run(() =>
				_scannerService.UpdateLiveryTitle(SelectedLivery.AircraftPath, SelectedLivery.TextureFolder, EditTitleText)
			);

			if (success)
			{
				SelectedLivery.Title = EditTitleText;
				StatusMessage = string.Format(TranslationSource.Instance["StatusTitleUpdated"], EditTitleText);
				await LoadLiveriesAsync();
			}
			else
			{
				StatusMessage = TranslationSource.Instance["StatusFailedToSaveTitle"];
			}
		}

		[RelayCommand]
		private async Task DeleteSelectedLiveryAsync()
		{
			if (SelectedLivery == null)
			{
				StatusMessage = TranslationSource.Instance["StatusNoLiverySelectedForDelete"];
				return;
			}

			string confirmMessage = string.Format(
				TranslationSource.Instance["MsgConfirmDeleteLivery"],
				SelectedLivery.Title,
				SelectedLivery.TextureFolder
			);

			var result = MessageBox.Show(
				confirmMessage,
				TranslationSource.Instance["DialogTitleConfirmDelete"],
				MessageBoxButton.YesNo,
				MessageBoxImage.Question
			);

			if (result != MessageBoxResult.Yes) return;

			StatusMessage = string.Format(TranslationSource.Instance["StatusRemovingLivery"], SelectedLivery.Title);

			bool success = await Task.Run(() =>
				_scannerService.DeleteLivery(SelectedLivery.AircraftPath, SelectedLivery.TextureFolder)
			);

			if (success)
			{
				SelectedLivery = null;
				EditTitleText = string.Empty;
				StatusMessage = TranslationSource.Instance["StatusLiveryRemoved"];
				await LoadLiveriesAsync();
			}
			else
			{
				StatusMessage = TranslationSource.Instance["StatusErrorRemovingLivery"];
			}
		}

		[RelayCommand]
		private async Task ConvertPtpToZipAsync()
		{
			var openDlg = new OpenFileDialog
			{
				Title = TranslationSource.Instance["DialogTitlePtpSelect"],
				Filter = TranslationSource.Instance["FilterPtpFiles"]
			};

			if (openDlg.ShowDialog() != true) return;

			string ptpPath = openDlg.FileName;
			string defaultZipName = Path.GetFileNameWithoutExtension(ptpPath) + ".zip";

			var saveDlg = new SaveFileDialog
			{
				Title = TranslationSource.Instance["DialogTitleZipSave"],
				Filter = TranslationSource.Instance["FilterZipFiles"],
				FileName = defaultZipName
			};

			if (saveDlg.ShowDialog() != true) return;

			string zipPath = saveDlg.FileName;
			StatusMessage = string.Format(TranslationSource.Instance["StatusConvertingToZip"], Path.GetFileName(ptpPath));

			var progress = new Progress<string>(msg => StatusMessage = msg);
			var (success, message) = await _ptpExtractorService.ConvertPtpToZipAsync(ptpPath, zipPath, progress);

			if (success)
			{
				StatusMessage = string.Format(TranslationSource.Instance["StatusConversionCompleted"], Path.GetFileName(zipPath));
				string successBoxMsg = string.Format(TranslationSource.Instance["MsgZipCreatedSuccess"], zipPath);

				MessageBox.Show(
					successBoxMsg,
					TranslationSource.Instance["DialogTitleConversionCompleted"],
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
			}
			else
			{
				StatusMessage = string.Format(TranslationSource.Instance["StatusConversionError"], message);

				MessageBox.Show(
					message,
					TranslationSource.Instance["DialogTitleConversionFailed"],
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
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
				StatusMessage = TranslationSource.Instance["StatusErrorNoSimOrPath"];
				return;
			}

			string fileName = Path.GetFileName(packagePath);
			StatusMessage = string.Format(TranslationSource.Instance["StatusInstallingPackage"], fileName);

			string effectivePath = ResolveEffectivePath(AirplanesPath, SelectedSimulator.Type);
			var progress = new Progress<string>(msg => StatusMessage = msg);

			bool success = await _ptpInstallerService.InstallPackageAsync(packagePath, effectivePath, SelectedSimulator.Type, progress);

			if (success)
			{
				StatusMessage = string.Format(TranslationSource.Instance["StatusLiveryInstalledSuccess"], fileName);
				await LoadLiveriesAsync();
			}
			else
			{
				StatusMessage = string.Format(TranslationSource.Instance["StatusLiveryInstallFailed"], fileName);
			}
		}

		public async Task InstallZipFileAsync(string zipPath) => await InstallPackageFileAsync(zipPath);

		public async Task InstallPtpFileAsync(string ptpPath) => await InstallPackageFileAsync(ptpPath);
	}
}
