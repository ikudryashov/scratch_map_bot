namespace ScratchMapApp.TelegramBot.Models;

public class MapTiles
{
	public List<TileCoordinates> Tiles { get; init; } = null!;
	public int MinTileX { get; init; }
	public int MinTileY { get; init; }
	public int MaxTileX { get; init; }
	public int MaxTileY { get; init; }
	public int XTilesCount { get; init; }
	public int YTilesCount { get; init; }
}