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
    /// <summary>
    /// Toda esta seccion se estan especificando los atributos de la lista para ver todos los prestamos pendientes
    /// </summary>
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

        // Estas propiedas nos serviran para filtrar la lista, por prioridad retraso o porque esta proxima la fecha de vencimiento
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

        // Vuelve a cargar los datos nuevos cuando volvemos a la pagina para evitar que no se reflejen los cambios
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await CargarDatosDashboardAsync();
        }

        // En esta parte se carga de forma asincrona los datos (asincrono es que no se detiene el flujo) y se cargan todos los indicadores de la pantalla
        private async Task CargarDatosDashboardAsync()
        {
            try
            {
                // Este indicador es para mostrar que se estan cargando los datos
                PrgCarga.IsActive = true;
                PrgCarga.Visibility = Visibility.Visible;
                TxtMensajeEstado.Text = "Cargando información...";
                TxtMensajeEstado.Foreground = new SolidColorBrush(Colors.Gray);
                TxtMensajeEstado.Visibility = Visibility.Visible;

                // Lectura de los JSON. Usamos await para parar la lectura asincrona hasta que termine de leer los archivos 1 por 1.
                List<Libro> libros = await _jsonService.LeerListaAsync<Libro>("libros.json");
                List<Lector> lectores = await _jsonService.LeerListaAsync<Lector>("lectores.json");
                List<Prestamo> prestamos = await _jsonService.LeerListaAsync<Prestamo>("prestamos.json");

                // Validacion de que no tenemos libros en cantidades negativas
                var libroInvalido = libros.FirstOrDefault(l => l.CantidadEjemplares <= 0);
                if (libroInvalido != null)
                {
                    throw new InvalidOperationException($"El libro \"{libroInvalido.Titulo}\" (ID: {libroInvalido.Id}) tiene una cantidad de ejemplares inválida ({libroInvalido.CantidadEjemplares}). Debe ser mayor que cero.");
                }

                // Aqui creamos la variable totalEjemplares suma todos los libros
                int totalEjemplares = libros.Sum(l => l.CantidadEjemplares);

                // Aqui hace una suma por ID.
                // ToDictionary sirve para leer los JSON
                // l representa cada elemento individual 
                // aqui utilizamos ToHashSet porque es mas rapido, es como una lista pero al leerla en vez de ir valor por valor, va directo a la memoria donde se encuentra en la tabla hash
                var librosPorId = libros.ToDictionary(l => l.Id);
                var lectoresPorId = lectores.ToDictionary(l => l.Id);
                var idsLibrosRegistrados = librosPorId.Keys.ToHashSet();

                // Aqui contamos los prestamos vencidos y pendientes de devolucion.
                // for each es para aplicar a cada elemento de una lista
                // Aqui se basa es que no tengan fecha de devolucion porque en vez de tener una columna con estatus, decidimos calcular los prestamos cuando devolucion es nula
                // Si la fecha de vencimiento es menor que hoy lo tomamos como vencido. Lo consideramos por dia no por horas.
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

                // Candado para validar que no hayan mas prestamos activos que los libros disponibles
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

                // Calcular indicadores
                TxtTotalLibros.Text = totalEjemplares.ToString();
                TxtLibrosDisponibles.Text = hayInconsistencia ? "!" : totalDisponibles.ToString();
                TxtLibrosPrestados.Text = totalPrestados.ToString();
                TxtLibrosVencidos.Text = totalVencidos.ToString();

                // Lista de prestamos pendientes
                ConstruirListaPrestamosPendientes(prestamos, librosPorId, lectoresPorId, hoy);

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

        // Aqui creamos la lista y se les asigna el orden el que se van a filtrar
        private void ConstruirListaPrestamosPendientes(
            List<Prestamo> prestamos,
            Dictionary<int, Libro> librosPorId,
            Dictionary<int, Lector> lectoresPorId,
            DateTime hoy)
        {
            var pendientesTemp = new List<PrestamoPendienteVista>();

            // Filtrar únicamente préstamos activos
            var prestamosActivos = prestamos.Where(p => p.FechaDevolucion == null).ToList();

            foreach (var prestamo in prestamosActivos)
            {
                string tituloLibro = librosPorId.TryGetValue(prestamo.LibroId, out var libro)
                    ? libro.Titulo
                    : $"(Libro ID {prestamo.LibroId} no encontrado)";

                // Datos de el lector asignado
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

                // Calculos para determinar si hay un retraso o el estado del prestamo
                DateTime fechaVenc = prestamo.FechaVencimiento.Date;
                int diasDiferencia = (fechaVenc - hoy).Days;

                int prioridad;
                int diasRetraso = 0;
                string estadoTexto;
                SolidColorBrush fondo;
                SolidColorBrush texto;

                if (diasDiferencia < 0)
                {
                    prioridad = 1;
                    diasRetraso = Math.Abs(diasDiferencia);
                    estadoTexto = diasRetraso == 1 ? "Vencido · 1 día" : $"Vencido · {diasRetraso} días";
                    fondo = new SolidColorBrush(Color.FromArgb(255, 254, 226, 226));
                    texto = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                }
                else if (diasDiferencia == 0)
                {
                    prioridad = 2;
                    estadoTexto = "Vence hoy";
                    fondo = new SolidColorBrush(Color.FromArgb(255, 254, 243, 199));
                    texto = new SolidColorBrush(Color.FromArgb(255, 180, 83, 9));
                }
                else
                {
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

            // Orden de prioridad con el que se mostraran los elementos de la lista
            // Primero se mostraran aquellos libros que estan vencidos
            var listaVencidos = pendientesTemp
                .Where(x => x.Prioridad == 1)
                .OrderByDescending(x => x.DiasRetraso);

            // Estos son los prestamos que vencen hoy
            var listaHoy = pendientesTemp
                .Where(x => x.Prioridad == 2);

            // Prestamos que aun no vencen ordenamos por >
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

        // Logica de los botones para navegar con los filtros activos.

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
