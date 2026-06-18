using System.Text.Json;
using AIChallenge.Data;
using AIChallenge.Models;

namespace AIChallenge.Repositories;

public sealed class JsonEcommerceRepository : IEcommerceRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public JsonEcommerceRepository(IWebHostEnvironment environment)
    {
        string dataDirectory = Path.Combine(environment.ContentRootPath, "DataStore");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "ecommerce.json");
    }

    public async Task<AppData> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await ReadInternalAsync(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task WriteAsync(AppData data, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureSeededAsync(cancellationToken);
            string json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<AppData> ReadInternalAsync(CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        string json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        AppData data = JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? SeedData.Create();
        bool changed = false;
        if (SeedData.EnsureProducts(data))
        {
            changed = true;
        }

        if (SeedData.EnsureAddressCatalog(data))
        {
            changed = true;
        }

        if (changed)
        {
            string updatedJson = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_filePath, updatedJson, cancellationToken);
        }

        return data;
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_filePath))
        {
            return;
        }

        string json = JsonSerializer.Serialize(SeedData.Create(), JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
