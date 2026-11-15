using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;
using BookManager.Views;

namespace BookManager
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            ConfigurarBarraDeTitulo();
            ConfigurarIconoVentana();

            RootFrame.Navigated += RootFrame_Navigated;
            RootFrame.Navigate(typeof(LoginPage));
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(LoginPage))
            {
                ChatAsistente.LimpiarSesion();
                ChatAsistente.Visibility = Visibility.Collapsed;
            }
            else
            {
                ChatAsistente.Visibility = Visibility.Visible;
            }
        }

        private void ConfigurarBarraDeTitulo()
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = false;

                Color azulMarino = Color.FromArgb(255, 22, 59, 112);     
                Color azulSecundario = Color.FromArgb(255, 36, 90, 150);  
                Color turquesaAcento = Color.FromArgb(255, 20, 184, 166); 
                Color blanco = Color.FromArgb(255, 255, 255, 255);        
                Color inactivoFondo = Color.FromArgb(255, 30, 68, 120);
                Color inactivoTexto = Color.FromArgb(255, 200, 215, 230);

                titleBar.BackgroundColor = azulMarino;
                titleBar.ForegroundColor = blanco;
                titleBar.InactiveBackgroundColor = inactivoFondo;
                titleBar.InactiveForegroundColor = inactivoTexto;

                titleBar.ButtonBackgroundColor = azulMarino;
                titleBar.ButtonForegroundColor = blanco;
                titleBar.ButtonHoverBackgroundColor = azulSecundario;
                titleBar.ButtonHoverForegroundColor = blanco;
                titleBar.ButtonPressedBackgroundColor = turquesaAcento;
                titleBar.ButtonPressedForegroundColor = blanco;
                titleBar.ButtonInactiveBackgroundColor = inactivoFondo;
                titleBar.ButtonInactiveForegroundColor = inactivoTexto;
            }
        }
        private void ConfigurarIconoVentana()
        {
            try
            {
                string rutaIcono = Path.Combine(AppContext.BaseDirectory, "Assets", "BookManager.ico");
                if (File.Exists(rutaIcono))
                {
                    this.AppWindow.SetIcon(rutaIcono);
                }
            }
            catch
            {
            }
        }
    }
}
