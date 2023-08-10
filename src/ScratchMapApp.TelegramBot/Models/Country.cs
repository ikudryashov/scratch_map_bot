using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScratchMapApp.TelegramBot.Models;

#pragma warning disable CS8618
public class Country
{
	public string Name { get; set; } = null!;
	public string Emoji { get; set; } = null!;
	public List<string> Aliases { get; set; } = null!;
	public GeoJsonGeometry Geometry { get; set; }
}

public class GeoJsonGeometry
{
	[JsonPropertyName("type")]
	public string Type { get; set; }
	[JsonPropertyName("coordinates")]
	public JsonElement Coordinates { get; set; }
}

#pragma warning restore CS8618