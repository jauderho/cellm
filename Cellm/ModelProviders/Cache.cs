﻿using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cellm.Services.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cellm.ModelProviders;

internal class Cache : ICache
{
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheEntryOptions _memoryCacheEntryOptions;

    public Cache(IMemoryCache memoryCache, IOptions<CellmAddInConfiguration> _cellmConfiguration)
    {
        _memoryCache = memoryCache;
        _memoryCacheEntryOptions = new()
        {
            SlidingExpiration = TimeSpan.FromSeconds(_cellmConfiguration.Value.CacheTimeoutInSeconds)
        };
    }

    public void Set<T, U>(T key, U value)
    {
        _memoryCache.Set(GetHash(key), value, _memoryCacheEntryOptions);
    }

    public bool TryGetValue<T>(T key, out object? value)
    {
        return _memoryCache.TryGetValue(GetHash(key), out value);
    }

    private static string GetHash<T>(T key)
    {
        var json = JsonSerializer.Serialize(key);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash = Convert.ToHexString(bytes);

        return hash;
    }
}
