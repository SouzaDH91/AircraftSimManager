using AircraftSimManager.Data.Models;
using AircraftSimManager.ViewModels;
using System.IO;
using System.Windows;

namespace AircraftSimManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
		public MainWindow()
		{
			InitializeComponent();
			DataContext = new MainViewModel();
		}
	}
}