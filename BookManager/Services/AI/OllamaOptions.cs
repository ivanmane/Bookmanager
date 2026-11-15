using System;

namespace BookManager.Services.AI
{
    public class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "qwen3.5:2b";

        public double Temperature { get; set; } = 0.2;
        public double TopP { get; set; } = 0.9;
        public int TopK { get; set; } = 20;
        public int NumCtx { get; set; } = 3072;
        public int NumPredict { get; set; } = 280;

        public string KeepAlive { get; set; } = "5m";
        public bool Think { get; set; } = false;

        public TimeSpan TimeoutVerificacion { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan TimeoutRespuesta { get; set; } = TimeSpan.FromSeconds(120);

        public int MaxIntercambiosHistorial { get; set; } = 4;
    }
}
