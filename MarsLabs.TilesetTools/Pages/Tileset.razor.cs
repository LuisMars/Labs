using Blazored.LocalStorage;
using BlazorPanzoom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace MarsLabs.TilesetTools.Pages;
public partial class Tileset
{
    [Inject]
    public IJSRuntime JsRuntime { get; set; }

    [Inject]
    public ILocalStorageService LocalStorage { get; set; }

    public string? ImageData { get; set; }
    public string? ImageSrc { get; set; }
    private int NumRows { get; set; }
    private int NumCols { get; set; }
    private Dictionary<(int X, int Y), TileProperties> Tiles { get; set; } = [];
    private List<TileProperties> SelectedTiles { get; set; } = [];
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
    private Tabs CurrentTab { get; set; } = Tabs.Import;
    public bool LargeSidebar { get; set; }

    private DotNetObjectReference<Tileset>? DotNetHelper { get; set; }
    private Panzoom Panzoom { get; set; }
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
        if (imageFile == null)
        {
            return;
        }

        TilesetProperties.ImageFile = e.File.Name;
        var format = "image/png"; // or determine from file
        var buffer = new byte[imageFile.Size];
        await imageFile.OpenReadStream().ReadAsync(buffer);
        ImageData = Convert.ToBase64String(buffer);
        ImageSrc = $"data:{format};base64,{ImageData}";
        await LocalStorage.SetItemAsync(e.File.Name, ImageSrc);
        // Calculate grid based on image dimensions
        await GetImageDimensions();
        await ApplyGridAsync();
    }

    private async Task LoadSaveFile(InputFileChangeEventArgs e)
    {
        using var fileStream = e.File.OpenReadStream();
        var properties = await JsonSerializer.DeserializeAsync<TilesetProperties>(
            fileStream,
            new JsonSerializerOptions
            {
                Converters = { new TilePropertiesConverter() },
            });
        if (properties is null)
        {
            return;
        }
        await LoadProperties(properties);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        DotNetHelper = DotNetObjectReference.Create(this);
        await JsRuntime.InvokeVoidAsync("init", DotNetHelper);

        var properties = await LocalStorage.GetItemAsync<TilesetProperties>("TilesetProperties");
        if (properties is null)
        {
            return;
        }
        await LoadProperties(properties);
    }

    private async Task LoadProperties(TilesetProperties properties)
    {
        TilesetProperties = properties;
        Tiles = TilesetProperties.Tiles.ToDictionary(t => (t.Col, t.Row), t => t);
        foreach (var SelectedTile in SelectedTiles)
        {
            await EditTileAsync(SelectedTile.Col, SelectedTile.Row);
        }
        if (!string.IsNullOrWhiteSpace(TilesetProperties.ImageFile))
        {

            ImageSrc = await LocalStorage.GetItemAsync<string>(TilesetProperties.ImageFile);
            await ApplyGridAsync();
        }
        StateHasChanged();
    }

    private async Task GetImageDimensions()
    {
        var dimensions = await JsRuntime.InvokeAsync<int[]>("getImageDimensions", ImageSrc);
        TilesetProperties.ImageWidth = dimensions[0];
        TilesetProperties.ImageHeight = dimensions[1];
    }

    private async Task ApplyGridAsync()
    {
        if (TilesetProperties.LinkTileSize)
        {
            TilesetProperties.TileHeight = TilesetProperties.TileWidth;
        }

        if (TilesetProperties.LinkTileGapSize)
        {
            TilesetProperties.TileGapHeight = TilesetProperties.TileGapWidth;
        }

        // Assuming image dimensions are accessible via some variables like imageWidth and imageHeight
        NumRows = (int)Math.Ceiling((float)TilesetProperties.ImageHeight / (TilesetProperties.TileHeight + TilesetProperties.TileGapHeight));
        NumCols = (int)Math.Ceiling((float)TilesetProperties.ImageWidth / (TilesetProperties.TileWidth + TilesetProperties.TileGapHeight));
        await SaveAsync();
    }

    private async Task ChangeTileAsync(int directionX, int directionY = 0)
    {
        if (SelectedTiles.Count > 1)
        {
            return;
        }
        var selectedTile = SelectedTiles[0];
        var col = selectedTile.Col + directionX;
        var row = selectedTile.Row + directionY;
        if (col >= NumCols)
        {
            row++;
            col = 0;
        }
        else if (col < 0)
        {
            row--;
            col = NumCols - 1;
        }
        if (row >= NumRows)
        {
            row = 0;
            col = 0;
        }
        else if (row < 0)
        {
            row = NumRows - 1;
            col = NumCols - 1;
        }
        await EditTileAsync(col, row);
    }

    private async Task EditTileAsync(int col, int row)
    {
        if (await IsKeyDown("shift") && SelectedTiles.Count == 1)
        {
            var selectedTile = SelectedTiles[0];
            for (var c = Math.Min(selectedTile.Col, col); c <= Math.Max(selectedTile.Col, col); c++)
            {
                for (var r = Math.Min(selectedTile.Row, row); r <= Math.Max(selectedTile.Row, row); r++)
                {
                    if (!Tiles.TryGetValue((c, r), out var t))
                    {
                        t = new TileProperties { Col = c, Row = r };
                        Tiles[(c, r)] = t;
                    }
                    SelectedTiles.Add(t);
                }
            }
            return;
        }

        if (!Tiles.TryGetValue((col, row), out var tile))
        {
            tile = new TileProperties { Col = col, Row = row };
            Tiles[(col, row)] = tile;
        }
        SelectedTiles = [tile];
        CustomPropertyName = "";
        CustomPropertyValue = "";
        AddGlobalProperties();
        StateHasChanged();
    }
    private async Task AddGlobalPropertyAsync()
    {
        if (string.IsNullOrEmpty(GlobalProperty))
        {
            return;
        }

        if (!string.IsNullOrEmpty(OldGlobalProperty) || TilesetProperties.Properties.Any(c => c.Name == GlobalProperty))
        {
            TilesetProperties.Properties.RemoveWhere(p => p.Name == OldGlobalProperty);
            TilesetProperties.Properties.RemoveWhere(p => p.Name == GlobalProperty);
            TilesetProperties.Properties.Add(new(GlobalProperty, GlobalPropertyType));
            RenameGlobalProperties(OldGlobalProperty, GlobalProperty);
            GlobalProperty = "";
            OldGlobalProperty = "";
            AddGlobalProperties();
            StateHasChanged();
            await SaveAsync();

            return;
        }
        TilesetProperties.Properties.Add(new(GlobalProperty, GlobalPropertyType));
        GlobalProperty = "";
        OldGlobalProperty = "";
        AddGlobalProperties();
        await SaveAsync();
        StateHasChanged();
    }

    private async Task DeletePropertyAsync(string name)
    {
        TilesetProperties.Properties.RemoveWhere(p => p.Name == name);
        foreach (var item in Tiles.Values)
        {
            item.BoolValues.Remove(name);
            item.StringValues.Remove(name);
            item.IntValues.Remove(name);
            item.FloatValues.Remove(name);
            item.Tags = item.Tags.Where(t => t != name).ToArray();
        }

        await SaveAsync();
        StateHasChanged();
    }

    private void AddGlobalProperties()
    {
        foreach (var item in Tiles)
        {
            foreach (var property in TilesetProperties.Properties)
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

    private async Task SaveAsync()
    {
        if (SelectedTiles.Count > 0)
        {
            var selectedTile = SelectedTiles[0];
            foreach (var tile in SelectedTiles.Skip(1))
            {
                tile.IntValues = selectedTile.IntValues.ToDictionary();
                tile.FloatValues = selectedTile.FloatValues.ToDictionary();
                tile.BoolValues = selectedTile.BoolValues.ToDictionary();
                tile.StringValues = selectedTile.StringValues.ToDictionary();
                tile.Tags = selectedTile.Tags.ToArray();
            }
        }
        var properties = Tiles.Where(t => t.Value.HasContent).Select(t => t.Value);
        TilesetProperties.Tiles = properties;
        Output = JsonSerializer.Serialize(TilesetProperties, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new TilePropertiesConverter() },
            IncludeFields = true,
        });

        await LocalStorage.SetItemAsync("TilesetProperties", TilesetProperties);
    }

    private async Task SaveFile()
    {
        await SaveAsync();
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", Output);

        byte[] file = System.Text.Encoding.UTF8.GetBytes(Output);
        await JsRuntime.InvokeVoidAsync("downloadFile", $"{TilesetProperties.ImageFile}.json", "text/json", file);
    }

    private async Task<bool> IsKeyDown(string key)
    {
        return await JsRuntime.InvokeAsync<bool>("isKeyDown", key);
    }

    [JSInvokable]
    public async Task OnKey(int keyCode, bool keyUp)
    {
        //var key = (char)keyCode;
        var isControlDown = await IsKeyDown("control");
        if (keyUp || !isControlDown || (keyCode != 37 && keyCode != 39 && keyCode != 38 && keyCode != 40))
        {
            return;
        }

        await SaveAsync();
        var directionX = 0;
        var directionY = 0;
        if (keyCode == 39)
        {
            directionX = 1;
        }
        if (keyCode == 37)
        {
            directionX = -1;
        }
        if (keyCode == 38)
        {
            directionY = -1;
        }
        if (keyCode == 40)
        {
            directionY = 1;
        }
        await ChangeTileAsync(directionX, directionY);
    }
}

