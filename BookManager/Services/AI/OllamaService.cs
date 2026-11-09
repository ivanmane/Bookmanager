using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BookManager.Services.AI
{
    public enum OllamaEstado
    {
        Disponible,
        ServidorNoDisponible,
        ModeloNoInstalado,
        TiempoAgotado,
        Cancelado,
        ErrorRespuesta
    }

    /// <summary>
    /// Resultado de una operación contra Ollama. El servicio no lanza excepciones hacia la UI:
    /// siempre devuelve un estado y un mensaje listo para mostrar al usuario.
    /// </summary>
    public sealed class OllamaResultado
    {
        public OllamaEstado Estado { get; }
        public string Contenido { get; }
        public string MensajeUsuario { get; }
        public bool Exito => Estado == OllamaEstado.Disponible;

        private OllamaResultado(OllamaEstado estado, string contenido, string mensajeUsuario)
        {
            Estado = estado;
            Contenido = contenido;
            MensajeUsuario = mensajeUsuario;
        }

        public static OllamaResultado Ok(string contenido = "") =>
            new(OllamaEstado.Disponible, contenido, string.Empty);

        public static OllamaResultado Falla(OllamaEstado estado, string mensajeUsuario) =>
            new(estado, string.Empty, mensajeUsuario);
    }

    /// <summary>
    /// Única responsabilidad: hablar con el servidor Ollama local (http://localhost:11434).
    /// </summary>
    public sealed class OllamaService
    {
        // Un solo HttpClient para toda la aplicación (no se crea uno por mensaje).
        // - Timeout infinito: cada petición controla su propio tiempo con CancellationToken.
        // - UseProxy = false: el tráfico a localhost nunca debe pasar por un proxy del sistema.
        private static readonly HttpClient _http = new(new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        private readonly OllamaOptions _opciones;
        private readonly Uri? _baseUri;
        private readonly string? _errorConfiguracion;

        public OllamaOptions Opciones => _opciones;

        public OllamaService(OllamaOptions? opciones = null)
        {
            _opciones = opciones ?? new OllamaOptions();

            string url = _opciones.BaseUrl.EndsWith('/') ? _opciones.BaseUrl : _opciones.BaseUrl + "/";

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                _errorConfiguracion = $"La URL de Ollama configurada no es válida: {_opciones.BaseUrl}";
            }
            else if (!uri.IsLoopback)
            {
                // Seguridad: la IA debe ser estrictamente local.
                _errorConfiguracion = "Por seguridad, el asistente solo puede conectarse a Ollama en este mismo equipo (localhost).";
            }
            else
            {
                _baseUri = uri;
            }
        }

        // ------------------------------------------------------------------
        // 1 y 2. Disponibilidad del servidor y del modelo (GET /api/tags)
        // ------------------------------------------------------------------
        public async Task<OllamaResultado> VerificarAsync(CancellationToken cancelacion = default)
        {
            if (_baseUri == null)
            {
                return OllamaResultado.Falla(OllamaEstado.ErrorRespuesta, _errorConfiguracion ?? "Configuración de Ollama no válida.");
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
            cts.CancelAfter(_opciones.TimeoutVerificacion);

            try
            {
                using HttpResponseMessage respuesta = await _http.GetAsync(new Uri(_baseUri, "api/tags"), cts.Token);

                if (!respuesta.IsSuccessStatusCode)
                {
                    return OllamaResultado.Falla(OllamaEstado.ErrorRespuesta,
                        $"Ollama respondió con el código {(int)respuesta.StatusCode}. Revisa que la instalación de Ollama esté en buen estado.");
                }

                string json = await respuesta.Content.ReadAsStringAsync(cts.Token);
                OllamaTagsResponse? tags = JsonSerializer.Deserialize(json, OllamaJsonContext.Default.OllamaTagsResponse);

                bool instalado = tags?.Models?.Any(m => CoincideModelo(m.Name) || CoincideModelo(m.Model)) ?? false;

                return instalado
                    ? OllamaResultado.Ok()
                    : OllamaResultado.Falla(OllamaEstado.ModeloNoInstalado, MensajeModeloNoInstalado());
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                return OllamaResultado.Falla(OllamaEstado.Cancelado, "Operación cancelada.");
            }
            catch (OperationCanceledException)
            {
                // Sin respuesta en el tiempo de verificación: se trata como servidor no disponible
                return OllamaResultado.Falla(OllamaEstado.ServidorNoDisponible, MensajeServidorNoDisponible());
            }
            catch (HttpRequestException)
            {
                return OllamaResultado.Falla(OllamaEstado.ServidorNoDisponible, MensajeServidorNoDisponible());
            }
            catch (JsonException)
            {
                return OllamaResultado.Falla(OllamaEstado.ErrorRespuesta, "Ollama devolvió una respuesta que no se pudo interpretar.");
            }
        }

        // ------------------------------------------------------------------
        // 3, 4, 5, 6 y 7. Envío de mensajes (POST /api/chat, stream = false)
        // ------------------------------------------------------------------
        public async Task<OllamaResultado> EnviarAsync(IEnumerable<OllamaMessage> mensajes, CancellationToken cancelacion = default)
        {
            if (_baseUri == null)
            {
                return OllamaResultado.Falla(OllamaEstado.ErrorRespuesta, _errorConfiguracion ?? "Configuración de Ollama no válida.");
            }

            var peticion = new OllamaChatRequest
            {
                Model = _opciones.Model,
                Messages = mensajes.ToList(),
                Stream = false,
                Think = _opciones.Think,
                KeepAlive = _opciones.KeepAlive,
                Options = new OllamaModelOptions
                {
                    Temperature = _opciones.Temperature,
                    TopP = _opciones.TopP,
                    TopK = _opciones.TopK,
                    NumCtx = _opciones.NumCtx,
                    NumPredict = _opciones.NumPredict
                }
            };

            string cuerpo = JsonSerializer.Serialize(peticion, OllamaJsonContext.Default.OllamaChatRequest);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
            cts.CancelAfter(_opciones.TimeoutRespuesta);

            try
            {
                using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");
                using HttpResponseMessage respuesta = await _http.PostAsync(new Uri(_baseUri, "api/chat"), contenido, cts.Token);
                string json = await respuesta.Content.ReadAsStringAsync(cts.Token);

                if (!respuesta.IsSuccessStatusCode)
                {
                    return InterpretarError(respuesta.StatusCode, json);
                }

                OllamaChatResponse? datos = JsonSerializer.Deserialize(json, OllamaJsonContext.Default.OllamaChatResponse);

                // Solo se usa message.content; nunca se muestra el JSON completo.
                string texto = LimpiarRazonamiento(datos?.Message?.Content);

                if (string.IsNullOrWhiteSpace(texto))
                {
                    return OllamaResultado.Falla(OllamaEstado.ErrorRespuesta,
                        "El modelo no devolvió texto. Intenta reformular la pregunta.");
                }

                // La respuesta se cortó por el límite de tokens (num_predict)
                if (string.Equals(datos?.DoneReason, "length", StringComparison.OrdinalIgnoreCase))
                {
                    texto += " […]";
                }

                return OllamaResultado.Ok(texto);
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                return OllamaResultado.Falla(OllamaEstado.Cancelado, "Solicitud cancelada.");
            }
            catch (OperationCanceledException)
            {
                return OllamaResultado.Falla(OllamaEstado.TiempoAgotado,
                    "La respuesta está tardando demasiado. Es normal que la primera consulta tarde mientras Ollama carga el modelo; intenta de nuevo en unos segundos.");
            }
            catch (HttpRequestException)
            {
                return OllamaResultado.Falla(OllamaEstado.ServidorNoDisponible, MensajeServidorNoDisponible());
            }
            catch (JsonException)
            {
                return OllamaResultado.Falla(OllamaEstado.ErrorRespuesta, "Ollama devolvió una respuesta que no se pudo interpretar.");
            }
        }

        // ------------------------------------------------------------------
        // Utilidades internas
        // ------------------------------------------------------------------
        private OllamaResultado InterpretarError(HttpStatusCode codigo, string json)
        {
            string? detalle = null;
            try
            {
                detalle = JsonSerializer.Deserialize(json, OllamaJsonContext.Default.OllamaErrorResponse)?.Error;
            }
            catch (JsonException)
            {
                // El cuerpo no era JSON de error; se usa el mensaje genérico.
            }

            // Ollama responde 404 cuando el modelo no está instalado.
            if (codigo == HttpStatusCode.NotFound)
            {
                return OllamaResultado.Falla(OllamaEstado.ModeloNoInstalado, MensajeModeloNoInstalado());
            }

            string mensaje = string.IsNullOrWhiteSpace(detalle)
                ? $"Ollama respondió con el código {(int)codigo}."
                : $"Ollama informó un problema: {detalle}";

            return OllamaResultado.Falla(OllamaEstado.ErrorRespuesta, mensaje);
        }

        private bool CoincideModelo(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return false;

            string esperado = _opciones.Model.Contains(':') ? _opciones.Model : _opciones.Model + ":latest";
            return string.Equals(nombre, esperado, StringComparison.OrdinalIgnoreCase);
        }

        // Por si algún modelo devuelve su razonamiento dentro del contenido (<think>...</think>)
        private static string LimpiarRazonamiento(string? contenido)
        {
            if (string.IsNullOrEmpty(contenido)) return string.Empty;

            string limpio = Regex.Replace(contenido, "<think>.*?</think>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            int abierto = limpio.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (abierto >= 0)
            {
                limpio = limpio.Substring(0, abierto);
            }

            return limpio.Trim();
        }

        private string MensajeServidorNoDisponible() =>
            "No pude conectarme con Ollama. Revisa lo siguiente:\n" +
            "• Que Ollama esté instalado (ollama.com/download).\n" +
            "• Que esté en ejecución (ícono de la llama junto al reloj, o ejecuta \"ollama serve\" en una terminal).\n" +
            $"• Que responda en {_opciones.BaseUrl}.\n" +
            "BookManager sigue funcionando con normalidad.";

        private string MensajeModeloNoInstalado() =>
            $"Ollama está activo, pero el modelo \"{_opciones.Model}\" no está instalado.\n" +
            $"Abre una terminal y ejecuta:\nollama pull {_opciones.Model}";
    }
}
