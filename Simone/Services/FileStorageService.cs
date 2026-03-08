using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Simone.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  CONFIGURACIÓN (leída desde appsettings.json → "Uploads")
    // ═══════════════════════════════════════════════════════════════════════

    public class UploadsOptions
    {
        /// <summary>
        /// Ruta física absoluta fuera del proyecto donde se guardan todos los archivos
        /// subidos por usuarios. Ej: "C:\\Simone_Uploads" o "/var/simone/uploads"
        /// </summary>
        public string BasePath { get; set; } = string.Empty;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  INTERFAZ
    // ═══════════════════════════════════════════════════════════════════════

    public interface IFileStorageService
    {
        /// <summary>
        /// Guarda un archivo subido y devuelve la URL relativa para guardar en BD.
        /// Ejemplo de retorno: "/images/Productos/5/abc123.jpg"
        /// </summary>
        Task<string> GuardarArchivoAsync(
            IFormFile archivo,
            string    subcarpeta,           // "images/Productos/5", "images/Perfiles", etc.
            string?   nombreBase = null,    // null = GUID aleatorio
            long      maxBytes   = 5 * 1024 * 1024,
            string[]? extensionesPermitidas = null);

        /// <summary>
        /// Elimina un archivo dado su URL relativa (ej: "/images/Perfiles/abc.jpg").
        /// Si no existe, no lanza excepción.
        /// </summary>
        void EliminarArchivo(string? urlRelativa);

        /// <summary>
        /// Convierte una URL relativa en la ruta física absoluta del archivo.
        /// </summary>
        string ObtenerRutaFisica(string urlRelativa);

        /// <summary>
        /// Directorio raíz donde se almacenan todos los archivos subidos.
        /// </summary>
        string RutaBase { get; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  IMPLEMENTACIÓN
    // ═══════════════════════════════════════════════════════════════════════

    public class FileStorageService : IFileStorageService
    {
        private readonly string _basePath;

        private static readonly HashSet<string> _imgExtensionesDefault = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif", ".bmp"
        };

        private static readonly HashSet<string> _docExtensiones = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf"
        };

        public string RutaBase => _basePath;

        public FileStorageService(IOptions<UploadsOptions> opts, IWebHostEnvironment env)
        {
            var configuredPath = opts.Value.BasePath;

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                _basePath = configuredPath;
            }
            else
            {
                // Fallback seguro: carpeta hermana del proyecto (fuera de wwwroot)
                // Ejemplo: si el proyecto está en C:\proyectos\Simone\
                //          se usa    C:\proyectos\Simone_Uploads\
                var contentRoot = env.ContentRootPath.TrimEnd(Path.DirectorySeparatorChar);
                _basePath = contentRoot + "_Uploads";
            }

            // Crear la carpeta base si no existe
            Directory.CreateDirectory(_basePath);
        }

        /// <inheritdoc/>
        public async Task<string> GuardarArchivoAsync(
            IFormFile archivo,
            string    subcarpeta,
            string?   nombreBase = null,
            long      maxBytes   = 5 * 1024 * 1024,
            string[]? extensionesPermitidas = null)
        {
            // ── Validación de tamaño ────────────────────────────────────
            if (archivo.Length > maxBytes)
                throw new InvalidOperationException(
                    $"El archivo supera el límite de {maxBytes / 1024 / 1024} MB.");

            // ── Validación de extensión ─────────────────────────────────
            var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                throw new InvalidOperationException("El archivo no tiene extensión.");

            var permitidas = extensionesPermitidas != null
                ? new HashSet<string>(extensionesPermitidas, StringComparer.OrdinalIgnoreCase)
                : _imgExtensionesDefault;

            if (!permitidas.Contains(ext))
                throw new InvalidOperationException(
                    $"Extensión '{ext}' no permitida. Permitidas: {string.Join(", ", permitidas)}");

            // ── Validación de contenido (magic bytes) ───────────────────
            if (!await LooksLikeImageOrPdfAsync(archivo))
                throw new InvalidOperationException(
                    "El contenido del archivo no corresponde a una imagen o PDF válido.");

            // ── Construir ruta física ────────────────────────────────────
            // Normalizar subcarpeta: "images/Productos/5" → sin barras iniciales
            var subNorm   = subcarpeta.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var carpeta   = Path.Combine(_basePath, subNorm);
            Directory.CreateDirectory(carpeta);

            // Nombre de archivo: usar nombreBase si se pasó, o GUID nuevo
            var fileName  = (nombreBase != null ? nombreBase : Guid.NewGuid().ToString("N")) + ext;
            var rutaFisica = Path.Combine(carpeta, fileName);

            // ── Guardar ─────────────────────────────────────────────────
            await using var stream = new FileStream(rutaFisica, FileMode.Create, FileAccess.Write);
            await archivo.CopyToAsync(stream);

            // ── Devolver URL relativa (con barras estilo web) ────────────
            // Ejemplo: "images/Productos/5/abc123.jpg" → "/images/Productos/5/abc123.jpg"
            var urlRelativa = "/" + subNorm.Replace(Path.DirectorySeparatorChar, '/') + "/" + fileName;
            return urlRelativa;
        }

        /// <inheritdoc/>
        public void EliminarArchivo(string? urlRelativa)
        {
            if (string.IsNullOrWhiteSpace(urlRelativa)) return;

            var rutaFisica = ObtenerRutaFisica(urlRelativa);
            if (File.Exists(rutaFisica))
                File.Delete(rutaFisica);
        }

        /// <inheritdoc/>
        public string ObtenerRutaFisica(string urlRelativa)
        {
            // "/images/Productos/5/abc.jpg" → "{BasePath}/images/Productos/5/abc.jpg"
            var rel = urlRelativa.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_basePath, rel);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static async Task<bool> LooksLikeImageOrPdfAsync(IFormFile archivo)
        {
            var buf = new byte[8];
            await using var s = archivo.OpenReadStream();
            var read = await s.ReadAsync(buf, 0, buf.Length);
            if (read < 4) return false;

            // JPEG: FF D8 FF
            if (buf[0] == 0xFF && buf[1] == 0xD8 && buf[2] == 0xFF) return true;
            // PNG: 89 50 4E 47
            if (buf[0] == 0x89 && buf[1] == 0x50 && buf[2] == 0x4E && buf[3] == 0x47) return true;
            // GIF: 47 49 46 38
            if (buf[0] == 0x47 && buf[1] == 0x49 && buf[2] == 0x46 && buf[3] == 0x38) return true;
            // WEBP: 52 49 46 46 ... 57 45 42 50
            if (buf[0] == 0x52 && buf[1] == 0x49 && buf[2] == 0x46 && buf[3] == 0x46) return true;
            // PDF: 25 50 44 46
            if (buf[0] == 0x25 && buf[1] == 0x50 && buf[2] == 0x44 && buf[3] == 0x46) return true;
            // BMP: 42 4D
            if (buf[0] == 0x42 && buf[1] == 0x4D) return true;
            // AVIF/MP4-container: starts with ftyp at offset 4
            if (read >= 8 && buf[4] == 0x66 && buf[5] == 0x74 && buf[6] == 0x79 && buf[7] == 0x70) return true;

            return false;
        }
    }
}
