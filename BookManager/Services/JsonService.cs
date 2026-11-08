using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace BookManager.Services
{
    public class JsonService
    {
        private readonly JsonSerializerOptions _opcionesLectura;
        private readonly JsonSerializerOptions _opcionesEscritura;

        // Ruta resuelta dinámicamente hacia la carpeta Data dentro del proyecto BookManager
        private readonly string _carpetaData;

        public JsonService()
        {
            _opcionesLectura = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _opcionesEscritura = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            _carpetaData = ResolverRutaCarpetaData();
        }

        // Resuelve dinámicamente la carpeta Data buscando la raíz del proyecto (donde reside BookManager.csproj)
        public static string ResolverRutaCarpetaData()
        {
            string[] puntosDePartida = {
                AppContext.BaseDirectory,
                Environment.CurrentDirectory
            };

            foreach (string punto in puntosDePartida)
            {
                if (string.IsNullOrWhiteSpace(punto)) continue;

                string? dirActual = Path.GetFullPath(punto);
                while (!string.IsNullOrEmpty(dirActual))
                {
                    string archivoCsproj = Path.Combine(dirActual, "BookManager.csproj");
                    if (File.Exists(archivoCsproj))
                    {
                        // Se identificó el directorio raíz del proyecto BookManager
                        string rutaData = Path.Combine(dirActual, "Data");
                        if (!Directory.Exists(rutaData))
                        {
                            Directory.CreateDirectory(rutaData);
                        }
                        return rutaData;
                    }

                    DirectoryInfo? padre = Directory.GetParent(dirActual);
                    dirActual = padre?.FullName;
                }
            }

            throw new DirectoryNotFoundException(
                "No se pudo localizar dinámicamente la carpeta Data del proyecto BookManager. " +
                "Se buscó 'BookManager.csproj' recorriendo los directorios superiores a partir de " +
                $"AppContext.BaseDirectory ('{AppContext.BaseDirectory}') y " +
                $"Environment.CurrentDirectory ('{Environment.CurrentDirectory}'). " +
                "Verifique que el proyecto se ejecute dentro de su estructura de carpetas.");
        }

        // Expone la ruta absoluta resuelta para comprobaciones e inspección
        public string ObtenerRutaCarpetaData()
        {
            if (!Directory.Exists(_carpetaData))
            {
                Directory.CreateDirectory(_carpetaData);
            }

            return _carpetaData;
        }

        // Construye la ruta absoluta para un archivo JSON dentro de la carpeta Data resuelta
        private string ObtenerRutaCompleta(string nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                throw new ArgumentException("El nombre del archivo no puede estar vacío.", nameof(nombreArchivo));
            }

            return Path.Combine(ObtenerRutaCarpetaData(), nombreArchivo);
        }

        // Lee una lista de objetos del archivo JSON indicado
        public async Task<List<T>> LeerListaAsync<T>(string nombreArchivo)
        {
            string rutaArchivo = ObtenerRutaCompleta(nombreArchivo);

            // Si el archivo no existe, lo crea inicializado con []
            if (!File.Exists(rutaArchivo))
            {
                await File.WriteAllTextAsync(rutaArchivo, "[]");
                return new List<T>();
            }

            string contenidoJson;
            try
            {
                contenidoJson = await File.ReadAllTextAsync(rutaArchivo);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al intentar leer el archivo '{nombreArchivo}': {ex.Message}", ex);
            }

            if (string.IsNullOrWhiteSpace(contenidoJson))
            {
                return new List<T>();
            }

            try
            {
                List<T>? resultado = JsonSerializer.Deserialize<List<T>>(contenidoJson, _opcionesLectura);
                return resultado ?? new List<T>();
            }
            catch (JsonException ex)
            {
                // No ocultamos el error ni reemplazamos información dañada con una lista vacía
                throw new InvalidOperationException($"El archivo '{nombreArchivo}' contiene un formato JSON inválido o dañado.", ex);
            }
        }

        // Guarda la lista de objetos serializada en formato JSON indentado
        public async Task GuardarListaAsync<T>(string nombreArchivo, List<T> datos)
        {
            if (datos == null)
            {
                throw new ArgumentNullException(nameof(datos), "La lista de datos no puede ser nula.");
            }

            string rutaArchivo = ObtenerRutaCompleta(nombreArchivo);

            try
            {
                string jsonString = JsonSerializer.Serialize(datos, _opcionesEscritura);
                await File.WriteAllTextAsync(rutaArchivo, jsonString);
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al intentar guardar los datos en el archivo '{nombreArchivo}': {ex.Message}", ex);
            }
        }
    }
}
