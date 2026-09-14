using CommunityToolkit.Mvvm.ComponentModel;

namespace AircraftSimManager.Data.Models
{
	public partial class LiveryItem : ObservableObject
	{
		[ObservableProperty]
		private string title = string.Empty;

		public string TextureFolder { get; set; } = string.Empty;
		public string AircraftPath { get; set; } = string.Empty;
		public string SectionHeader { get; set; } = string.Empty;

		// Caminho local para o arquivo da imagem de preview (JPG/PNG)
		public string? ThumbnailPath { get; set; }
	}
}
