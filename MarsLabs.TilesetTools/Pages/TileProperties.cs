using System.Text.Json.Serialization;
using System.Text.Json;
using System.Globalization;

namespace MarsLabs.TilesetTools.Pages;

public class TilesetProperties
{
    public int ImageHeight { get; set; }
    public int ImageWidth { get; set; }
    public int TileWidth { get; set; } = 16;
    public int TileHeight { get; set; } = 16;
    public bool LinkTileSize { get; set; } = true;
    public int TileGapWidth { get; set; } = 0;
    public int TileGapHeight { get; set; } = 0;
    public bool LinkTileGapSize { get; set; } = true;
    public int GridColorAlpha { get; set; } = 128;
    public string GridColor { get; set; } = "#808080";
    public string ImageFile { get; set; } = "";

    public HashSet<PropertyDefinition> Properties { get; set; } = [];
    public IEnumerable<TileProperties> Tiles { get; set; } = [];
}

public class PropertyDefinition(string Name, string Type) : IEquatable<PropertyDefinition?>
{
    public string Name { get; set; } = Name;
    public string Type { get; set; } = Type;
    public override bool Equals(object? obj)
    {
        return Equals(obj as PropertyDefinition);
    }

    public bool Equals(PropertyDefinition? other)
    {
        return other is not null &&
               Name == other.Name &&
               Type == other.Type;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Type);
    }

    public static bool operator ==(PropertyDefinition? left, PropertyDefinition? right)
    {
        return EqualityComparer<PropertyDefinition>.Default.Equals(left, right);
    }

    public static bool operator !=(PropertyDefinition? left, PropertyDefinition? right)
    {
        return !(left == right);
    }
}

// Represents properties for each tile that you might want to edit
public class TileProperties
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string Name { get; set; }
    public Dictionary<string, string> StringValues { get; set; } = [];
    public Dictionary<string, bool> BoolValues { get; set; } = [];
    public Dictionary<string, int> IntValues { get; set; } = [];
    public Dictionary<string, float> FloatValues { get; set; } = [];
    public Dictionary<string, string> PropertyTypes { get; set; } = [];
    public string[] Tags { get; set; } = [];
    internal bool HasContent
    {
        get
        {
            return
                !string.IsNullOrWhiteSpace(Name) ||
                Tags.Length > 0 ||
                StringValues?.Count(p => p.Value is not null) > 0 ||
                BoolValues?.Count(p => p.Value != default) > 0 ||
                FloatValues?.Count(p => p.Value != default) > 0 ||
                IntValues?.Count(p => p.Value != default) > 0
                ;
        }
    }
}

public class TilePropertiesConverter : JsonConverter<TileProperties>
{
    public override TileProperties Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var tileProperties = new TileProperties();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return tileProperties;
            }

            // Assume property name is known and valid.
            var propName = reader.GetString();
            reader.Read();
            if (propName is null)
            {
                continue;
            }
            // Custom logic to handle different types, you might need to adjust based on actual type logic.
            switch (propName)
            {
                case "Row":
                    tileProperties.Row = reader.GetInt32();
                    break;
                case "Col":
                    tileProperties.Col = reader.GetInt32();
                    break;
                case "Name":
                    tileProperties.Name = reader.GetString() ?? "";
                    break;
                case "Tags":
                    var tags = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            tags.Add(reader.GetString() ?? "");
                        }
                    }
                    tileProperties.Tags = [.. tags];
                    break;
                default:
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.String:
                            tileProperties.StringValues[propName] = reader.GetString() ?? "";
                            break;
                        case JsonTokenType.Number:
                            
                            if (reader.TryGetInt32(out var intValue))
                            {
                                tileProperties.IntValues[propName] = intValue;
                            }
                            if (reader.TryGetSingle(out var floatValue))
                            {
                                tileProperties.FloatValues[propName] = floatValue;
                            }
                            break;
                        case JsonTokenType.True:
                            tileProperties.BoolValues[propName] = true;
                            break;
                        case JsonTokenType.False:
                            tileProperties.BoolValues[propName] = false;
                            break;
                    }
                    break;
            }
        }

        return tileProperties;
    }

    public override void Write(Utf8JsonWriter writer, TileProperties value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write standard properties
        writer.WriteNumber("Row", value.Row);
        writer.WriteNumber("Col", value.Col);
        if (value.Name is not null)
        {
            writer.WriteString("Name", value.Name);
        }

        // Flatten and write properties from the dictionary
        if (value.PropertyTypes != null)
        {
            foreach (var (name, type) in value.PropertyTypes)
            {
                try
                {
                    switch (type)
                    {
                        case "int":
                            writer.WriteNumber(name, value.IntValues[name]);
                            break;
                        case "float":
                            var floatValue = value.FloatValues[name];
                            // Ensure there is always a decimal point
                            if (floatValue % 1 == 0) // It's a whole number
                            {
                                // Append .0 to make sure it's represented as a float
                                writer.WriteNumber(name, Convert.ToDecimal($"{floatValue}.0", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                writer.WriteNumber(name, floatValue);
                            }
                            break;
                        case "string":
                            writer.WriteString(name, value.StringValues[name]);
                            break;
                        case "bool":
                            writer.WriteBoolean(name, value.BoolValues[name]);
                            break;
                        default:
                            // Possibly throw an exception or handle unknown types
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            }
        }
        if (value.Tags != null && value.Tags.Length != 0)
        {
            writer.WriteStartArray("Tags");
            foreach (var tag in value.Tags)
            {
                writer.WriteStringValue(tag);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}
