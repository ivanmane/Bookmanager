using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BookManager.Services.AI
{
    public class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("thinking")]
        public string? Thinking { get; set; }

        public OllamaMessage() { }

        public OllamaMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    public class OllamaModelOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("top_p")]
        public double TopP { get; set; }

        [JsonPropertyName("top_k")]
        public int TopK { get; set; }

        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; }

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; }
    }

    public class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("think")]
        public bool? Think { get; set; }

        [JsonPropertyName("keep_alive")]
        public string? KeepAlive { get; set; }

        [JsonPropertyName("options")]
        public OllamaModelOptions? Options { get; set; }
    }

    public class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; set; }
    }

    public class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo>? Models { get; set; }
    }

    public class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    public class OllamaErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(OllamaChatRequest))]
    [JsonSerializable(typeof(OllamaChatResponse))]
    [JsonSerializable(typeof(OllamaTagsResponse))]
    [JsonSerializable(typeof(OllamaErrorResponse))]
    internal partial class OllamaJsonContext : JsonSerializerContext
    {
    }
}
