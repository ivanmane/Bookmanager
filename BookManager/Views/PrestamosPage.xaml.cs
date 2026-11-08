using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI;
using BookManager.Models;
using BookManager.Services;

namespace BookManager.Views
{
    // Elemento auxiliar para presentar lectores en el ComboBox de registro
    public class LectorComboItem
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;
        public string TextoPresentacion => $"{Nombre} ({Matricula})";
        public override string ToString() => TextoPresentacion;
    }

    // Elemento auxiliar para presentar libros disponibles en el ComboBox de registro
    public class LibroComboItem
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public int EjemplaresDisponibles { get; set; }
        public string TextoPresentacion => $"{Titulo} ({EjemplaresDisponibles} disp.)";
        public override string ToString() => TextoPresentacion;
    }

    // Elemento auxiliar para presentar filas en la tabla del historial de préstamos
    public class PrestamoItemVista
    {
        public int Id { get; set; }
        public int LibroId { get; set; }
        public int LectorId { get; set; }
        public string TituloLibro { get; set; } = string.Empty;
        public string NombreLector { get; set; } = string.Empty;
        public string MatriculaLector { get; set; } = string.Empty;
        public DateTime FechaPrestamo { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public DateTime? FechaDevolucion { get; set; }

        public string FechaPrestamoTexto => FechaPrestamo.ToString("dd/MM/yyyy");
        public string FechaVencimientoTexto => FechaVencimiento.ToString("dd/MM/yyyy");
        public string FechaDevolucionTexto => FechaDevolucion.HasValue ? FechaDevolucion.Value.ToString("dd/MM/yyyy") : "-";

        public string Estado { get; set; } = string.Empty;
        public SolidColorBrush ColorFondoEstado { get; set; } = new(Colors.Transparent);
        public SolidColorBrush ColorTextoEstado { get; set; } = new(Colors.Black);

        public bool EsActivo => FechaDevolucion == null;
    }

    public sealed partial class PrestamosPage : Page
    {
        private readonly JsonService _jsonService;
        private List<Libro> _libros = new();
        private List<Lector> _lectores = new();
        private List<Prestamo> _prestamos = new();
        private List<PrestamoItemVista> _prestamosVista = new();

        // Bandera de inicialización para evitar disparos prematuros de eventos XAML
        private bool _estaCargado = false;

        public PrestamosPage()
        {
            this.InitializeComponent();
            _jsonService = new JsonService();
            this.Loaded += PrestamosPage_Loaded;
        }

        private async void PrestamosPage_Loaded(object sender, RoutedEventArgs e)
        {
            _estaCargado = true;
            if (CmbFiltro != null && CmbFiltro.SelectedIndex < 0)
            {
                CmbFiltro.SelectedIndex = 0; // "Todos"
            }
            await CargarDatosAsync();
        }

        // Carga asíncrona de los 3 archivos JSON y preparación de la interfaz
        private async Task CargarDatosAsync()
        {
            try
            {
                TxtTotalResultados.Text = "Cargando información...";
                _libros = await _jsonService.LeerListaAsync<Libro>("libros.json");
                _lectores = await _jsonService.LeerListaAsync<Lector>("lectores.json");
                _prestamos = await _jsonService.LeerListaAsync<Prestamo>("prestamos.json");

                // Validar que ningún libro contenga cantidad de ejemplares <= 0 en el JSON
                var libroInvalido = _libros.FirstOrDefault(l => l.CantidadEjemplares <= 0);
                if (libroInvalido != null)
                {
                    throw new InvalidOperationException($"El libro \"{libroInvalido.Titulo}\" (ID: {libroInvalido.Id}) tiene una cantidad de ejemplares inválida ({libroInvalido.CantidadEjemplares}). Debe ser mayor que cero.");
                }

                // Configurar fecha de préstamo como hoy y fecha de vencimiento inicial (+7 días)
                TxtFechaPrestamo.Text = DateTime.Today.ToString("dd/MM/yyyy");
                DpkFechaVencimiento.Date = DateTimeOffset.Now.AddDays(7);

                PoblarComboLectores();
                PoblarComboLibros();

                ConstruirListaVista();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al cargar los registros: {ex.Message}", false);
                TxtTotalResultados.Text = "No se pudieron cargar los datos.";
                ListaPrestamos.ItemsSource = null;
            }
        }

        // Llena el ComboBox de lectores con todos los lectores registrados
        private void PoblarComboLectores()
        {
            var items = _lectores.Select(l => new LectorComboItem
            {
                Id = l.Id,
                Nombre = l.Nombre,
                Matricula = l.Matricula
            }).OrderBy(l => l.Nombre).ToList();

            CmbLectores.ItemsSource = items;
            CmbLectores.SelectedItem = null;
        }

        // Llena el ComboBox de libros únicamente con títulos que posean ejemplares disponibles
        private void PoblarComboLibros()
        {
            var items = new List<LibroComboItem>();
            foreach (var libro in _libros)
            {
                int prestamosActivos = _prestamos.Count(p => p.LibroId == libro.Id && p.FechaDevolucion == null);
                int disponibles = libro.CantidadEjemplares - prestamosActivos;

                // Solo mostrar títulos con existencias disponibles (> 0) y sin inconsistencias
                if (disponibles > 0 && prestamosActivos < libro.CantidadEjemplares)
                {
                    items.Add(new LibroComboItem
                    {
                        Id = libro.Id,
                        Titulo = libro.Titulo,
                        EjemplaresDisponibles = disponibles
                    });
                }
            }

            CmbLibros.ItemsSource = items.OrderBy(b => b.Titulo).ToList();
            CmbLibros.SelectedItem = null;
        }

        // Crea una copia profunda independiente de la lista de préstamos para guardado atómico
        private List<Prestamo> ClonarPrestamos(List<Prestamo> origen)
        {
            return origen.Select(p => new Prestamo
            {
                Id = p.Id,
                LibroId = p.LibroId,
                LectorId = p.LectorId,
                FechaPrestamo = p.FechaPrestamo,
                FechaVencimiento = p.FechaVencimiento,
                FechaDevolucion = p.FechaDevolucion
            }).ToList();
        }

        // Convierte los registros de préstamo a elementos de vista con títulos, nombres y estados calculados
        private void ConstruirListaVista()
        {
            DateTime hoy = DateTime.Today;
            var librosPorId = _libros.ToDictionary(l => l.Id);
            var lectoresPorId = _lectores.ToDictionary(l => l.Id);

            _prestamosVista = _prestamos.Select(p =>
            {
                string tituloLibro = librosPorId.TryGetValue(p.LibroId, out var libro)
                    ? libro.Titulo
                    : $"(Libro ID {p.LibroId} no encontrado)";

                string nombreLector, matriculaLector;
                if (lectoresPorId.TryGetValue(p.LectorId, out var lector))
                {
                    nombreLector = lector.Nombre;
                    matriculaLector = lector.Matricula;
                }
                else
                {
                    nombreLector = $"(Lector ID {p.LectorId} no encontrado)";
                    matriculaLector = "-";
                }

                string estado;
                SolidColorBrush fondo;
                SolidColorBrush texto;

                if (p.FechaDevolucion != null)
                {
                    estado = "Devuelto";
                    fondo = new SolidColorBrush(Color.FromArgb(255, 238, 242, 255));
                    texto = new SolidColorBrush(Color.FromArgb(255, 36, 90, 150));
                }
                else if (p.FechaVencimiento.Date < hoy)
                {
                    estado = "Vencido";
                    fondo = new SolidColorBrush(Color.FromArgb(255, 254, 226, 226));
                    texto = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                }
                else
                {
                    estado = "Pendiente";
                    fondo = new SolidColorBrush(Color.FromArgb(255, 236, 253, 245));
                    texto = new SolidColorBrush(Color.FromArgb(255, 21, 128, 61));
                }

                return new PrestamoItemVista
                {
                    Id = p.Id,
                    LibroId = p.LibroId,
                    LectorId = p.LectorId,
                    TituloLibro = tituloLibro,
                    NombreLector = nombreLector,
                    MatriculaLector = matriculaLector,
                    FechaPrestamo = p.FechaPrestamo,
                    FechaVencimiento = p.FechaVencimiento,
                    FechaDevolucion = p.FechaDevolucion,
                    Estado = estado,
                    ColorFondoEstado = fondo,
                    ColorTextoEstado = texto
                };
            }).OrderByDescending(p => p.Id).ToList();
        }

        // Aplica el filtro seleccionado (Todos, Activos, Vencidos, Devueltos) a la tabla
        private void AplicarFiltros()
        {
            if (!_estaCargado || ListaPrestamos == null) return;

            string filtro = (CmbFiltro?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";

            IEnumerable<PrestamoItemVista> consulta = _prestamosVista;

            if (filtro == "Activos")
            {
                consulta = consulta.Where(p => p.EsActivo);
            }
            else if (filtro == "Vencidos")
            {
                consulta = consulta.Where(p => p.Estado == "Vencido");
            }
            else if (filtro == "Devueltos")
            {
                consulta = consulta.Where(p => p.Estado == "Devuelto");
            }

            var resultados = consulta.ToList();
            ListaPrestamos.ItemsSource = resultados;
            TxtTotalResultados.Text = $"Mostrando {resultados.Count} de {_prestamosVista.Count} préstamos registrados";

            ListaPrestamos.SelectedItem = null;
            ActualizarBotonDevolucion();
        }

        // Habilita el botón de devolución únicamente si hay un préstamo activo seleccionado
        private void ActualizarBotonDevolucion()
        {
            if (ListaPrestamos?.SelectedItem is PrestamoItemVista item)
            {
                BtnDevolver.IsEnabled = item.EsActivo;
            }
            else
            {
                BtnDevolver.IsEnabled = false;
            }
        }

        // Evento al cambiar la selección en la tabla de préstamos
        private void ListaPrestamos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_estaCargado || ListaPrestamos == null) return;
            ActualizarBotonDevolucion();
        }

        // Evento al cambiar el filtro de estado
        private void CmbFiltro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_estaCargado || ListaPrestamos == null) return;
            AplicarFiltros();
        }

        // ========================================================
        // REGISTRO DE NUEVO PRÉSTAMO
        // ========================================================

        private async void BtnRegistrarPrestamo_Click(object sender, RoutedEventArgs e)
        {
            OcultarErrorFormulario();
            OcultarMensaje();

            // 1. Validar lector seleccionado
            if (CmbLectores.SelectedItem is not LectorComboItem lectorSeleccionado)
            {
                MostrarErrorFormulario("Debe seleccionar un lector de la lista.");
                return;
            }

            var lector = _lectores.FirstOrDefault(l => l.Id == lectorSeleccionado.Id);
            if (lector == null)
            {
                MostrarErrorFormulario("El lector seleccionado no existe en lectores.json.");
                return;
            }

            // 2. Validar libro seleccionado
            if (CmbLibros.SelectedItem is not LibroComboItem libroSeleccionado)
            {
                MostrarErrorFormulario("Debe seleccionar un libro de la lista.");
                return;
            }

            var libro = _libros.FirstOrDefault(l => l.Id == libroSeleccionado.Id);
            if (libro == null)
            {
                MostrarErrorFormulario("El libro seleccionado no existe en libros.json.");
                return;
            }

            // 3. Validar disponibilidad física e inconsistencias de inventario
            int prestamosActivos = _prestamos.Count(p => p.LibroId == libro.Id && p.FechaDevolucion == null);
            if (prestamosActivos > libro.CantidadEjemplares)
            {
                MostrarErrorFormulario($"Inconsistencia de inventario en \"{libro.Titulo}\": existen {prestamosActivos} préstamos activos pero solo {libro.CantidadEjemplares} ejemplar(es) registrado(s). Corrija los datos antes de registrar nuevos préstamos.");
                return;
            }

            if (prestamosActivos >= libro.CantidadEjemplares)
            {
                MostrarErrorFormulario($"El libro \"{libro.Titulo}\" no tiene ejemplares disponibles para préstamo ({prestamosActivos} prestados de {libro.CantidadEjemplares} registrados).");
                return;
            }

            // 4. Validar fecha de vencimiento (igual o posterior a hoy)
            DateTime fechaVencimiento = DpkFechaVencimiento.Date.Date;
            if (fechaVencimiento < DateTime.Today)
            {
                MostrarErrorFormulario("La fecha de vencimiento debe ser igual o posterior a la fecha de préstamo (hoy).");
                return;
            }

            // 5. Generar ID autoincremental
            int nuevoId = _prestamos.Count > 0 ? _prestamos.Max(p => p.Id) + 1 : 1;

            // 6. Crear objeto de préstamo
            var nuevoPrestamo = new Prestamo
            {
                Id = nuevoId,
                LibroId = libro.Id,
                LectorId = lector.Id,
                FechaPrestamo = DateTime.Today,
                FechaVencimiento = fechaVencimiento,
                FechaDevolucion = null
            };

            // 7. Persistencia atómica: crear copia de lista, agregar préstamo y guardar en disco
            List<Prestamo> listaTemporal = ClonarPrestamos(_prestamos);
            listaTemporal.Add(nuevoPrestamo);

            try
            {
                await _jsonService.GuardarListaAsync("prestamos.json", listaTemporal);

                // Únicamente tras el guardado exitoso se reemplaza la lista en memoria
                _prestamos = listaTemporal;

                ConstruirListaVista();
                AplicarFiltros();
                PoblarComboLibros();

                // Limpiar selecciones del formulario
                CmbLectores.SelectedItem = null;
                CmbLibros.SelectedItem = null;
                DpkFechaVencimiento.Date = DateTimeOffset.Now.AddDays(7);
                OcultarErrorFormulario();

                MostrarMensaje($"Préstamo #{nuevoId} registrado correctamente para el libro \"{libro.Titulo}\" y el lector \"{lector.Nombre}\".", true);
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al guardar el préstamo en disco: {ex.Message}", false);
            }
        }

        // ========================================================
        // REGISTRO DE DEVOLUCIÓN
        // ========================================================

        private async void BtnDevolver_Click(object sender, RoutedEventArgs e)
        {
            OcultarMensaje();

            if (ListaPrestamos.SelectedItem is not PrestamoItemVista prestamoSeleccionado)
            {
                MostrarMensaje("Debe seleccionar un préstamo de la tabla para registrar su devolución.", false);
                return;
            }

            if (!prestamoSeleccionado.EsActivo)
            {
                MostrarMensaje("El préstamo seleccionado ya ha sido devuelto anteriormente.", false);
                return;
            }

            List<Prestamo> listaTemporal = ClonarPrestamos(_prestamos);
            var prestamoEnCopia = listaTemporal.FirstOrDefault(p => p.Id == prestamoSeleccionado.Id);
            if (prestamoEnCopia == null)
            {
                MostrarMensaje("No se encontró el préstamo en los registros.", false);
                return;
            }

            if (prestamoEnCopia.FechaDevolucion != null)
            {
                MostrarMensaje("El préstamo ya ha sido devuelto anteriormente.", false);
                return;
            }

            // Asignar fecha y hora actual de devolución
            prestamoEnCopia.FechaDevolucion = DateTime.Now;

            try
            {
                await _jsonService.GuardarListaAsync("prestamos.json", listaTemporal);

                // Únicamente tras el guardado exitoso se reemplaza la lista en memoria
                _prestamos = listaTemporal;

                ConstruirListaVista();
                AplicarFiltros();
                PoblarComboLibros();

                MostrarMensaje($"Devolución del préstamo #{prestamoEnCopia.Id} registrada exitosamente.", true);
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al registrar la devolución en disco: {ex.Message}", false);
            }
        }

        // ========================================================
        // NAVEGACIÓN Y MENSAJES DE INTERFAZ
        // ========================================================

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(DashboardPage));
        }

        private void MostrarMensaje(string mensaje, bool esExito)
        {
            TxtMensajeEstado.Text = mensaje;
            TxtMensajeEstado.Foreground = new SolidColorBrush(esExito ? Colors.Green : Colors.Red);
            TxtMensajeEstado.Visibility = Visibility.Visible;
        }

        private void OcultarMensaje()
        {
            TxtMensajeEstado.Visibility = Visibility.Collapsed;
        }

        private void MostrarErrorFormulario(string mensaje)
        {
            TxtErrorFormulario.Text = mensaje;
            TxtErrorFormulario.Visibility = Visibility.Visible;
        }

        private void OcultarErrorFormulario()
        {
            TxtErrorFormulario.Visibility = Visibility.Collapsed;
        }

        // ========================================================
        // VALIDACIONES DE ENTRADA Y RESTRICCIONES VISUALES
        // ========================================================

        // Evita que el usuario comience con espacios desde el teclado
        private void BloquearEspacioInicial_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                if (sender is TextBox tb && tb.SelectionStart == 0)
                {
                    e.Handled = true;
                }
            }
        }

        // Elimina espacios iniciales al pegar texto, conservando los espacios internos legítimos
        private void BloquearEspacioInicial_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.Text.StartsWith(' '))
            {
                int cursor = tb.SelectionStart;
                string original = tb.Text;
                string trimmed = original.TrimStart(' ');
                int eliminados = original.Length - trimmed.Length;
                tb.Text = trimmed;
                tb.SelectionStart = Math.Max(0, cursor - eliminados);
            }
        }
    }
}
