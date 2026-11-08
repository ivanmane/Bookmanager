using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;
using BookManager.Models;
using BookManager.Services;

namespace BookManager.Views
{
    // Clase auxiliar para la presentación de préstamos pendientes en el Dashboard
    public class PrestamoPendienteVista
    {
        public int PrestamoId { get; set; }
        public string TituloLibro { get; set; } = string.Empty;
        public string NombreLector { get; set; } = string.Empty;
        public string MatriculaLector { get; set; } = string.Empty;
        public string FechaPrestamoTexto { get; set; } = string.Empty;
        public string FechaVencimientoTexto { get; set; } = string.Empty;
        public string EstadoRetraso { get; set; } = string.Empty;
        public SolidColorBrush ColorFondoEstado { get; set; } = new SolidColorBrush(Colors.Transparent);
        public SolidColorBrush ColorTextoEstado { get; set; } = new SolidColorBrush(Colors.Black);
        public string CorreoLector { get; set; } = string.Empty;
        public string TelefonoLector { get; set; } = string.Empty;

        // Propiedades auxiliares para ordenamiento estricto
        public int Prioridad { get; set; }
        public int DiasRetraso { get; set; }
        public DateTime FechaVencimiento { get; set; }
    }

    public sealed partial class DashboardPage : Page
    {
        private readonly JsonService _jsonService;

        public DashboardPage()
        {
            this.InitializeComponent();
            _jsonService = new JsonService();

            this.Loaded += DashboardPage_Loaded;
        }

        // Se ejecuta al cargar la página por primera vez
        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CargarDatosDashboardAsync();
        }

