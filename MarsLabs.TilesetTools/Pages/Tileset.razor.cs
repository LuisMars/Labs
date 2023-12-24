using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Text.Json;

namespace MarsLabs.TilesetTools.Pages;
public partial class Tileset
{
    [Inject]
    public IJSRuntime JsRuntime { get; set; }
    public string? ImageData { get; set; }
    public string? ImageSrc { get; set; }
    private int NumRows { get; set; }
    private int NumCols { get; set; }
    private int ImageHeight { get; set; }
    private int ImageWidth { get; set; }
    private Dictionary<(int X, int Y), TileProperties> Tiles { get; set; } = [];
    private TileProperties? SelectedTile { get; set; }
    private TilesetProperties TilesetProperties { get; set; } = new TilesetProperties();
    private string CustomPropertyName { get; set; } = "";
    private string CustomPropertyValue { get; set; } = "";
    private string Output { get; set; } = "";
    private bool LastPanel1Status { get; set; }
    private bool LastPanel2Status { get; set; }
    private bool LastPanel3Status { get; set; }
    private bool IsPanel1Open { get; set; }
    private bool IsPanel2Open { get; set; }
    private bool IsPanel3Open { get; set; }
    private string OldGlobalProperty { get; set; } = "";
    private string OldGlobalPropertyType { get; set; } = "";
    private string GlobalProperty { get; set; } = "";
    private string GlobalPropertyType { get; set; } = "string";
    private Tabs CurrentTab { get; set; } = Tabs.Tileset;
    public enum Tabs
    {
        Tileset,
        Properties,
        Tile,
        Import,
        Export
    }

    private async Task LoadImage(InputFileChangeEventArgs e)
    {
        var imageFile = e.File;
        if (imageFile != null)
        {
            TilesetProperties.ImageFile = e.File.Name;
            var format = "image/png"; // or determine from file
            var buffer = new byte[imageFile.Size];
            await imageFile.OpenReadStream().ReadAsync(buffer);
            ImageData = Convert.ToBase64String(buffer);
            ImageSrc = $"data:{format};base64,{ImageData}";
            // Calculate grid based on image dimensions
            await GetImageDimensions();
            ApplyGrid();
        }
    }
    private async Task LoadSaveFile(InputFileChangeEventArgs e)
    {
        using var fileStream = e.File.OpenReadStream();
        TilesetProperties = await JsonSerializer.DeserializeAsync<TilesetProperties>(fileStream);

    }

    private async Task GetImageDimensions()
    {
        var dimensions = await JsRuntime.InvokeAsync<int[]>("getImageDimensions", ImageSrc);
        ImageWidth = dimensions[0];
        ImageHeight = dimensions[1];
    }

    private void ApplyGrid()
    {
        // Assuming image dimensions are accessible via some variables like imageWidth and imageHeight
        NumRows = (int)Math.Ceiling((float)ImageHeight / (TilesetProperties.TileHeight + TilesetProperties.TileGapHeight));
        NumCols = (int)Math.Ceiling((float)ImageWidth / (TilesetProperties.TileWidth + TilesetProperties.TileGapHeight));
        GenerateJsonOutput();
    }

    private void EditTile(int row, int col)
    {
        if (!Tiles.TryGetValue((col, row), out var tile))
        {
            tile = new TileProperties { Col = col, Row = row };
            Tiles[(col, row)] = tile;
        }

        SelectedTile = tile;
        CustomPropertyName = "";
        CustomPropertyValue = "";
        AddGlobalProperties();
        StateHasChanged();
    }
    private void AddGlobalProperty()
    {
        if (string.IsNullOrEmpty(GlobalProperty))
        {
            return;
        }

        if (!string.IsNullOrEmpty(OldGlobalProperty) || TilesetProperties.GlobalProperties.Any(c => c.Name == GlobalProperty))
        {
            TilesetProperties.GlobalProperties.RemoveWhere(p => p.Name == OldGlobalProperty);
            TilesetProperties.GlobalProperties.RemoveWhere(p => p.Name == GlobalProperty);
            TilesetProperties.GlobalProperties.Add(new (GlobalProperty, GlobalPropertyType));
            RenameGlobalProperties(OldGlobalProperty, GlobalProperty);
            GlobalProperty = "";
            OldGlobalProperty = "";
            AddGlobalProperties();
            StateHasChanged(); 
            GenerateJsonOutput();

            return;
        }
        TilesetProperties.GlobalProperties.Add(new (GlobalProperty, GlobalPropertyType));
        GlobalProperty = "";
        OldGlobalProperty = "";
        AddGlobalProperties();
        GenerateJsonOutput();
        StateHasChanged();
    }

    private void AddGlobalProperties()
    {
        foreach (var item in Tiles)
        {
            foreach (var property in TilesetProperties.GlobalProperties)
            {
                SetProperty(item.Value, property.Name, property.Type);
            }
        }
    }

    private void RenameGlobalProperties(string oldName, string newName)
    {
        foreach (var item in Tiles)
        {
            if (item.Value.StringValues.TryGetValue(oldName, out var oldValue))
            {
                item.Value.StringValues[newName] = oldValue;
                item.Value.StringValues.Remove(oldName);
            }
            if (item.Value.BoolValues.TryGetValue(oldName, out var oldBoolValue))
            {
                item.Value.BoolValues[newName] = oldBoolValue;
                item.Value.BoolValues.Remove(oldName);
            }
            if (item.Value.IntValues.TryGetValue(oldName, out var oldIntValue))
            {
                item.Value.IntValues[newName] = oldIntValue;
                item.Value.IntValues.Remove(oldName);
            }
            if (item.Value.FloatValues.TryGetValue(oldName, out var oldFloatValue))
            {
                item.Value.FloatValues[newName] = oldFloatValue;
                item.Value.FloatValues.Remove(oldName);
            }

            if (item.Value.PropertyTypes.TryGetValue(oldName, out var oldType))
            {                      
                item.Value.PropertyTypes[newName] = oldType;
                item.Value.PropertyTypes.Remove(oldName);
            }
        }
    }

    private static void SetProperty(TileProperties? tile, string propertyName, string propertyType)
    {
        if (tile is null || string.IsNullOrEmpty(propertyName))
        {
            return;
        }
        tile.PropertyTypes ??= [];
        tile.PropertyTypes[propertyName] = propertyType;
        switch (propertyType)
        {
            case "string" when !tile.StringValues.ContainsKey(propertyName):
                tile.StringValues[propertyName] = default;
                break;
            case "bool" when !tile.BoolValues.ContainsKey(propertyName):
                tile.BoolValues[propertyName] = default;
                break;
            case "int" when !tile.IntValues.ContainsKey(propertyName):
                tile.IntValues[propertyName] = default;
                break;
            case "float" when !tile.FloatValues.ContainsKey(propertyName):
                tile.FloatValues[propertyName] = default;
                break;
        }
    }

    private void GenerateJsonOutput()
    {
        var properties = Tiles.Where(t => t.Value.HasContent).Select(t => t.Value);
        TilesetProperties.Tiles = properties;
        Output = JsonSerializer.Serialize(TilesetProperties, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new TilePropertiesConverter() },
            IncludeFields = true,
        });
    }
    // This method is triggered when the button is clicked
    private async Task SaveFile()
    {
        GenerateJsonOutput();
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", Output);

        byte[] file = System.Text.Encoding.UTF8.GetBytes(Output);
        await JsRuntime.InvokeVoidAsync("downloadFile", $"{TilesetProperties.ImageFile}.json", "text/json", file);
    }
}

