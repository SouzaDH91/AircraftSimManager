using System.Text.Json.Serialization;

namespace AircraftSimManager.Data.Models
{
	public class MsfsLayout
	{
		[JsonPropertyName("content")]
		public List<MsfsLayoutFile> Content { get; set; } = new();
	}
}