        // Se ejecuta cada vez que se navega de regreso a esta página
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await CargarDatosDashboardAsync();
        }

        // Método asíncrono para leer los tres JSON, calcular indicadores y préstamos pendientes
        private async Task CargarDatosDashboardAsync()
        {
            try
            {
                // Indicador visual de carga
                PrgCarga.IsActive = true;
                PrgCarga.Visibility = Visibility.Visible;
                TxtMensajeEstado.Text = "Cargando información...";
                TxtMensajeEstado.Foreground = new SolidColorBrush(Colors.Gray);
                TxtMensajeEstado.Visibility = Visibility.Visible;

                // Lectura de los tres archivos JSON mediante JsonService
                List<Libro> libros = await _jsonService.LeerListaAsync<Libro>("libros.json");
                List<Lector> lectores = await _jsonService.LeerListaAsync<Lector>("lectores.json");
                List<Prestamo> prestamos = await _jsonService.LeerListaAsync<Prestamo>("prestamos.json");

                // 1. Validar que ningún libro contenga explícitamente CantidadEjemplares <= 0
                var libroInvalido = libros.FirstOrDefault(l => l.CantidadEjemplares <= 0);
                if (libroInvalido != null)
                {
                    throw new InvalidOperationException($"El libro \"{libroInvalido.Titulo}\" (ID: {libroInvalido.Id}) tiene una cantidad de ejemplares inválida ({libroInvalido.CantidadEjemplares}). Debe ser mayor que cero.");
                }

                // 2. Total de ejemplares físicos registrados
                int totalEjemplares = libros.Sum(l => l.CantidadEjemplares);

                // Mapeos rápidos para relacionar información
                var librosPorId = libros.ToDictionary(l => l.Id);
                var lectoresPorId = lectores.ToDictionary(l => l.Id);
                var idsLibrosRegistrados = librosPorId.Keys.ToHashSet();

                // 3. Contar préstamos activos y vencidos asociados a libros existentes
                int totalPrestados = 0;
                int totalVencidos = 0;
                DateTime hoy = DateTime.Today;

                foreach (var prestamo in prestamos)
                {
                    if (idsLibrosRegistrados.Contains(prestamo.LibroId))
                    {
                        if (prestamo.FechaDevolucion == null)
                        {
                            totalPrestados++;

                            if (prestamo.FechaVencimiento.Date < hoy)
                            {
                                totalVencidos++;
                            }
                        }
                    }
                }

                // 4. Detección de inconsistencias: si los préstamos activos superan las existencias
                bool hayInconsistencia = false;
                string detalleInconsistencia = string.Empty;

                if (totalPrestados > totalEjemplares)
                {
                    hayInconsistencia = true;
                    detalleInconsistencia = $"Inconsistencia de inventario: Existen {totalPrestados} préstamos activos pero solo {totalEjemplares} ejemplares registrados en total.";
                }
                else
                {
                    foreach (var libro in libros)
                    {
                        int prestamosDelLibro = prestamos.Count(p => p.LibroId == libro.Id && p.FechaDevolucion == null);
                        if (prestamosDelLibro > libro.CantidadEjemplares)
                        {
                            hayInconsistencia = true;
                            detalleInconsistencia = $"Inconsistencia de inventario: El libro \"{libro.Titulo}\" (ID: {libro.Id}) tiene {prestamosDelLibro} préstamos activos pero solo {libro.CantidadEjemplares} ejemplar(es) registrado(s).";
                            break;
                        }
                    }
                }

                int totalDisponibles = totalEjemplares - totalPrestados;

                // 5. Asignar los 4 indicadores numéricos
                TxtTotalLibros.Text = totalEjemplares.ToString();
                TxtLibrosDisponibles.Text = hayInconsistencia ? "!" : totalDisponibles.ToString();
                TxtLibrosPrestados.Text = totalPrestados.ToString();
                TxtLibrosVencidos.Text = totalVencidos.ToString();

                // 6. Construir lista de Préstamos pendientes de devolución (FechaDevolucion == null)
                ConstruirListaPrestamosPendientes(prestamos, librosPorId, lectoresPorId, hoy);

                // Ocultar carga
                PrgCarga.IsActive = false;
                PrgCarga.Visibility = Visibility.Collapsed;

                if (hayInconsistencia)
                {
                    TxtMensajeEstado.Text = detalleInconsistencia;
                    TxtMensajeEstado.Foreground = new SolidColorBrush(Colors.Red);
                    TxtMensajeEstado.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtMensajeEstado.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                TxtTotalLibros.Text = "-";
                TxtLibrosDisponibles.Text = "-";
                TxtLibrosPrestados.Text = "-";
                TxtLibrosVencidos.Text = "-";

                PrgCarga.IsActive = false;
                PrgCarga.Visibility = Visibility.Collapsed;

                TxtMensajeEstado.Text = $"No se pudieron cargar los datos de la biblioteca: {ex.Message}";
                TxtMensajeEstado.Foreground = new SolidColorBrush(Colors.Red);
                TxtMensajeEstado.Visibility = Visibility.Visible;

                ContenedorTablaPrestamos.Visibility = Visibility.Collapsed;
                TxtSinPrestamos.Visibility = Visibility.Collapsed;
            }
        }

        // Construye y ordena la lista de préstamos pendientes según la prioridad requerida
        private void ConstruirListaPrestamosPendientes(
            List<Prestamo> prestamos,
            Dictionary<int, Libro> librosPorId,
            Dictionary<int, Lector> lectoresPorId,
            DateTime hoy)
        {
            var pendientesTemp = new List<PrestamoPendienteVista>();

            // Filtrar únicamente préstamos activos (no devueltos)
            var prestamosActivos = prestamos.Where(p => p.FechaDevolucion == null).ToList();

            foreach (var prestamo in prestamosActivos)
            {
                // Obtener datos del libro relacionado o indicar dato faltante
                string tituloLibro = librosPorId.TryGetValue(prestamo.LibroId, out var libro)
                    ? libro.Titulo
                    : $"(Libro ID {prestamo.LibroId} no encontrado)";

                // Obtener datos del lector relacionado o indicar dato faltante
                string nombreLector, matriculaLector, correoLector, telefonoLector;
                if (lectoresPorId.TryGetValue(prestamo.LectorId, out var lector))
                {
                    nombreLector = lector.Nombre;
                    matriculaLector = lector.Matricula;
                    correoLector = lector.Correo;
                    telefonoLector = string.IsNullOrWhiteSpace(lector.Telefono) ? "-" : lector.Telefono;
                }
                else
                {
                    nombreLector = $"(Lector ID {prestamo.LectorId} no encontrado)";
                    matriculaLector = "-";
                    correoLector = "-";
                    telefonoLector = "-";
                }

                // Cálculo de estado y días de retraso
                DateTime fechaVenc = prestamo.FechaVencimiento.Date;
                int diasDiferencia = (fechaVenc - hoy).Days;

                int prioridad;
                int diasRetraso = 0;
                string estadoTexto;
                SolidColorBrush fondo;
                SolidColorBrush texto;

                if (diasDiferencia < 0)
                {
                    // 1. Vencido: rojo discreto (#B91C1C) con fondo suave (#FEE2E2)
                    prioridad = 1;
                    diasRetraso = Math.Abs(diasDiferencia);
                    estadoTexto = diasRetraso == 1 ? "Vencido · 1 día" : $"Vencido · {diasRetraso} días";
                    fondo = new SolidColorBrush(Color.FromArgb(255, 254, 226, 226));
                    texto = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                }
                else if (diasDiferencia == 0)
                {
                    // 2. Vence hoy: amarillo/ámbar (#B45309) con fondo suave (#FEF3C7)
                    prioridad = 2;
                    estadoTexto = "Vence hoy";
                    fondo = new SolidColorBrush(Color.FromArgb(255, 254, 243, 199));
                    texto = new SolidColorBrush(Color.FromArgb(255, 180, 83, 9));
                }
                else
                {
                    // 3. Futuro: azul suave (#245A96) con fondo suave (#EEF2FF)
                    prioridad = 3;
                    estadoTexto = diasDiferencia == 1 ? "Vence en 1 día" : $"Vence en {diasDiferencia} días";
                    fondo = new SolidColorBrush(Color.FromArgb(255, 238, 242, 255));
                    texto = new SolidColorBrush(Color.FromArgb(255, 36, 90, 150));
                }

                pendientesTemp.Add(new PrestamoPendienteVista
                {
                    PrestamoId = prestamo.Id,
                    TituloLibro = tituloLibro,
                    NombreLector = nombreLector,
                    MatriculaLector = matriculaLector,
                    FechaPrestamoTexto = prestamo.FechaPrestamo.ToString("dd/MM/yyyy"),
                    FechaVencimientoTexto = prestamo.FechaVencimiento.ToString("dd/MM/yyyy"),
                    EstadoRetraso = estadoTexto,
                    ColorFondoEstado = fondo,
                    ColorTextoEstado = texto,
                    CorreoLector = correoLector,
                    TelefonoLector = telefonoLector,
                    Prioridad = prioridad,
                    DiasRetraso = diasRetraso,
                    FechaVencimiento = fechaVenc
                });
            }

            // Aplicar orden de prioridad estricto:
            // 1. Vencidos primero, comenzando por los que tienen mayor retraso
            var listaVencidos = pendientesTemp
                .Where(x => x.Prioridad == 1)
                .OrderByDescending(x => x.DiasRetraso);

            // 2. Préstamos que vencen hoy
            var listaHoy = pendientesTemp
                .Where(x => x.Prioridad == 2);

            // 3. Préstamos futuros ordenados por fecha más cercana
            var listaFuturos = pendientesTemp
                .Where(x => x.Prioridad == 3)
                .OrderBy(x => x.FechaVencimiento);

            var pendientesOrdenados = listaVencidos.Concat(listaHoy).Concat(listaFuturos).ToList();

            if (pendientesOrdenados.Count == 0)
            {
                TxtSinPrestamos.Visibility = Visibility.Visible;
                ContenedorTablaPrestamos.Visibility = Visibility.Collapsed;
                ListaPrestamosPendientes.ItemsSource = null;
            }
            else
            {
                TxtSinPrestamos.Visibility = Visibility.Collapsed;
                ContenedorTablaPrestamos.Visibility = Visibility.Visible;
                ListaPrestamosPendientes.ItemsSource = pendientesOrdenados;
            }
        }

        // ========================================================
        // NAVEGACIÓN HACIA EL CATÁLOGO Y LECTORES
        // ========================================================

        private void IndicadorTotal_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(CatalogoPage), "Todos");
        }

        private void IndicadorDisponibles_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(CatalogoPage), "Disponibles");
        }

        private void IndicadorPrestados_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(CatalogoPage), "Prestados");
        }

        private void IndicadorVencidos_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(CatalogoPage), "Vencidos");
        }

        private void BtnVerLectores_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(LectoresPage));
        }

        private void BtnVerPrestamos_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(PrestamosPage));
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(LoginPage));
        }
    }
}
