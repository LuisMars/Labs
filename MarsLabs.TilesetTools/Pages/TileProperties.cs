using static MarsLabs.TilesetTools.Pages.Tileset;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Globalization;

namespace MarsLabs.TilesetTools.Pages;
public partial class Tileset
{
    // Represents properties for each tile that you might want to edit
    public class TileProperties
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public string Name { get; set; }
        public string Id { get; set; }
        public Dictionary<string, (object Value, string Type)?> Properties { get; set; }

        internal bool HasContent => !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(Id) || Properties?.Count(p => p.Value?.Value is not null) > 0;
    }
}


public class TilePropertiesConverter : JsonConverter<TileProperties>
{
    public override TileProperties Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var tileProperties = new TileProperties();
        tileProperties.Properties = new Dictionary<string, (object Value, string Type)?>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return tileProperties;
            }

            // Assume property name is known and valid.
            var propName = reader.GetString();
            reader.Read();

            // Custom logic to handle different types, you might need to adjust based on actual type logic.
            if (propName == "Row" || propName == "Col")
            {
                var intValue = reader.GetInt32();
                tileProperties.GetType().GetProperty(propName).SetValue(tileProperties, intValue);
            }
            else if (propName == "Name" || propName == "Id")
            {
                var stringValue = reader.GetString();
                tileProperties.GetType().GetProperty(propName).SetValue(tileProperties, stringValue);
            }
            else // Handle properties
            {
                // You need to implement logic to deserialize and cast based on the 'Type' value.
                // This is a simplistic approach; you'll need to handle actual types and errors.
                var value = reader.GetString(); // Or use appropriate method to get the value based on type
                var type = "String"; // Replace with actual logic to get the type.
                tileProperties.Properties[propName] = (value, type);
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
        if (value.Properties != null)
        {
            foreach (var prop in value.Properties)
            {
                if (prop.Value.HasValue)
                {
                    try
                    {
                        var (val, type) = prop.Value.Value;

                        // You should implement a more robust type handling based on 'type' value.
                        // Here's a simplistic example for illustration.
                        switch (type)
                        {
                            case "int":
                                if (val is int intValue)
                                {
                                    writer.WriteNumber(prop.Key, (int)val);
                                }
                                else if (val is string stringIntValue && int.TryParse(stringIntValue, out var parsedIntValue))
                                {
                                    writer.WriteNumber(prop.Key, parsedIntValue);
                                }
                                break;
                            case "float":
                                if (val is float floatValue)
                                {
                                    writer.WriteNumber(prop.Key, (float)val);
                                }
                                else if (val is string stringfloatValue && float.TryParse(stringfloatValue, CultureInfo.InvariantCulture, out var parsedfloatValue))
                                {
                                    writer.WriteNumber(prop.Key, parsedfloatValue);
                                }
                                break;
                            case "string":
                                writer.WriteString(prop.Key, (string)val);
                                break;
                            case "bool":
                                var boolenValue = false;
                                if (val is bool b)
                                {
                                    boolenValue = b;
                                }
                                writer.WriteBoolean(prop.Key, boolenValue);
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
        }

        writer.WriteEndObject();
    }
}
