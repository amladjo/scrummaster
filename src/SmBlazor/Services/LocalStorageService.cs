using System.Text.Json;
using Microsoft.JSInterop;

namespace SmBlazor.Services;

public sealed class LocalStorageService
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LocalStorageService(IJSRuntime js) => _js = js;

    public ValueTask<string?> GetItemAsync(string key)
        => _js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetItemAsync(string key, string value)
        => _js.InvokeVoidAsync("localStorage.setItem", key, value);

    public async Task<T?> GetJsonAsync<T>(string key)
    {
        var json = await GetItemAsync(key);
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public Task SetJsonAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return SetItemAsync(key, json).AsTask();
    }
}

