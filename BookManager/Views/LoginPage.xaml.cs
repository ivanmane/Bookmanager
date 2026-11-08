using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace BookManager.Views
{
    public sealed partial class LoginPage : Page
    {
        private const string UsuarioValido = "admin";
        private const string PasswordValido = "1234";

        public LoginPage()
        {
            this.InitializeComponent();
        }

        // Restringe que el usuario comience con espacios desde el teclado
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

        // Controla que no queden espacios iniciales al pegar texto o modificar el campo
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

        // Bloquea espacio inicial en el PasswordBox si está vacío
        private void Password_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                if (sender is PasswordBox pb && string.IsNullOrEmpty(pb.Password))
                {
                    e.Handled = true;
                }
            }
        }

        // Elimina espacios al inicio si se pegaron en el PasswordBox
        private void Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb && pb.Password.StartsWith(' '))
            {
                pb.Password = pb.Password.TrimStart(' ');
            }
        }

        // Al presionar el boton de ingresar
        private void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = TxtUsuario.Text.Trim();
            string password = TxtPassword.Password;

            // Aqui validamos que los campos no esten vacios, en caso de estar vacios se borran xd. Osea los espacios no el texto.
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                MostrarMensaje("Por favor, complete tanto el usuario como la contraseña.", false);
                return;
            }

            // Comparar datos ingresados con las variables creadas antes
            if (usuario == UsuarioValido && password == PasswordValido)
            {
                // Navegar hacia el Dashboard principal de la biblioteca
                this.Frame.Navigate(typeof(DashboardPage));
            }
            else
            {
                MostrarMensaje("Usuario o contraseña incorrectos.", false);
            }
        }

        // Cambiar el mensaje al presionar el botor, verde si es correcto y rojo cuando es incorrecto
        private void MostrarMensaje(string mensaje, bool esExito)
        {
            TxtMensaje.Text = mensaje;
            TxtMensaje.Foreground = esExito
                ? new SolidColorBrush(Colors.Green)
                : new SolidColorBrush(Colors.Red);
        }
    }
}
