using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Text.Json;

namespace MarsLabs.TilesetTools.Pages;
public partial class Tileset
{
    [Inject]
    public IJSRuntime JsRuntime { get; set; }
    private string? ImageSrc { get; set; }
    private string? ImageData { get; set; }
    private int TileWidth { get; set; } = 16; 
    private int TileHeight { get; set; } = 16;
    private int TileGapWidth { get; set; } = 1;
    private int TileGapHeight { get; set; } = 1;
   
    private int NumRows { get; set; }
    private int NumCols { get; set; }
    private int ImageHeight { get; set; }
    private int ImageWidth { get; set; }
    private int GridColorAlpha { get; set; } = 128;
    private string GridColor { get; set; } = "#808080";
    private Dictionary<(int X, int Y), TileProperties> Tiles { get; set; } = [];
    private TileProperties? SelectedTile { get; set; }
    private string CustomPropertyName { get; set; } = "";
    private object CustomPropertyValue { get; set; } = "";
    private string Output { get; set; } = "";
    private bool LastPanel1Status { get; set; }
    private bool LastPanel2Status { get; set; }
    private bool LastPanel3Status { get; set; }
    private bool IsPanel1Open { get; set; }
    private bool IsPanel2Open { get; set; }
    private bool IsPanel3Open { get; set; }
    private HashSet<(string Name, string Type)> GlobalProperties { get; set; } = [];
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
        Output
    }

    private async Task LoadFile(InputFileChangeEventArgs e)
    {
        var imageFile = e.File;
        if (imageFile != null)
        {
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
    private async Task GetImageDimensions()
    {
        var dimensions = await JsRuntime.InvokeAsync<int[]>("getImageDimensions", ImageSrc);
        ImageWidth = dimensions[0];
        ImageHeight = dimensions[1];
    }

    private void ApplyGrid()
    {
        // Assuming image dimensions are accessible via some variables like imageWidth and imageHeight
        NumRows = (int)Math.Ceiling((float)ImageHeight / (TileHeight + TileGapHeight));
        NumCols = (int)Math.Ceiling((float)ImageWidth / (TileWidth + TileGapHeight));

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

        if (!string.IsNullOrEmpty(OldGlobalProperty) || GlobalProperties.Any(c => c.Name == GlobalProperty))
        {
            GlobalProperties.RemoveWhere(p => p.Name == OldGlobalProperty);
            GlobalProperties.RemoveWhere(p => p.Name == GlobalProperty);
            GlobalProperties.Add((GlobalProperty, GlobalPropertyType));
            RenameGlobalProperties(OldGlobalProperty, GlobalProperty);
            GlobalProperty = "";
            OldGlobalProperty = "";
            AddGlobalProperties();
            StateHasChanged();
            return;
        }
        GlobalProperties.Add((GlobalProperty, GlobalPropertyType));
        GlobalProperty = "";
        OldGlobalProperty = "";
        AddGlobalProperties();
        StateHasChanged();
    }

    private void AddGlobalProperties()
    {
        foreach (var item in Tiles)
        {
            foreach (var property in GlobalProperties)
            {
                SetProperty(item.Value, property.Name, property.Type, null);
            }
        }
    }

    private void RenameGlobalProperties(string oldName, string newName)
    {
        foreach (var item in Tiles)
        {
            if (item.Value.Properties.TryGetValue(oldName, out var oldValue))
            {
                item.Value.Properties[newName] = oldValue;
                item.Value.Properties.Remove(oldName);
            }
        }
    }
    private void SaveProperty()
    {
        SetProperty(SelectedTile, CustomPropertyName, null, CustomPropertyValue);
        ExportToJson();
        CustomPropertyName = "";
        CustomPropertyValue = "";
    }

    private static void SetProperty(TileProperties? tile, string propertyName, string propertyType, object propertyValue)
    {
        if (tile is null || string.IsNullOrEmpty(propertyName))
        {
            return;
        }
        tile.Properties ??= [];
        tile.Properties[propertyName] = (propertyValue, propertyType);
    }

    private void ExportToJson()
    {
        var properties = Tiles.Where(t => t.Value.HasContent).Select(t => t.Value);
        Output = JsonSerializer.Serialize(properties, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new TilePropertiesConverter() }
        });
    }
    // This method is triggered when the button is clicked
    private async Task CopyToClipboard()
    {
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", Output);
    }
}

