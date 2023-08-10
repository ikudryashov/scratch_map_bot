namespace ScratchMapApp.TelegramBot.Models;

public class MapParameters
{
	public double LongitudeMin { get; set; }
	public double LatitudeMin { get; set; }
	public double LongitudeMax { get; set; }
	public double LatitudeMax { get; set; }
	public int ZoomLevel { get; set; }
	public int TileSize { get; set; }
}