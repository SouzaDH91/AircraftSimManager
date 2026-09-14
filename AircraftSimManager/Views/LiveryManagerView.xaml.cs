using AircraftSimManager.Data.Models;
using AircraftSimManager.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AircraftSimManager.Views
{
	/// <summary>
	/// Interaction logic for LiveryManagerView.xaml
	/// </summary>
	public partial class LiveryManagerView : UserControl
	{
		private LiveryManagerViewModel? ViewModel => DataContext as LiveryManagerViewModel;

		public LiveryManagerView()
		{
			InitializeComponent();
			AllowDrop = true;
			DragOver += LiveryManagerView_DragOver;
		}

		private void LiveryManagerView_DragOver(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

				if (files != null && files.Any(f => Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
												   Path.GetExtension(f).Equals(".ptp", StringComparison.OrdinalIgnoreCase)))
				{
					e.Effects = DragDropEffects.Copy;
					e.Handled = true;
					return;
				}
			}

			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private async void Window_Drop(object sender, DragEventArgs e)
		{
			if (ViewModel == null) return;

			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

				if (files == null || files.Length == 0) return;

				var validPackages = files.Where(f => Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
													Path.GetExtension(f).Equals(".ptp", StringComparison.OrdinalIgnoreCase)).ToList();

				if (validPackages.Any())
				{
					foreach (var packagePath in validPackages)
					{
						await ViewModel.InstallPackageFileAsync(packagePath);
					}
					return;
				}

				MessageBox.Show("Por favor, solte arquivos com extensão .PTP ou .ZIP para realizar a instalação.", "Formato Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		}

		private void TvAircrafts_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (ViewModel == null) return;

			if (e.NewValue is AircraftGroup group)
			{
				ViewModel.SelectedAircraftGroup = group;
				ViewModel.SelectedLivery = null;
			}
			else if (e.NewValue is LiveryItem livery)
			{
				ViewModel.SelectedLivery = livery;
			}
		}
	}
}
