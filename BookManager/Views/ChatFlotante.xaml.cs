using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using BookManager.Models;
using BookManager.Services;
using BookManager.Services.AI;

namespace BookManager.Views
{
    public sealed partial class ChatFlotante : UserControl
    {
        private enum TipoBurbuja
        {
            Usuario,
            Asistente,
            Error
        }

        private readonly OllamaOptions _opciones = new();
        private readonly OllamaService _servicio;
        private readonly JsonService _jsonService = new();

        private readonly List<OllamaMessage> _historial = new();

        private CancellationTokenSource? _cts;
        private bool _generando;
        private int _sesion;

        public ChatFlotante()
        {
            this.InitializeComponent();
            _servicio = new OllamaService(_opciones);

            ConfigurarSombras();
            AgregarMensajeBienvenida();
        }

        public void LimpiarSesion()
        {
            _sesion++;

            _cts?.Cancel();
            _cts = null;
            _generando = false;

            _historial.Clear();
            PanelMensajes.Children.Clear();
            TxtMensaje.Text = string.Empty;

            IndicadorEspera.Visibility = Visibility.Collapsed;
            BtnEnviar.IsEnabled = true;
            PanelChat.Visibility = Visibility.Collapsed;

            AgregarMensajeBienvenida();
        }

        private void ConfigurarSombras()
        {
            try
            {
                var sombraBoton = new ThemeShadow();
                sombraBoton.Receivers.Add(RaizChat);
                BtnFlotante.Shadow = sombraBoton;
                BtnFlotante.Translation = new Vector3(0, 0, 24);

                var sombraPanel = new ThemeShadow();
                sombraPanel.Receivers.Add(RaizChat);
                PanelChat.Shadow = sombraPanel;
                PanelChat.Translation = new Vector3(0, 0, 32);
            }
            catch
            {
            }
        }

        private void ChatFlotante_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double altoDisponible = e.NewSize.Height - 48 - 56 - 12;
            double anchoDisponible = e.NewSize.Width - 48;

            PanelChat.Height = Math.Clamp(altoDisponible, 320, 480);
            PanelChat.Width = Math.Clamp(anchoDisponible, 280, 370);
        }

        private static Brush Pincel(string clave) => (Brush)Application.Current.Resources[clave];

