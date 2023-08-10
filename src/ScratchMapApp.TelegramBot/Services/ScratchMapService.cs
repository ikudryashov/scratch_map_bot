using System.Text.Json;
using ScratchMapApp.TelegramBot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using ScratchMapApp.TelegramBot.Exceptions;
using ScratchMapApp.TelegramBot.Interfaces;
using SkiaSharp;

namespace ScratchMapApp.TelegramBot.Services;
public class ScratchMapService : IScratchMapService
{
	private readonly IConfiguration _configuration;
	private readonly IDockerContainerManager _containerManager;
	private readonly HttpClient _httpClient;
	private readonly ILogger<ScratchMapService> _logger;
	private readonly MapParameters _mapParameters;
	private readonly MapTiles _mapTiles;
	private int ImageWidth => _mapTiles.XTilesCount * _mapParameters.TileSize;
	private int ImageHeight => _mapTiles.YTilesCount * _mapParameters.TileSize;

	public ScratchMapService(
		IConfiguration configuration, 
		IDockerContainerManager containerManager, 
		HttpClient httpClient, ILogger<ScratchMapService> logger)
	{
		_configuration = configuration;
		_containerManager = containerManager;
		_httpClient = httpClient;
		_logger = logger;
		
		_mapParameters = new MapParameters();
		_configuration.GetSection("mapParameters").Bind(_mapParameters);
		
		_mapTiles = CalculateMapTilesForBoundingBox(
			_mapParameters.LongitudeMin, 
			_mapParameters.LatitudeMin, 
			_mapParameters.LongitudeMax, 
			_mapParameters.LatitudeMax, 
			_mapParameters.ZoomLevel);
	}

	public async Task<string> GetScratchMap(HashSet<Country> countries)
	{
			_logger.LogInformation("{1} Generating scratch map", DateTime.UtcNow);
			
			// edit map style JSON to add fill layers for user-selected countries
			AddCountriesToMapStyle(countries);
			
			// send SIGHUP signal to tileserver-gl Docker container to reload the map style JSON
			// https://github.com/maptiler/tileserver-gl/pull/155
			var containerName = _configuration.GetSection("docker")["containerName"];
		
			if (containerName is null)
			{
				throw new ConfigurationException();
			}
		
			await _containerManager.SendPosixSignal(containerName, "SIGHUP");
			
			// request tiles from tileserver gl and merge them into an image
			var scratchMapPath = await CreateImageFromTiles();
		
			// Clear style file
			RemoveCountriesFromMapStyle(countries);
            
			_logger.LogInformation("{1} Scratch map generated", DateTime.UtcNow);
			
			return scratchMapPath;
	}

	private void AddCountriesToMapStyle(HashSet<Country> countries)
	{
		var mapStyleConfig = _configuration.GetSection("mapStyle");
		var mapStyleFilePath = mapStyleConfig["mapStylePath"];
		var countryFillColor = mapStyleConfig["fillColor"];
		var countryFillOpacity = mapStyleConfig.GetValue<double?>("fillOpacity");
        
		if (mapStyleFilePath is null) throw new ConfigurationException();
		if (countryFillColor is null) throw new ConfigurationException();
		if (countryFillOpacity is null) throw new ConfigurationException();

		var mapStyleJson = File.ReadAllText(mapStyleFilePath);
        
		var mapStyle = JsonSerializer.Deserialize<MapStyle>(mapStyleJson)!;

		foreach (var country in countries)
		{
			var sourceName = $"{country.Name}-geojson";
			var source = new GeoJsonSource
			{
				Type = "geojson",
				Data = new GeoJsonData
				{
					Type = "Feature",
					Properties = new { },
					Geometry = country.Geometry
				}
			};

			mapStyle.Sources.Add(sourceName, source);
			
			var layerName = $"fill-{country.Name}";
			var layer = new Layer
			{
				Id = layerName,
				Type = "fill",
				Source = sourceName,
				Paint = new Paint { FillColor = countryFillColor, FillOpacity = (double)countryFillOpacity }
			};

			mapStyle.Layers.Add(layer);
		}

		var jsonString = JsonSerializer.Serialize(mapStyle);
		File.WriteAllText(mapStyleFilePath, jsonString);
	}
	
