using System.Text.Json.Serialization;

namespace AircraftSimManager.Data.Models
{
	public class MsfsLayoutFile
	{
		[JsonPropertyName("path")]
		public string Path { get; set; } = string.Empty;

		[JsonPropertyName("size")]
		public long Size { get; set; }

		[JsonPropertyName("date")]
		public long Date { get; set; } = 132537600000000000; // Timestamp padrão MSFS
	}
}
