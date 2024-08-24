﻿using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using Cellm.Exceptions;
using Cellm.Prompts;
using Microsoft.Extensions.Options;
using Cellm.ModelProviders;

namespace Cellm.Models;

internal class AnthropicClient : IClient
{
    private readonly AnthropicConfiguration _anthropicConfiguration;
    private readonly HttpClient _httpClient;

    public AnthropicClient(
        IOptions<AnthropicConfiguration> anthropicConfiguration,
        IHttpClientFactory httpClientFactory)
    {
        _anthropicConfiguration = anthropicConfiguration.Value;
        _httpClient = httpClientFactory.CreateClient();
    }

    public string Send(Prompt prompt)
    {
        // TODO: Find more elegant way of doing this
        foreach (var header in  _anthropicConfiguration.Headers)
        {
            _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        var requestBody = new RequestBody
        {
            System = prompt.SystemMessage,
            Messages = prompt.messages.Select(x => new Message { Content = x.Content, Role = x.Role.ToString().ToLower() }).ToList(),
            Model = _anthropicConfiguration.Model,
            MaxTokens = 256,
            Temperature = prompt.Temperature
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var json = JsonSerializer.Serialize(requestBody, options);
        var jsonAsString = new StringContent(json, Encoding.UTF8, "application/json");

        var response = _httpClient.PostAsync(new Uri(_anthropicConfiguration.BaseAddress, "/v1/messages"), jsonAsString).Result;
        var responseBodyAsString = response.Content.ReadAsStringAsync().Result;

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(responseBodyAsString, null, response.StatusCode);
        }

        var responseBody = JsonSerializer.Deserialize<ResponseBody>(responseBodyAsString, options);
        var assistantMessage = responseBody?.Content?.Last()?.Text ?? "No content received from API";

        if (assistantMessage.StartsWith("#INSTRUCTION_ERROR?"))
        {
            throw new CellmException(assistantMessage);
        }

        return assistantMessage;
    }

    private class ResponseBody
    {
        public List<Content> Content { get; set; }

        public string Id { get; set; }

        public string Model { get; set; }

        public string Role { get; set; }

        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; }

        [JsonPropertyName("stop_sequence")]
        public string? StopSequence { get; set; }

        public string Type { get; set; }

        public Usage Usage { get; set; }
    }

    private class RequestBody
    {
        public List<Message> Messages { get; set; }

        public string System { get; set; }

        public string Model { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        public double Temperature { get; set; }
    }

    private class Message
    {
        public string Role { get; set; }

        public string Content { get; set; }
    }

    private class Content
    {
        public string Text { get; set; }

        public string Type { get; set; }
    }

    private class Usage
    {
        public int InputTokens { get; set; }

        public int OutputTokens { get; set; }
    }
}
