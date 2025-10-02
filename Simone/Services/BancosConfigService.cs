using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simone.Configuration;

namespace Simone.Services
{
    /// <summary>
    /// Servicio de configuración de cuentas bancarias para:
    ///  - Administrador (App_Data/bancos-admin.json)
    ///  - Vendedor (App_Data/bancos-proveedor-{proveedorIdSanitizado}.json)
    /// IO atómico y acceso thread-safe.
    /// </summary>
    public interface IBancosConfigService
    {
        Task<IReadOnlyList<CuentaBancaria>> GetAdminAsync(CancellationToken ct = default);
        Task SetAdminAsync(IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default);

        Task<IReadOnlyList<CuentaBancaria>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default);
        Task SetByProveedorAsync(string proveedorId, IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default);
    }

    public class BancosConfigService : IBancosConfigService
    {
        private readonly string _dataFolder;
        private readonly ILogger<BancosConfigService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Semáforo para operaciones de escritura (async-friendly)
        private static readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        private const string AdminFileName = "bancos-admin.json";
        private const string FileExtension = ".json";
        private const string TempFileExtension = ".tmp";

        public BancosConfigService(IHostEnvironment env, ILogger<BancosConfigService> logger)
        {
            _dataFolder = Path.Combine(env.ContentRootPath, "App_Data");
            _logger = logger;

            // Asegurar que el directorio existe
            try
            {
                Directory.CreateDirectory(_dataFolder);
                _logger.LogInformation("Directorio de datos inicializado: {DataFolder}", _dataFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear el directorio de datos: {DataFolder}", _dataFolder);
                throw;
            }
        }

        private string GetAdminFilePath() => Path.Combine(_dataFolder, AdminFileName);

        // Genera un nombre de archivo seguro para el proveedorId
        private string GetProveedorFilePath(string proveedorId)
        {
            var safeId = SanitizeForFileName(ValidateProveedorId(proveedorId));
            return Path.Combine(_dataFolder, $"bancos-proveedor-{safeId}{FileExtension}");
        }

        // ----------------------------
        // API pública
        // ----------------------------
        public async Task<IReadOnlyList<CuentaBancaria>> GetAdminAsync(CancellationToken ct = default)
        {
            var filePath = GetAdminFilePath();
            var list = await ReadListSafeAsync(filePath, ct).ConfigureAwait(false);
            return list.AsReadOnly();
        }

        public async Task SetAdminAsync(IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default)
        {
            if (cuentas == null)
            {
                _logger.LogWarning("Intento de guardar lista nula de cuentas bancarias para administrador");
                cuentas = new List<CuentaBancaria>();
            }

            var filePath = GetAdminFilePath();
            await WriteListSafeAsync(filePath, cuentas, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<CuentaBancaria>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default)
        {
            var filePath = GetProveedorFilePath(proveedorId);
            var list = await ReadListSafeAsync(filePath, ct).ConfigureAwait(false);
            return list.AsReadOnly();
        }

        public async Task SetByProveedorAsync(string proveedorId, IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default)
        {
            if (cuentas == null)
            {
                _logger.LogWarning("Intento de guardar lista nula de cuentas bancarias para proveedor: {ProveedorId}", proveedorId);
                cuentas = new List<CuentaBancaria>();
            }

            var filePath = GetProveedorFilePath(proveedorId);
            await WriteListSafeAsync(filePath, cuentas, ct).ConfigureAwait(false);
        }

        // ----------------------------
        // Helpers de IO (async + atómico)
        // ----------------------------
        private async Task<List<CuentaBancaria>> ReadListSafeAsync(string filePath, CancellationToken ct)
        {
            try
            {
                // Recuperar archivo temporal si existe y el principal no
                await TryRecoverTemporaryFileAsync(filePath).ConfigureAwait(false);

                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("Archivo no encontrado, devolviendo lista vacía: {FilePath}", filePath);
                    return new List<CuentaBancaria>();
                }

                await using var fileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true
                );

                var list = await JsonSerializer.DeserializeAsync<List<CuentaBancaria>>(
                    fileStream, _jsonOptions, ct
                ).ConfigureAwait(false);

                _logger.LogDebug("Leídas {Count} cuentas bancarias desde {FilePath}", list?.Count ?? 0, filePath);
                return list ?? new List<CuentaBancaria>();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al leer archivo de cuentas bancarias: {FilePath}", filePath);
                // Fail-safe: devolver lista vacía en caso de error
                return new List<CuentaBancaria>();
            }
        }

        private async Task WriteListSafeAsync(string filePath, IEnumerable<CuentaBancaria> list, CancellationToken ct)
        {
            await _writeSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var tempFilePath = filePath + TempFileExtension;

                // Limpiar archivo temporal existente
                await CleanupTemporaryFileAsync(tempFilePath).ConfigureAwait(false);

                // Serializar datos
                var jsonData = JsonSerializer.Serialize(list, _jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(jsonData);

                // Escribir archivo temporal
                await using (var tempStream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true
                ))
                {
                    await tempStream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                    await tempStream.FlushAsync(ct).ConfigureAwait(false);
                }

                // Reemplazar archivo principal
                File.Move(tempFilePath, filePath, overwrite: true);

                _logger.LogInformation("Guardadas {Count} cuentas bancarias en {FilePath}",
                    list.Count(), filePath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al guardar archivo de cuentas bancarias: {FilePath}", filePath);
                throw;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private async Task TryRecoverTemporaryFileAsync(string filePath)
        {
            var tempFilePath = filePath + TempFileExtension;

            if (!File.Exists(filePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Move(tempFilePath, filePath, overwrite: true);
                    _logger.LogInformation("Archivo temporal recuperado: {TempFilePath} -> {FilePath}",
                        tempFilePath, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo recuperar archivo temporal: {TempFilePath}", tempFilePath);
                }
            }
        }

        private async Task CleanupTemporaryFileAsync(string tempFilePath)
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                    _logger.LogDebug("Archivo temporal limpiado: {TempFilePath}", tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo eliminar archivo temporal: {TempFilePath}", tempFilePath);
                }
            }
        }

        // ----------------------------
        // Validación y utilidades
        // ----------------------------
        private static string ValidateProveedorId(string proveedorId)
        {
            if (string.IsNullOrWhiteSpace(proveedorId))
            {
                throw new ArgumentException("El ID del proveedor no puede ser nulo o vacío.", nameof(proveedorId));
            }

            return proveedorId.Trim();
        }

        /// <summary>
        /// Limpia proveedorId para que sea seguro como parte del nombre de archivo.
        /// Caracteres no válidos se reemplazan por '-'.
        /// Si el resultado es muy largo o queda vacío, se usa un hash estable.
        /// </summary>
        private static string SanitizeForFileName(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return "invalid";
            }

            var input = rawInput.Trim();
            var sanitized = new StringBuilder(input.Length);

            foreach (var character in input)
            {
                if (char.IsLetterOrDigit(character) || character is '-' or '_')
                {
                    sanitized.Append(character);
                }
                else
                {
                    sanitized.Append('-');
                }
            }

            var result = sanitized.ToString().Trim('-');

            // Si el resultado está vacío o es muy largo, usar hash
            if (string.IsNullOrEmpty(result) || result.Length > 80)
            {
                return GenerateFileNameHash(input);
            }

            return result;
        }

        private static string GenerateFileNameHash(string input)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
            return Convert.ToHexString(hashBytes).ToLowerInvariant()[..16]; // Usar solo primeros 16 chars
        }
    }
}