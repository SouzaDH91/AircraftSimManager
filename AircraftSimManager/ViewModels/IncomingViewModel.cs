using CommunityToolkit.Mvvm.ComponentModel;

namespace AircraftSimManager.ViewModels
{
	public partial class IncomingViewModel : ViewModelBase
	{
		[ObservableProperty]
		private string featureName = string.Empty;

		public IncomingViewModel(string featureName)
		{
			FeatureName = featureName;
		}
	}
}
