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
}

public static class TypeExtensions
{
    public static float GetFloat(this object value)
    {
        if (value is float floatValue)
        {
            return floatValue;
        }
        else if (value is string stringfloatValue && float.TryParse(stringfloatValue, CultureInfo.InvariantCulture, out var parsedfloatValue))
        {
            return parsedfloatValue;
        }
        return 0;
    }

    public static int GetInt(this object value)
    {
        if (value is int intValue)
        {
            return intValue;
        }
        else if (value is string stringIntValue && int.TryParse(stringIntValue, CultureInfo.InvariantCulture, out var parsedIntValue))
        {
            return parsedIntValue;
        }
        return 0;
    }

    public static bool GetBool(this object value)
    {
        var boolenValue = false;
        if (value is bool b)
        {
            return b;
        }
        return boolenValue;
    }

    public static string GetString(this object value)
    {
        if (value is string s)
        {
            return s;
        }
        return value?.ToString() ?? "";
    }
}

public class TilePropertiesConverter : JsonConverter<TileProperties>
{
    public override TileProperties Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var tileProperties = new TileProperties();
        //tileProperties.Properties = new Dictionary<string, ValueTypeTuple>();

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
                //tileProperties.Properties[propName] = (value, type);
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
