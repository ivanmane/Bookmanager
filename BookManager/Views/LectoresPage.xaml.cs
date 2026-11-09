using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    public sealed partial class LectoresPage : Page
    {
        private readonly JsonService _jsonService;
        private List<Lector> _lectores = new();
        private List<Prestamo> _prestamos = new();

        // Esta variable sirve para saber si estamos editando la info de algun lector
        private int? _lectorEnEdicionId = null;

        private bool _estaCargado = false;

        public LectoresPage()
        {
            this.InitializeComponent();
            _jsonService = new JsonService();
            this.Loaded += LectoresPage_Loaded;
        }

        private async void LectoresPage_Loaded(object sender, RoutedEventArgs e)
        {
            _estaCargado = true;
            await CargarDatosAsync();
        }

        // Carga de todos los archivos
        private async Task CargarDatosAsync()
        {
            try
            {
                TxtTotalResultados.Text = "Cargando lectores...";
                _lectores = await _jsonService.LeerListaAsync<Lector>("lectores.json");
                _prestamos = await _jsonService.LeerListaAsync<Prestamo>("prestamos.json");

                // Compatibilidad hacia atrás: asegurar que registros antiguos sin Telefono tengan cadena vacía
                foreach (var lector in _lectores)
                {
                    lector.Telefono ??= string.Empty;
                }

                AplicarBusqueda();
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al cargar los datos de lectores: {ex.Message}", false);
                TxtTotalResultados.Text = "No se pudieron cargar los lectores.";
                ListaLectores.ItemsSource = null;
            }
        }

        // Crear copia de lectores para casos de fallos, este es el molde que se usa
        private List<Lector> ClonarLectores(List<Lector> origen)
        {
            return origen.Select(l => new Lector
            {
                Id = l.Id,
                Nombre = l.Nombre,
                Matricula = l.Matricula,
                Correo = l.Correo,
                Telefono = l.Telefono ?? string.Empty
            }).ToList();
        }

        // Logica de el cuadro de busqueda
        private void AplicarBusqueda()
        {
            if (!_estaCargado || ListaLectores == null)
            {
                return;
            }

            string busqueda = TxtBuscar?.Text?.Trim() ?? string.Empty;

            IEnumerable<Lector> consulta = _lectores;
            
            //busqueda por nombre, matricula, correo o telefono, el telefono tenemos que convertirlo en string

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                consulta = consulta.Where(l =>
                    l.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    l.Matricula.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    l.Correo.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(l.Telefono) && l.Telefono.Contains(busqueda, StringComparison.OrdinalIgnoreCase)));
            }

            var resultados = consulta.ToList();
            ListaLectores.ItemsSource = resultados;

            if (TxtTotalResultados != null)
            {
                TxtTotalResultados.Text = $"Mostrando {resultados.Count} de {_lectores.Count} lector(es) registrado(s).";
            }

            ListaLectores.SelectedItem = null;
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

            AplicarBusqueda();
        }

        // Aqui estan todas las validaciones y restricciones de la pantalla

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

        private void ListaLectores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarBotonesSeleccion();
        }

        private void ActualizarBotonesSeleccion()
        {
            if (ListaLectores == null || BtnEditar == null || BtnEliminar == null)
            {
                return;
            }

            bool haySeleccion = ListaLectores.SelectedItem != null;
            BtnEditar.IsEnabled = haySeleccion;
            BtnEliminar.IsEnabled = haySeleccion;
        }

        // Logica para agregar y tmb para editar los lectores

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            _lectorEnEdicionId = null;
            TxtTituloFormulario.Text = "Registrar nuevo lector";
            LimpiarFormulario();
            PanelFormulario.Visibility = Visibility.Visible;
            TxtNombre.Focus(FocusState.Programmatic);
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (ListaLectores.SelectedItem is not Lector item)
            {
                return;
            }

            _lectorEnEdicionId = item.Id;
            TxtTituloFormulario.Text = $"Editar lector (ID: {item.Id})";

            TxtNombre.Text = item.Nombre;
            TxtMatricula.Text = item.Matricula;
            TxtCorreo.Text = item.Correo;
            TxtTelefono.Text = item.Telefono ?? string.Empty;

            TxtErrorFormulario.Visibility = Visibility.Collapsed;
            PanelFormulario.Visibility = Visibility.Visible;
            TxtNombre.Focus(FocusState.Programmatic);
        }

        private void BtnCancelarFormulario_Click(object sender, RoutedEventArgs e)
        {
            PanelFormulario.Visibility = Visibility.Collapsed;
            LimpiarFormulario();
        }

        private void LimpiarFormulario()
        {
            TxtNombre.Text = string.Empty;
            TxtMatricula.Text = string.Empty;
            TxtCorreo.Text = string.Empty;
            TxtTelefono.Text = string.Empty;
            TxtErrorFormulario.Visibility = Visibility.Collapsed;
        }

        private async void BtnGuardarLector_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtNombre.Text.Trim();
            string matricula = TxtMatricula.Text.Trim();
            string correo = TxtCorreo.Text.Trim();
            string telefono = TxtTelefono.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarErrorFormulario("El nombre completo es obligatorio.");
                return;
            }

            if (nombre.Any(char.IsDigit))
            {
                MostrarErrorFormulario("El nombre no debe contener números.");
                return;
            }

            if (!Regex.IsMatch(nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s\.\'\-]+$"))
            {
                MostrarErrorFormulario("El nombre contiene caracteres inválidos. Solo se aceptan letras, acentos, espacios, puntos, guiones y apóstrofes.");
                return;
            }

            if (string.IsNullOrWhiteSpace(matricula))
            {
                MostrarErrorFormulario("La matrícula es obligatoria.");
                return;
            }

            if (!Regex.IsMatch(matricula, @"^[a-zA-Z0-9\-]+$"))
            {
                MostrarErrorFormulario("La matrícula solo debe contener letras, números y guiones (sin espacios ni símbolos especiales).");
                return;
            }

            int idActual = _lectorEnEdicionId ?? 0;
            bool matriculaDuplicada = _lectores.Any(l =>
                l.Id != idActual &&
                string.Equals(l.Matricula.Trim(), matricula, StringComparison.OrdinalIgnoreCase));

            if (matriculaDuplicada)
            {
                MostrarErrorFormulario($"Ya existe otro lector registrado con la matrícula '{matricula}'.");
                return;
            }

            if (string.IsNullOrWhiteSpace(correo))
            {
                MostrarErrorFormulario("El correo electrónico es obligatorio.");
                return;
            }

            if (correo.Contains(' '))
            {
                MostrarErrorFormulario("El correo electrónico no debe contener espacios.");
                return;
            }

            if (!Regex.IsMatch(correo, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                MostrarErrorFormulario("El correo electrónico no tiene un formato válido (ejemplo: usuario@dominio.com).");
                return;
            }

            if (string.IsNullOrWhiteSpace(telefono))
            {
                MostrarErrorFormulario("El teléfono es obligatorio.");
                return;
            }

            if (!Regex.IsMatch(telefono, @"^[0-9]{10}$"))
            {
                MostrarErrorFormulario("El teléfono debe contener exactamente 10 dígitos numéricos (ejemplo: 8145899327), sin espacios, guiones ni prefijos.");
                return;
            }

            try
            {
                var listaTemporal = ClonarLectores(_lectores);

                string mensajeExito;

                if (_lectorEnEdicionId == null)
                {
                    // Operacion agregar
                    int nuevoId = listaTemporal.Count > 0 ? listaTemporal.Max(l => l.Id) + 1 : 1;

                    var nuevoLector = new Lector
                    {
                        Id = nuevoId,
                        Nombre = nombre,
                        Matricula = matricula,
                        Correo = correo,
                        Telefono = telefono
                    };

                    listaTemporal.Add(nuevoLector);
                    mensajeExito = $"Lector \"{nombre}\" registrado exitosamente con ID {nuevoId}.";
                }
                else
                {
                    var lectorEnTemporal = listaTemporal.FirstOrDefault(l => l.Id == _lectorEnEdicionId.Value);
                    if (lectorEnTemporal == null)
                    {
                        MostrarErrorFormulario("No se encontró el lector a editar en la lista.");
                        return;
                    }

                    lectorEnTemporal.Nombre = nombre;
                    lectorEnTemporal.Matricula = matricula;
                    lectorEnTemporal.Correo = correo;
                    lectorEnTemporal.Telefono = telefono;

                    mensajeExito = $"Lector \"{nombre}\" (ID: {lectorEnTemporal.Id}) actualizado exitosamente.";
                }

                // Guardar en el lectores json
                await _jsonService.GuardarListaAsync("lectores.json", listaTemporal);

                _lectores = listaTemporal;

                // Ocultamos formulario, mostramos el mensaje de excito y aplicamos busqueda en blanco para actualizar la lista
                PanelFormulario.Visibility = Visibility.Collapsed;
                LimpiarFormulario();
                MostrarMensaje(mensajeExito, true);
                AplicarBusqueda();
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

        // Logica para eliminar lectores

        private async void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (ListaLectores.SelectedItem is not Lector itemSeleccionado)
            {
                return;
            }

            bool tieneHistorialPrestamos = _prestamos.Any(p => p.LectorId == itemSeleccionado.Id);
            if (tieneHistorialPrestamos)
            {
                MostrarMensaje($"No se puede eliminar el lector \"{itemSeleccionado.Nombre}\" porque cuenta con registros en el historial de préstamos.", false);
                return;
            }

            var dialogo = new ContentDialog
            {
                Title = "Confirmar eliminación",
                Content = $"¿Está seguro de que desea eliminar al lector \"{itemSeleccionado.Nombre}\" (Matrícula: {itemSeleccionado.Matricula})?",
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
                    var listaTemporal = ClonarLectores(_lectores);

                    listaTemporal.RemoveAll(l => l.Id == itemSeleccionado.Id);

                    await _jsonService.GuardarListaAsync("lectores.json", listaTemporal);

                    _lectores = listaTemporal;

                    MostrarMensaje($"El lector \"{itemSeleccionado.Nombre}\" fue eliminado exitosamente.", true);
                    AplicarBusqueda();
                }
                catch (Exception ex)
                {
                    MostrarMensaje($"Error al eliminar el lector en el archivo JSON: {ex.Message}. No se realizó ningún cambio.", false);
                }
            }
        }

        // Regresar a la pantalla dashboard y mostrar mensaje de excito

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
