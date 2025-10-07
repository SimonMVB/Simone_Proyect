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
    /// Servicio de configuración de tarifas de envío por:
    ///  - Administrador (App_Data/envios-admin.json) [opcional]
    ///  - Vendedor (App_Data/envios-proveedor-{proveedorIdSanitizado}.json)
    /// Mismo patrón que Bancos: IO atómico + thread-safe.
    /// </summary>
    public interface IEnviosConfigService
    {
        Task<IReadOnlyList<TarifaEnvioRegla>> GetAdminAsync(CancellationToken ct = default);
        Task SetAdminAsync(IEnumerable<TarifaEnvioRegla> reglas, CancellationToken ct = default);

        Task<IReadOnlyList<TarifaEnvioRegla>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default);
        Task SetByProveedorAsync(string proveedorId, IEnumerable<TarifaEnvioRegla> reglas, CancellationToken ct = default);
    }

    public class EnviosConfigService : IEnviosConfigService
    {
        private readonly string _dataFolder;
        private readonly ILogger<EnviosConfigService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        private const string AdminFileName = "envios-admin.json";
        private const string FileExtension = ".json";
        private const string TempFileExtension = ".tmp";

        public EnviosConfigService(IHostEnvironment env, ILogger<EnviosConfigService> logger)
        {
            _dataFolder = Path.Combine(env.ContentRootPath, "App_Data");
            _logger = logger;

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

        private string GetProveedorFilePath(string proveedorId)
        {
            var safeId = SanitizeForFileName(ValidateProveedorId(proveedorId));
            return Path.Combine(_dataFolder, $"envios-proveedor-{safeId}{FileExtension}");
        }

        // -------- API --------
        public async Task<IReadOnlyList<TarifaEnvioRegla>> GetAdminAsync(CancellationToken ct = default)
        {
            var filePath = GetAdminFilePath();
            var list = await ReadListSafeAsync(filePath, ct).ConfigureAwait(false);
            return list.AsReadOnly();
        }

        public async Task SetAdminAsync(IEnumerable<TarifaEnvioRegla> reglas, CancellationToken ct = default)
        {
            if (reglas == null)
            {
                _logger.LogWarning("Intento de guardar lista nula de reglas de envío (admin)");
                reglas = new List<TarifaEnvioRegla>();
            }

            var filePath = GetAdminFilePath();
            await WriteListSafeAsync(filePath, reglas, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<TarifaEnvioRegla>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default)
        {
            var filePath = GetProveedorFilePath(proveedorId);
            var list = await ReadListSafeAsync(filePath, ct).ConfigureAwait(false);
            return list.AsReadOnly();
        }

        public async Task SetByProveedorAsync(string proveedorId, IEnumerable<TarifaEnvioRegla> reglas, CancellationToken ct = default)
        {
            if (reglas == null)
            {
                _logger.LogWarning("Intento de guardar lista nula de reglas de envío para proveedor: {ProveedorId}", proveedorId);
                reglas = new List<TarifaEnvioRegla>();
            }

            var filePath = GetProveedorFilePath(proveedorId);
            await WriteListSafeAsync(filePath, reglas, ct).ConfigureAwait(false);
        }

        // -------- IO helpers --------
        private async Task<List<TarifaEnvioRegla>> ReadListSafeAsync(string filePath, CancellationToken ct)
        {
            try
            {
                await TryRecoverTemporaryFileAsync(filePath).ConfigureAwait(false);

                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("Archivo no encontrado, devolviendo lista vacía: {FilePath}", filePath);
                    return new List<TarifaEnvioRegla>();
                }

                await using var fileStream = new FileStream(
                    filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

                var list = await JsonSerializer.DeserializeAsync<List<TarifaEnvioRegla>>(
                    fileStream, _jsonOptions, ct).ConfigureAwait(false);

                _logger.LogDebug("Leídas {Count} reglas de envío desde {FilePath}", list?.Count ?? 0, filePath);
                return list ?? new List<TarifaEnvioRegla>();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al leer reglas de envío: {FilePath}", filePath);
                return new List<TarifaEnvioRegla>();
            }
        }

        private async Task WriteListSafeAsync(string filePath, IEnumerable<TarifaEnvioRegla> list, CancellationToken ct)
        {
            await _writeSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var tempFilePath = filePath + TempFileExtension;

                await CleanupTemporaryFileAsync(tempFilePath).ConfigureAwait(false);

                var jsonData = JsonSerializer.Serialize(list, _jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(jsonData);

                await using (var tempStream = new FileStream(
                    tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await tempStream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                    await tempStream.FlushAsync(ct).ConfigureAwait(false);
                }

                File.Move(tempFilePath, filePath, overwrite: true);
                _logger.LogInformation("Guardadas {Count} reglas de envío en {FilePath}", list.Count(), filePath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al guardar reglas de envío: {FilePath}", filePath);
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

        // -------- util --------
        private static string ValidateProveedorId(string proveedorId)
        {
            if (string.IsNullOrWhiteSpace(proveedorId))
                throw new ArgumentException("El ID del proveedor no puede ser nulo o vacío.", nameof(proveedorId));
            return proveedorId.Trim();
        }

        private static string SanitizeForFileName(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput)) return "invalid";

            var input = rawInput.Trim();
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
                sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-');

            var result = sb.ToString().Trim('-');
            if (string.IsNullOrEmpty(result) || result.Length > 80)
                return GenerateFileNameHash(input);

            return result;
        }

        private static string GenerateFileNameHash(string input)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
            return Convert.ToHexString(hash).ToLowerInvariant()[..16];
        }
    }
}
