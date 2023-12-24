﻿using System.Text.Json.Serialization;
using System.Text.Json;

namespace MarsLabs.TilesetTools.Pages;

public class TilesetProperties
{

    public int TileWidth { get; set; } = 16;
    public int TileHeight { get; set; } = 16;
    public int TileGapWidth { get; set; } = 1;
    public int TileGapHeight { get; set; } = 1;
    public int GridColorAlpha { get; set; } = 128;
    public string GridColor { get; set; } = "#808080";
    public string ImageFile { get; internal set; }
    public HashSet<PropertyDefinition> GlobalProperties { get; set; } = [];
    public IEnumerable<TileProperties> Tiles { get; set; }
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
    public string Id { get; set; }
    public Dictionary<string, string> StringValues { get; set; } = [];
    public Dictionary<string, bool> BoolValues { get; set; } = [];
    public Dictionary<string, int> IntValues { get; set; } = [];
    public Dictionary<string, float> FloatValues { get; set; } = [];
    public Dictionary<string, string> PropertyTypes { get; set; } = [];

    internal bool HasContent
    {
        get
        {
            return
                !string.IsNullOrWhiteSpace(Name) ||
                !string.IsNullOrWhiteSpace(Id) ||
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
                case "Id":
                    tileProperties.Id = reader.GetString() ?? "";
                    break;
                default:
                    tileProperties.StringValues[propName] = reader.GetString() ?? "";
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
        if (value.Id is not null)
        {
            writer.WriteString("Id", value.Id);
        }
        // Flatten and write properties from the dictionary
        if (value.PropertyTypes != null)
        {
            foreach (var (name, type) in value.PropertyTypes)
            {
                try
                {
                    // You should implement a more robust type handling based on 'type' value.
                    // Here's a simplistic example for illustration.
                    switch (type)
                    {
                        case "int":
                            writer.WriteNumber(type, value.IntValues[name]);
                            break;
                        case "float":
                            writer.WriteNumber(type, value.FloatValues[name]);
                            break;
                        case "string":
                            writer.WriteString(type, value.StringValues[name]);
                            break;
                        case "bool":
                            writer.WriteBoolean(type, value.BoolValues[name]);
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

        writer.WriteEndObject();
    }
}