	private void RemoveCountriesFromMapStyle(HashSet<Country> countries)
	{
		var mapStyleConfig = _configuration.GetSection("mapStyle");
		var mapStyleFilePath = mapStyleConfig["mapStylePath"];
        
		if (mapStyleFilePath is null) throw new ConfigurationException();

		var mapStyleJson = File.ReadAllText(mapStyleFilePath);
		var mapStyle = JsonSerializer.Deserialize<MapStyle>(mapStyleJson)!;

		foreach (var country in countries)
		{
			mapStyle.Sources.Remove($"{country.Name}-geojson");
			var layer = mapStyle.Layers.SingleOrDefault(layer => layer.Id == $"fill-{country.Name}");
			mapStyle.Layers.Remove(layer!);
		}
		
		var jsonString = JsonSerializer.Serialize(mapStyle);
		File.WriteAllText(mapStyleFilePath, jsonString);
	}

	private async Task<string> CreateImageFromTiles()
	{
		// Create an empty SKBitmap with the desired width and height
		using var mergedBitmap = new SKBitmap(ImageWidth, ImageHeight);

		// Create an SKCanvas from the SKBitmap
		using var canvas = new SKCanvas(mergedBitmap);
		
		// Draw each tile onto the canvas
		foreach (var tile in _mapTiles.Tiles)
		{
			// Load the tile image
			var tileData = await GetRasterTileAsync(_mapParameters.ZoomLevel, tile.X, tile.Y);
			var tileBitmap = SKBitmap.Decode(tileData);

			// Calculate the position to draw the tile on the canvas
			var x = (tile.X - _mapTiles.MinTileX) * _mapParameters.TileSize;
			var y = (tile.Y - _mapTiles.MinTileY) * _mapParameters.TileSize;

			// Draw the tile onto the canvas at the specified position
			canvas.DrawBitmap(tileBitmap, x, y);
		}

		// Save the merged image to a file
		var appDirectory = AppContext.BaseDirectory;
		var path = Path.Combine(appDirectory, "scratchmap.png");
		
		await using var output = File.OpenWrite(path);
		mergedBitmap.Encode(SKEncodedImageFormat.Png, 100).SaveTo(output);
		
		return Path.GetFullPath(path);
	}
	
	private async Task<byte[]> GetRasterTileAsync(int zoom, int x, int y)
	{
		var policy = Policy.Handle<Exception>()
			.WaitAndRetryAsync(
				10,
				_ => TimeSpan.FromMilliseconds(200));
		
		var baseUrl = _configuration.GetSection("docker")["baseUrl"];
        
		var response = await policy.ExecuteAsync(async () =>
		{
			var tileUrl = $"{baseUrl}{zoom}/{x}/{y}@3x.png";
			return await _httpClient.GetByteArrayAsync(tileUrl);
		});
		
		return response;
	}

	private MapTiles CalculateMapTilesForBoundingBox(
		double lonMin, double latMin, double lonMax, double latMax, int zoomLevel)
	{
		var xMin = LongitudeToTileX(lonMin, zoomLevel);
		var xMax = LongitudeToTileX(lonMax, zoomLevel);
		var yMin = LatitudeToTileY(latMin, zoomLevel);
		var yMax = LatitudeToTileY(latMax, zoomLevel);

		var tiles = new List<TileCoordinates>();
		for (var x = xMin; x <= xMax; x++)
		{
			for (var y = yMin; y <= yMax; y++)
			{
				tiles.Add(new TileCoordinates { X = x, Y = y });
			}
		}

		var mapTiles = new MapTiles
		{
			Tiles = tiles,
			MinTileX = xMin,
			MinTileY = yMin,
			MaxTileX = xMax,
			MaxTileY = yMax,
			// Calculate the number of tiles in each direction
			XTilesCount = xMax - xMin + 1,
			YTilesCount = yMax - yMin + 1
		};

		return mapTiles;
	}
	
	// helper methods provided by OpenStreetMap Wiki to convert coordinates into tiles
	// https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#C#
	private static int LongitudeToTileX(double lon, int z) => 
		(int)Math.Floor((lon + 180.0) / 360.0 * (1 << z));
	
	private static int LatitudeToTileY(double lat, int z) => 
		(int)Math.Floor((1 - Math.Log(Math.Tan(ToRadians(lat)) + 1 / Math.Cos(ToRadians(lat))) / Math.PI) / 2 * (1 << z));

	private static double ToRadians(double degrees) => (Math.PI / 180) * degrees;

	// should the scratch map generation fail, the map style JSON has to be reverted back to
	// its original state before the app exits
	public void ResetMapStyle()
	{
		var mapStyleConfig = _configuration.GetSection("mapStyle");
		var mapStylePath = mapStyleConfig["mapStylePath"]!;
		var mapStyleBackupPath = mapStyleConfig["mapStyleBackupPath"]!;
		
		File.Delete(mapStylePath);
		File.Copy(mapStyleBackupPath, mapStylePath);
	}
}