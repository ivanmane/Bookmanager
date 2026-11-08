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

        // Controla si estamos editando un lector existente o agregando uno nuevo
        private int? _lectorEnEdicionId = null;

        // Bandera de inicialización para evitar eventos prematuros
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

        // Carga asíncrona de los archivos JSON de lectores y préstamos
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

        // Genera una copia profunda independiente de los lectores para operaciones CRUD atómicas
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

        // Búsqueda en tiempo real por Nombre, Matrícula, Correo o Teléfono
        private void AplicarBusqueda()
        {
            if (!_estaCargado || ListaLectores == null)
            {
                return;
            }

            string busqueda = TxtBuscar?.Text?.Trim() ?? string.Empty;

            IEnumerable<Lector> consulta = _lectores;

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

            // Eliminar espacios iniciales si fueron introducidos o pegados
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

        // ==========================================
        // AGREGAR Y EDITAR LECTORES
        // ==========================================

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

            // 1. Validación de Nombre: obligatorio, sin números, solo letras y caracteres permitidos
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

            // 2. Validación de Matrícula: obligatoria, solo letras/números/guiones, y única
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

            // 3. Validación de Correo: obligatorio, sin espacios, formato de correo válido
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

            // 4. Validación de Teléfono: obligatorio, exactamente 10 dígitos numéricos del 0 al 9
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
                // 1. Crear una lista temporal con objetos independientes (clonación profunda)
                var listaTemporal = ClonarLectores(_lectores);

                string mensajeExito;

                if (_lectorEnEdicionId == null)
                {
                    // Operación Agregar
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
                    // Operación Editar
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

                // 2. Ejecutar y esperar el guardado en el archivo JSON
                await _jsonService.GuardarListaAsync("lectores.json", listaTemporal);

                // 3. Confirmación atómica: solo si guardó bien en disco, actualizamos la lista en memoria
                _lectores = listaTemporal;

                // 4. Ocultar formulario, notificar éxito y refrescar tabla
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

        // ==========================================
        // ELIMINAR LECTOR
        // ==========================================

        private async void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (ListaLectores.SelectedItem is not Lector itemSeleccionado)
            {
                return;
            }

            // 1. Comprobar si existe algún préstamo asociado a este lector (activo o devuelto)
            bool tieneHistorialPrestamos = _prestamos.Any(p => p.LectorId == itemSeleccionado.Id);
            if (tieneHistorialPrestamos)
            {
                MostrarMensaje($"No se puede eliminar el lector \"{itemSeleccionado.Nombre}\" porque cuenta con registros en el historial de préstamos.", false);
                return;
            }

            // 2. Solicitar confirmación al usuario
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
                    // 1. Clonación independiente
                    var listaTemporal = ClonarLectores(_lectores);

                    // 2. Eliminación sobre la copia temporal
                    listaTemporal.RemoveAll(l => l.Id == itemSeleccionado.Id);

                    // 3. Guardado en disco
                    await _jsonService.GuardarListaAsync("lectores.json", listaTemporal);

                    // 4. Confirmación atómica en memoria
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

        // ==========================================
        // NAVEGACIÓN Y MENSAJES
        // ==========================================

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