        private void BtnFlotante_Click(object sender, RoutedEventArgs e)
        {
            if (PanelChat.Visibility == Visibility.Visible)
            {
                CerrarPanel();
            }
            else
            {
                AbrirPanel();
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            CerrarPanel();
        }

        private void AbrirPanel()
        {
            PanelChat.Visibility = Visibility.Visible;
            TxtMensaje.Focus(FocusState.Programmatic);
            ScrollAlFinal();

            _ = ActualizarEstadoIAAsync();
        }

        private void CerrarPanel()
        {
            PanelChat.Visibility = Visibility.Collapsed;
        }

        private async Task ActualizarEstadoIAAsync()
        {
            try
            {
                OllamaResultado resultado = await _servicio.VerificarAsync();
                AplicarEstadoIA(resultado.Estado);
            }
            catch
            {
                AplicarEstadoIA(OllamaEstado.ServidorNoDisponible);
            }
        }

        private void AplicarEstadoIA(OllamaEstado estado)
        {
            switch (estado)
            {
                case OllamaEstado.Disponible:
                    PuntoEstado.Fill = Pincel("BrushExito");
                    TxtEstadoIA.Text = "IA local · Conectada";
                    break;

                case OllamaEstado.ModeloNoInstalado:
                    PuntoEstado.Fill = Pincel("BrushAdvertencia");
                    TxtEstadoIA.Text = "IA local · Modelo no instalado";
                    break;

                case OllamaEstado.ServidorNoDisponible:
                    PuntoEstado.Fill = Pincel("BrushError");
                    TxtEstadoIA.Text = "IA local · Ollama no disponible";
                    break;

                default:
                    break;
            }
        }

        private async void BtnEnviar_Click(object sender, RoutedEventArgs e)
        {
            await EnviarMensajeAsync();
        }

        private void TxtMensaje_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                _ = EnviarMensajeAsync();
            }
        }

        private void TxtMensaje_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space && sender is TextBox tb && tb.SelectionStart == 0)
            {
                e.Handled = true;
            }
        }

        private async Task EnviarMensajeAsync()
        {
            if (_generando)
            {
                return;
            }

            string texto = TxtMensaje.Text.Trim();
            if (string.IsNullOrWhiteSpace(texto))
            {
                return;
            }

            int sesion = _sesion;
            _generando = true;
            BtnEnviar.IsEnabled = false;

            AgregarBurbuja(texto, TipoBurbuja.Usuario);
            TxtMensaje.Text = string.Empty;
            IndicadorEspera.Visibility = Visibility.Visible;
            ScrollAlFinal();

            var cts = new CancellationTokenSource();
            _cts = cts;

            try
            {
                List<Libro> libros = new();
                List<Lector> lectores = new();
                List<Prestamo> prestamos = new();

                try
                {
                    libros = await _jsonService.LeerListaAsync<Libro>("libros.json");
                    lectores = await _jsonService.LeerListaAsync<Lector>("lectores.json");
                    prestamos = await _jsonService.LeerListaAsync<Prestamo>("prestamos.json");
                }
                catch
                {
                }

                string contextoBiblioteca = PromptBiblioteca.ConstruirContextoBiblioteca(libros, lectores, prestamos);

                OllamaResultado resultado = await _servicio.EnviarAsync(ConstruirMensajes(texto, contextoBiblioteca), cts.Token);

                if (sesion != _sesion)
                {
                    return;
                }

                if (resultado.Exito)
                {
                    _historial.Add(new OllamaMessage("user", texto));
                    _historial.Add(new OllamaMessage("assistant", resultado.Contenido));
                    RecortarHistorial();

                    AgregarBurbuja(resultado.Contenido, TipoBurbuja.Asistente);
                }
                else if (resultado.Estado != OllamaEstado.Cancelado)
                {
                    AgregarBurbuja(resultado.MensajeUsuario, TipoBurbuja.Error);
                }

                AplicarEstadoIA(resultado.Estado);
            }
            catch (Exception)
            {
                if (sesion == _sesion)
                {
                    AgregarBurbuja("Ocurrió un error inesperado al consultar al asistente. Intenta de nuevo.", TipoBurbuja.Error);
                }
            }
            finally
            {
                if (sesion == _sesion)
                {
                    _generando = false;
                    IndicadorEspera.Visibility = Visibility.Collapsed;
                    BtnEnviar.IsEnabled = true;

                    if (ReferenceEquals(_cts, cts))
                    {
                        _cts = null;
                    }

                    ScrollAlFinal();
                    TxtMensaje.Focus(FocusState.Programmatic);
                }

                cts.Dispose();
            }
        }

        private List<OllamaMessage> ConstruirMensajes(string preguntaActual, string contextoBiblioteca)
        {
            var mensajes = new List<OllamaMessage>
            {
                new OllamaMessage("system", PromptBiblioteca.ObtenerPromptSistema(contextoBiblioteca))
            };

            mensajes.AddRange(_historial);
            mensajes.Add(new OllamaMessage("user", preguntaActual));
            return mensajes;
        }

        private void RecortarHistorial()
        {
            int maximo = _opciones.MaxIntercambiosHistorial * 2;
            if (_historial.Count > maximo)
            {
                _historial.RemoveRange(0, _historial.Count - maximo);
            }
        }

        private void AgregarMensajeBienvenida()
        {
            AgregarBurbuja(PromptBiblioteca.MensajeBienvenida, TipoBurbuja.Asistente);
        }

        private void AgregarBurbuja(string texto, TipoBurbuja tipo)
        {
            bool esUsuario = tipo == TipoBurbuja.Usuario;
            bool esError = tipo == TipoBurbuja.Error;

            var bloque = new TextBlock
            {
                Text = texto,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                FontSize = 13.5,
                Foreground = esUsuario
                    ? new SolidColorBrush(Microsoft.UI.Colors.White)
                    : esError ? Pincel("BrushError") : Pincel("BrushTextoPrincipal")
            };

            var burbuja = new Border
            {
                Child = bloque,
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 290,
                HorizontalAlignment = esUsuario ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                CornerRadius = esUsuario ? new CornerRadius(12, 12, 2, 12) : new CornerRadius(12, 12, 12, 2),
                Background = esUsuario
                    ? Pincel("BrushAzulMarino")
                    : esError
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 254, 226, 226))
                        : Pincel("BrushSuperficie"),
                BorderBrush = esError ? Pincel("BrushError") : Pincel("BrushBorde"),
                BorderThickness = new Thickness(esUsuario ? 0 : 1)
            };

            PanelMensajes.Children.Add(burbuja);
            ScrollAlFinal();
        }

        private void ScrollAlFinal()
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                ScrollMensajes.UpdateLayout();
                ScrollMensajes.ChangeView(null, ScrollMensajes.ScrollableHeight, null);
            });
        }
    }
}
