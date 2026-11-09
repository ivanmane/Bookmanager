using System;

namespace BookManager.Services.AI
{
    /// <summary>
    /// Configuración centralizada del asistente de IA local (Ollama).
    /// Para cambiar de modelo basta con modificar <see cref="Model"/>.
    /// </summary>
    public class OllamaOptions
    {
        // Solo se permite localhost / loopback. Ollama no debe exponerse a la red.
        public string BaseUrl { get; set; } = "http://localhost:11434";

        // Modelo instalado en Ollama (ollama pull qwen3.5:2b)
        public string Model { get; set; } = "qwen3.5:2b";

        // Parámetros conservadores pensados para un equipo de 8 GB de RAM
        public double Temperature { get; set; } = 0.2;
        public double TopP { get; set; } = 0.9;
        public int TopK { get; set; } = 20;
        public int NumCtx { get; set; } = 3072;
        public int NumPredict { get; set; } = 280;

        // Tiempo que Ollama mantiene el modelo cargado en memoria tras la última petición
        public string KeepAlive { get; set; } = "5m";

        // Qwen razona por defecto ("thinking"); con false se pide respuesta directa
        // para que el razonamiento no consuma los pocos tokens de NumPredict.
        public bool Think { get; set; } = false;

        // Tiempos de espera
        public TimeSpan TimeoutVerificacion { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan TimeoutRespuesta { get; set; } = TimeSpan.FromSeconds(120); // la primera respuesta incluye la carga del modelo

        // Mensajes de usuario+asistente que se conservan en memoria (4 intercambios = 8 mensajes)
        public int MaxIntercambiosHistorial { get; set; } = 4;
    }
}
