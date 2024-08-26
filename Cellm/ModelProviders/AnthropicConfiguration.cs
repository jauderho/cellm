﻿namespace Cellm.ModelProviders;

internal class AnthropicConfiguration
{
    public Uri BaseAddress { get; init; }
    public string Model { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();

    public AnthropicConfiguration()
    {
        BaseAddress = default!;
        Model = default!;
        Headers = new Dictionary<string, string>();
    }
}