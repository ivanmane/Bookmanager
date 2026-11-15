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
using BookManager.Models;
using BookManager.Services;

namespace BookManager.Views
{
    public class LibroItemVista
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Autor { get; set; } = string.Empty;
        public string ISBN { get; set; } = string.Empty;
        public string Genero { get; set; } = string.Empty;
        public int AnioPublicacion { get; set; }
        public int TotalEjemplares { get; set; }
        public int EjemplaresDisponibles { get; set; }
        public int EjemplaresPrestados { get; set; }
        public int EjemplaresVencidos { get; set; }
    }

    public sealed partial class CatalogoPage : Page
    {
        private readonly JsonService _jsonService;
        private List<Libro> _libros = new();
        private List<Prestamo> _prestamos = new();
        private List<LibroItemVista> _librosVista = new();

        private int? _libroEnEdicionId = null;
        private string _filtroInicial = "Todos";
        private bool _estaCargado = false;

        public CatalogoPage()
        {
            this.InitializeComponent();
            _jsonService = new JsonService();
            this.Loaded += CatalogoPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string filtro && !string.IsNullOrWhiteSpace(filtro))
            {
                _filtroInicial = filtro;
            }
        }

        private async void CatalogoPage_Loaded(object sender, RoutedEventArgs e)
        {
            _estaCargado = true;
            SeleccionarFiltroInicial();
            await CargarDatosAsync();
        }

        private void SeleccionarFiltroInicial()
        {
            if (CmbFiltro == null) return;

            foreach (var item in CmbFiltro.Items)
            {
                if (item is ComboBoxItem comboItem && comboItem.Content?.ToString() == _filtroInicial)
                {
                    CmbFiltro.SelectedItem = comboItem;
                    return;
                }
            }

            if (CmbFiltro.SelectedIndex < 0 && CmbFiltro.Items.Count > 0)
            {
                CmbFiltro.SelectedIndex = 0;
            }
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                TxtTotalResultados.Text = "Cargando datos...";
                _libros = await _jsonService.LeerListaAsync<Libro>("libros.json");
                _prestamos = await _jsonService.LeerListaAsync<Prestamo>("prestamos.json");

                var libroInvalido = _libros.FirstOrDefault(l => l.CantidadEjemplares <= 0);
                if (libroInvalido != null)
                {
                    throw new InvalidOperationException($"El libro \"{libroInvalido.Titulo}\" (ID: {libroInvalido.Id}) tiene una cantidad de ejemplares inválida ({libroInvalido.CantidadEjemplares}). Debe ser mayor que cero.");
                }

                ConstruirListaVista();
                AplicarBusquedaYFiltros();
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al cargar los datos del catálogo: {ex.Message}", false);
                TxtTotalResultados.Text = "No se pudieron cargar los libros.";
                ListaLibros.ItemsSource = null;
            }
        }

        private void ConstruirListaVista()
        {
            DateTime hoy = DateTime.Today;

            _librosVista = _libros.Select(libro =>
            {
                int total = libro.CantidadEjemplares;

                var prestamosActivos = _prestamos
                    .Where(p => p.LibroId == libro.Id && p.FechaDevolucion == null)
                    .ToList();

                int prestados = prestamosActivos.Count;
                int vencidos = prestamosActivos.Count(p => p.FechaVencimiento.Date < hoy);
                int disponibles = Math.Max(0, total - prestados);

                return new LibroItemVista
                {
                    Id = libro.Id,
                    Titulo = libro.Titulo,
                    Autor = libro.Autor,
                    ISBN = libro.ISBN,
                    Genero = libro.Genero,
                    AnioPublicacion = libro.AnioPublicacion,
                    TotalEjemplares = total,
                    EjemplaresDisponibles = disponibles,
                    EjemplaresPrestados = prestados,
                    EjemplaresVencidos = vencidos
                };
            }).ToList();
        }

        private List<Libro> ClonarLibros(List<Libro> origen)
        {
            return origen.Select(l => new Libro
            {
                Id = l.Id,
                Titulo = l.Titulo,
                Autor = l.Autor,
                ISBN = l.ISBN,
                Genero = l.Genero,
                AnioPublicacion = l.AnioPublicacion,
                CantidadEjemplares = l.CantidadEjemplares
            }).ToList();
        }

        private void AplicarBusquedaYFiltros()
        {
            if (!_estaCargado || ListaLibros == null)
            {
                return;
            }

            string busqueda = TxtBuscar?.Text?.Trim() ?? string.Empty;
            string filtro = (CmbFiltro?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";

            IEnumerable<LibroItemVista> consulta = _librosVista;

            if (filtro == "Disponibles")
            {
                consulta = consulta.Where(l => l.EjemplaresDisponibles > 0);
            }
            else if (filtro == "Prestados")
            {
                consulta = consulta.Where(l => l.EjemplaresPrestados > 0);
            }
            else if (filtro == "Vencidos")
            {
                consulta = consulta.Where(l => l.EjemplaresVencidos > 0);
            }

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                consulta = consulta.Where(l =>
                    l.Titulo.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    l.Autor.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    l.ISBN.Contains(busqueda, StringComparison.OrdinalIgnoreCase));
            }

            var resultados = consulta.ToList();
            ListaLibros.ItemsSource = resultados;

            if (TxtTotalResultados != null)
            {
                int totalCopias = _librosVista.Sum(l => l.TotalEjemplares);
                TxtTotalResultados.Text = $"Mostrando {resultados.Count} de {_librosVista.Count} título(s) ({totalCopias} ejemplar(es) físico(s) en total).";
            }

            ListaLibros.SelectedItem = null;
            ActualizarBotonesSeleccion();
        }

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_estaCargado) return;

            if (sender is TextBox tb && tb.Text.StartsWith(' '))
            {
                int cursor = tb.SelectionStart;
                string original = tb.Text;
                string trimmed = original.TrimStart(' ');
                int eliminados = original.Length - trimmed.Length;
                tb.Text = trimmed;
                tb.SelectionStart = Math.Max(0, cursor - eliminados);
                return;
            }

            AplicarBusquedaYFiltros();
        }

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

        private void TxtAnio_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Back ||
                e.Key == VirtualKey.Delete ||
                e.Key == VirtualKey.Tab ||
                e.Key == VirtualKey.Left ||
                e.Key == VirtualKey.Right ||
                e.Key == VirtualKey.Home ||
                e.Key == VirtualKey.End)
            {
                return;
            }

            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
                return;
            }

            bool esNumeroSuperior = e.Key >= VirtualKey.Number0 && e.Key <= VirtualKey.Number9;
            bool esNumpad = e.Key >= VirtualKey.NumberPad0 && e.Key <= VirtualKey.NumberPad9;

            if (!esNumeroSuperior && !esNumpad)
            {
                e.Handled = true;
            }
        }

        private void TxtAnio_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;

            string soloDigitos = new string(tb.Text.Where(c => c >= '0' && c <= '9').ToArray());

            if (soloDigitos.Length > 4)
            {
                soloDigitos = soloDigitos.Substring(0, 4);
            }

            if (tb.Text != soloDigitos)
            {
                int cursor = tb.SelectionStart;
                tb.Text = soloDigitos;
                tb.SelectionStart = Math.Min(cursor, soloDigitos.Length);
            }
        }

        private void TxtEjemplares_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Back ||
                e.Key == VirtualKey.Delete ||
                e.Key == VirtualKey.Tab ||
                e.Key == VirtualKey.Left ||
                e.Key == VirtualKey.Right ||
                e.Key == VirtualKey.Home ||
                e.Key == VirtualKey.End)
            {
                return;
            }

            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
                return;
            }

            bool esNumeroSuperior = e.Key >= VirtualKey.Number0 && e.Key <= VirtualKey.Number9;
            bool esNumpad = e.Key >= VirtualKey.NumberPad0 && e.Key <= VirtualKey.NumberPad9;

            if (!esNumeroSuperior && !esNumpad)
            {
                e.Handled = true;
            }
        }

        private void TxtEjemplares_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;

            string soloDigitos = new string(tb.Text.Where(c => c >= '0' && c <= '9').ToArray());

            if (soloDigitos.Length > 4)
            {
                soloDigitos = soloDigitos.Substring(0, 4);
            }

            if (tb.Text != soloDigitos)
            {
                int cursor = tb.SelectionStart;
                tb.Text = soloDigitos;
                tb.SelectionStart = Math.Min(cursor, soloDigitos.Length);
            }
        }

        private void CmbFiltro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_estaCargado) return;
            AplicarBusquedaYFiltros();
        }

        private void ListaLibros_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarBotonesSeleccion();
        }

        private void ActualizarBotonesSeleccion()
        {
            if (ListaLibros == null || BtnEditar == null || BtnEliminar == null)
            {
                return;
            }

            bool haySeleccion = ListaLibros.SelectedItem != null;
            BtnEditar.IsEnabled = haySeleccion;
            BtnEliminar.IsEnabled = haySeleccion;
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            _libroEnEdicionId = null;
            TxtTituloFormulario.Text = "Registrar nuevo libro";
            LimpiarFormulario();
            TxtEjemplares.Text = "1";
            PanelFormulario.Visibility = Visibility.Visible;
            TxtTitulo.Focus(FocusState.Programmatic);
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (ListaLibros.SelectedItem is not LibroItemVista item)
            {
                return;
            }

            _libroEnEdicionId = item.Id;
            TxtTituloFormulario.Text = $"Editar libro (ID: {item.Id})";

            TxtTitulo.Text = item.Titulo;
            TxtAutor.Text = item.Autor;
            TxtISBN.Text = item.ISBN;
            TxtGenero.Text = item.Genero;
            TxtAnio.Text = item.AnioPublicacion.ToString();
            TxtEjemplares.Text = item.TotalEjemplares.ToString();

            TxtErrorFormulario.Visibility = Visibility.Collapsed;
            PanelFormulario.Visibility = Visibility.Visible;
            TxtTitulo.Focus(FocusState.Programmatic);
        }

        private void BtnCancelarFormulario_Click(object sender, RoutedEventArgs e)
        {
            PanelFormulario.Visibility = Visibility.Collapsed;
            LimpiarFormulario();
        }

        private void LimpiarFormulario()
        {
            TxtTitulo.Text = string.Empty;
            TxtAutor.Text = string.Empty;
            TxtISBN.Text = string.Empty;
            TxtGenero.Text = string.Empty;
            TxtAnio.Text = string.Empty;
            TxtEjemplares.Text = "1";
            TxtErrorFormulario.Visibility = Visibility.Collapsed;
        }

        private async void BtnGuardarLibro_Click(object sender, RoutedEventArgs e)
        {
            string titulo = TxtTitulo.Text.Trim();
            string autor = TxtAutor.Text.Trim();
            string isbn = TxtISBN.Text.Trim();
            string genero = TxtGenero.Text.Trim();
            string anioTexto = TxtAnio.Text.Trim();
            string ejemplaresTexto = TxtEjemplares.Text.Trim();

            if (string.IsNullOrWhiteSpace(titulo))
            {
                MostrarErrorFormulario("El título es obligatorio.");
                return;
            }

            if (string.IsNullOrWhiteSpace(autor))
            {
                MostrarErrorFormulario("El autor es obligatorio.");
                return;
            }

            if (!int.TryParse(anioTexto, out int anio) || anio <= 0 || anio > DateTime.Now.Year)
            {
                MostrarErrorFormulario($"El año de publicación debe ser un número mayor a 0 y no superior al año actual ({DateTime.Now.Year}).");
                return;
            }

            if (!int.TryParse(ejemplaresTexto, out int ejemplares) || ejemplares <= 0)
            {
                MostrarErrorFormulario("La cantidad de ejemplares debe ser un número entero mayor que cero.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(isbn))
            {
                int idActual = _libroEnEdicionId ?? 0;
                bool isbnDuplicado = _libros.Any(l =>
                    l.Id != idActual &&
                    !string.IsNullOrWhiteSpace(l.ISBN) &&
                    string.Equals(l.ISBN.Trim(), isbn, StringComparison.OrdinalIgnoreCase));

                if (isbnDuplicado)
                {
                    MostrarErrorFormulario($"Ya existe otro libro registrado con el ISBN '{isbn}'.");
                    return;
                }
            }

            if (_libroEnEdicionId != null)
            {
                int prestamosActivos = _prestamos.Count(p => p.LibroId == _libroEnEdicionId.Value && p.FechaDevolucion == null);
                if (ejemplares < prestamosActivos)
                {
                    MostrarErrorFormulario($"No se puede reducir la cantidad a {ejemplares} ejemplar(es) porque este libro tiene actualmente {prestamosActivos} préstamo(s) activo(s).");
                    return;
                }
            }

            try
            {
                var listaTemporal = ClonarLibros(_libros);

                string mensajeExito;

                if (_libroEnEdicionId == null)
                {
                    int nuevoId = listaTemporal.Count > 0 ? listaTemporal.Max(l => l.Id) + 1 : 1;

                    var nuevoLibro = new Libro
                    {
                        Id = nuevoId,
                        Titulo = titulo,
                        Autor = autor,
                        ISBN = isbn,
                        Genero = genero,
                        AnioPublicacion = anio,
                        CantidadEjemplares = ejemplares
                    };

                    listaTemporal.Add(nuevoLibro);
                    mensajeExito = $"Libro \"{titulo}\" registrado exitosamente con ID {nuevoId} ({ejemplares} ejemplar(es)).";
                }
                else
                {
                    var libroEnTemporal = listaTemporal.FirstOrDefault(l => l.Id == _libroEnEdicionId.Value);
                    if (libroEnTemporal == null)
                    {
                        MostrarErrorFormulario("No se encontró el libro a editar en la lista.");
                        return;
                    }

                    libroEnTemporal.Titulo = titulo;
                    libroEnTemporal.Autor = autor;
                    libroEnTemporal.ISBN = isbn;
                    libroEnTemporal.Genero = genero;
                    libroEnTemporal.AnioPublicacion = anio;
                    libroEnTemporal.CantidadEjemplares = ejemplares;

                    mensajeExito = $"Libro \"{titulo}\" (ID: {libroEnTemporal.Id}) actualizado exitosamente ({ejemplares} ejemplar(es)).";
                }

                await _jsonService.GuardarListaAsync("libros.json", listaTemporal);

                _libros = listaTemporal;

                PanelFormulario.Visibility = Visibility.Collapsed;
                LimpiarFormulario();
                MostrarMensaje(mensajeExito, true);
                ConstruirListaVista();
                AplicarBusquedaYFiltros();
            }
            catch (Exception ex)
            {
                MostrarErrorFormulario($"Error al guardar en el archivo JSON: {ex.Message}. Los cambios no se aplicaron.");
            }
        }

        private void MostrarErrorFormulario(string mensaje)
        {
            TxtErrorFormulario.Text = mensaje;
            TxtErrorFormulario.Visibility = Visibility.Visible;
        }

        private async void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (ListaLibros.SelectedItem is not LibroItemVista itemSeleccionado)
            {
                return;
            }

            bool tieneHistorialPrestamos = _prestamos.Any(p => p.LibroId == itemSeleccionado.Id);
            if (tieneHistorialPrestamos)
            {
                MostrarMensaje($"No se puede eliminar el libro \"{itemSeleccionado.Titulo}\" porque tiene registros asociados en el historial de préstamos.", false);
                return;
            }

            var dialogo = new ContentDialog
            {
                Title = "Confirmar eliminación",
                Content = $"¿Está seguro de que desea eliminar el libro \"{itemSeleccionado.Titulo}\" (ID: {itemSeleccionado.Id})?",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot ?? this.Content.XamlRoot
            };

            var resultado = await dialogo.ShowAsync();
            if (resultado == ContentDialogResult.Primary)
            {
                try
                {
                    var listaTemporal = ClonarLibros(_libros);

                    listaTemporal.RemoveAll(l => l.Id == itemSeleccionado.Id);

                    await _jsonService.GuardarListaAsync("libros.json", listaTemporal);

                    _libros = listaTemporal;

                    MostrarMensaje($"El libro \"{itemSeleccionado.Titulo}\" fue eliminado exitosamente.", true);
                    ConstruirListaVista();
                    AplicarBusquedaYFiltros();
                }
                catch (Exception ex)
                {
                    MostrarMensaje($"Error al eliminar el libro en el archivo JSON: {ex.Message}. No se realizó ningún cambio.", false);
                }
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(DashboardPage));
        }

        private void MostrarMensaje(string mensaje, bool esExito)
        {
            TxtMensajeEstado.Text = mensaje;
            TxtMensajeEstado.Foreground = esExito
                ? new SolidColorBrush(Colors.Green)
                : new SolidColorBrush(Colors.Red);
            TxtMensajeEstado.Visibility = Visibility.Visible;
        }
    }
}
