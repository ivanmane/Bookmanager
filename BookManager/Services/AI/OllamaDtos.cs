using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BookManager.Services.AI
{
    /// <summary>Mensaje de la conversación (role: system | user | assistant).</summary>
    public class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        // Solo llega en respuestas cuando el modelo razona; se ignora al mostrar.
        [JsonPropertyName("thinking")]
        public string? Thinking { get; set; }

        public OllamaMessage() { }

        public OllamaMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>Opciones del modelo enviadas en la petición.</summary>
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

    /// <summary>Petición para POST /api/chat.</summary>
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

    /// <summary>Respuesta de POST /api/chat (con stream = false).</summary>
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

    /// <summary>Respuesta de GET /api/tags (modelos instalados).</summary>
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

    /// <summary>Cuerpo de error de Ollama: {"error": "..."}.</summary>
    public class OllamaErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    // Serialización generada en tiempo de compilación: segura con PublishTrimmed (Release)
    // y sin reflexión. No requiere paquetes NuGet adicionales.
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(OllamaChatRequest))]
    [JsonSerializable(typeof(OllamaChatResponse))]
    [JsonSerializable(typeof(OllamaTagsResponse))]
    [JsonSerializable(typeof(OllamaErrorResponse))]
    internal partial class OllamaJsonContext : JsonSerializerContext
    {
    }
}
