using System.Text.Json.Serialization;

namespace ScratchMapApp.TelegramBot.Models;

#pragma warning disable CS8618
public class Layer
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("paint")]
    public Paint Paint { get; set; }
    
    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Source { get; set; }
    
    [JsonPropertyName("source-layer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SourceLayer { get; set; }
    
    [JsonPropertyName("filter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object> Filter { get; set; }
}

public class Metadata
{
    [JsonPropertyName("openmaptiles:version")]
    public string OpenmaptilesVersion { get; set; }
}

public class Openmaptiles
{
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class Paint
{
    [JsonPropertyName("background-color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string BackgroundColor { get; set; }
    
    [JsonPropertyName("fill-color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string FillColor { get; set; }
    [JsonPropertyName("fill-opacity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double FillOpacity { get; set; }
    
    [JsonPropertyName("line-color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string LineColor { get; set; }
    
    [JsonPropertyName("line-width")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object LineWidth { get; set; }
}

public class MapStyle
{
    [JsonPropertyName("version")]
    public int Version { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("metadata")]
    public Metadata Metadata { get; set; }
    
    [JsonPropertyName("sources")]
    public Dictionary<string, object> Sources { get; set; }
    
    [JsonPropertyName("layers")]
    public List<Layer> Layers { get; set; }
    
    [JsonPropertyName("id")]
    public string Id { get; set; }
}

public class VectorSource
{
    [JsonPropertyName("type")]
    public string Type { get; set; }
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class GeoJsonSource
{
    [JsonPropertyName("type")]
    public string Type { get; set; }
    [JsonPropertyName("data")]
    public GeoJsonData Data { get; set; }
}

public class GeoJsonData
{
    [JsonPropertyName("type")]
    public string Type { get; set; }
    [JsonPropertyName("properties")]
    public object Properties { get; set; }
    [JsonPropertyName("geometry")]
    public GeoJsonGeometry Geometry { get; set; }
}

#pragma warning restore CS8618





