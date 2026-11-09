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

        // PreviewKeyDown sirve para que no se puedan poner espacios
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

        // Bloqueo de los espacios pero para el textbox de la contraseña
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

        private void Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb && pb.Password.StartsWith(' '))
            {
                pb.Password = pb.Password.TrimStart(' ');
            }
        }

        // Toda la logica del boton ingresar 
        // e se refiere al evento
        // Trim elimina los espacios antes y despues del texto que se ingresa
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
                // Navigate te envia a otra pantalla, en este caso a Dashboard
                this.Frame.Navigate(typeof(DashboardPage));
            }
            else
            {
                MostrarMensaje("Usuario o contraseña incorrectos.", false);
            }
        }

        // Cambiar el mensaje al presionar el botor, verde si es correcto y rojo cuando es incorrecto
        // ? y : sirven como una forma mas sencilla de un if else. Por ejemplo aqui que si mensaje es true verde, false red. La estructura es:
        // condicion ? verdadero : falso
        private void MostrarMensaje(string mensaje, bool esExito)
        {
            TxtMensaje.Text = mensaje;
            TxtMensaje.Foreground = esExito
                ? new SolidColorBrush(Colors.Green)
                : new SolidColorBrush(Colors.Red);
        }
    }
}
