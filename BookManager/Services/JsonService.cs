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

        public string ObtenerRutaCarpetaData()
        {
            if (!Directory.Exists(_carpetaData))
            {
                Directory.CreateDirectory(_carpetaData);
            }

            return _carpetaData;
        }

        private string ObtenerRutaCompleta(string nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                throw new ArgumentException("El nombre del archivo no puede estar vacío.", nameof(nombreArchivo));
            }

            return Path.Combine(ObtenerRutaCarpetaData(), nombreArchivo);
        }

        public async Task<List<T>> LeerListaAsync<T>(string nombreArchivo)
        {
            string rutaArchivo = ObtenerRutaCompleta(nombreArchivo);

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
                throw new InvalidOperationException($"El archivo '{nombreArchivo}' contiene un formato JSON inválido o dañado.", ex);
            }
        }

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
