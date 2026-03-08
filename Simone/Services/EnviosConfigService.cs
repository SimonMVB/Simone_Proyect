using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simone.Configuration;

namespace Simone.Services
{
    #region Interface

    /// <summary>
    /// Servicio de configuración de tarifas de envío con operaciones thread-safe
    /// Gestiona reglas de envío tanto para administrador como para vendedores individuales
    /// Implementa patrón de IO atómico similar a BancosConfigService
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface IEnviosConfigService
    {
        #region Administrador

        /// <summary>
        /// Obtiene las reglas de envío del administrador
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Lista de solo lectura con las reglas configuradas</returns>
        Task<IReadOnlyList<TarifaEnvioRegla>> GetAdminAsync(CancellationToken ct = default);

        /// <summary>
        /// Establece las reglas de envío del administrador
        /// </summary>
        /// <param name="reglas">Reglas a guardar (null se trata como lista vacía)</param>
        /// <param name="ct">Token de cancelación</param>
        Task SetAdminAsync(IEnumerable<TarifaEnvioRegla> reglas, CancellationToken ct = default);

        /// <summary>
        /// Elimina la configuración del administrador
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> DeleteAdminAsync(CancellationToken ct = default);

        #endregion

        #region Proveedor

        /// <summary>
        /// Obtiene las reglas de envío de un proveedor
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Lista de solo lectura con las reglas configuradas</returns>
        Task<IReadOnlyList<TarifaEnvioRegla>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default);

        /// <summary>
        /// Establece las reglas de envío de un proveedor
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <param name="reglas">Reglas a guardar (null se trata como lista vacía)</param>
        /// <param name="ct">Token de cancelación</param>
        Task SetByProveedorAsync(string proveedorId, IEnumerable<TarifaEnvioRegla> reglas, CancellationToken ct = default);

        /// <summary>
        /// Elimina la configuración de un proveedor
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> DeleteProveedorAsync(string proveedorId, CancellationToken ct = default);

        /// <summary>
        /// Verifica si existe configuración para un proveedor
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> ExistsConfigForProveedorAsync(string proveedorId, CancellationToken ct = default);

        #endregion

        #region Utilidades

        /// <summary>
        /// Obtiene la lista de todos los IDs de proveedores con configuración
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        Task<IReadOnlyList<string>> GetAllProveedorIdsAsync(CancellationToken ct = default);

        /// <summary>
        /// Crea un backup de todas las configuraciones
        /// </summary>
        /// <param name="backupFolderPath">Carpeta donde guardar el backup</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Cantidad de archivos respaldados</returns>
        Task<int> BackupAllConfigsAsync(string backupFolderPath, CancellationToken ct = default);

        /// <summary>
        /// Limpia archivos temporales y backups antiguos
        /// </summary>
        /// <param name="daysToKeepBackups">Días de retención para backups (default: 30)</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Tupla con archivos eliminados (total, temporales, backups)</returns>
        Task<(int total, int temp, int backups)> CleanupOldFilesAsync(int daysToKeepBackups = 30, CancellationToken ct = default);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del servicio de configuración de envíos con IO atómico y thread-safe
    /// </summary>
    public class EnviosConfigService : IEnviosConfigService
    {
        #region Dependencias

        private readonly string _dataFolder;
        private readonly ILogger<EnviosConfigService> _logger;

        #endregion

        #region Configuración - JSON

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        #endregion

        #region Configuración - Thread Safety

        private static readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        #endregion

        #region Constantes - Configuración

        private const string ADMIN_FILE_NAME = "envios-admin.json";
        private const string FILE_EXTENSION = ".json";
        private const string TEMP_FILE_EXTENSION = ".tmp";
        private const string BACKUP_FILE_EXTENSION = ".bak.json";
        private const string PROVEEDOR_FILE_PREFIX = "envios-proveedor-";
        private const int FILE_BUFFER_SIZE = 8192;
        private const int MAX_FILENAME_LENGTH = 80;
        private const int HASH_DISPLAY_LENGTH = 16;
        private const int DEFAULT_BACKUP_RETENTION_DAYS = 30;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_DIRECTORIO_INICIALIZADO = "Directorio de datos de envíos inicializado. Ruta: {DataFolder}";
        private const string LOG_INFO_REGLAS_LEIDAS = "Reglas de envío leídas. Archivo: {FilePath}, Count: {Count}";
        private const string LOG_INFO_REGLAS_GUARDADAS = "Reglas de envío guardadas. Archivo: {FilePath}, Count: {Count}, Tamaño: {Bytes} bytes";
        private const string LOG_INFO_CONFIG_ELIMINADA = "Configuración de envío eliminada. Archivo: {FilePath}";
        private const string LOG_INFO_BACKUP_CREADO = "Backup de configuración creado. Archivo: {BackupFile}";
        private const string LOG_INFO_BACKUP_COMPLETADO = "Backup completado. Archivos respaldados: {Count}, Carpeta: {BackupFolder}";
        private const string LOG_INFO_CLEANUP_COMPLETADO = "Cleanup completado. Total eliminados: {Total}, Temporales: {Temp}, Backups: {Backups}";
        private const string LOG_INFO_TEMPORAL_RECUPERADO = "Archivo temporal recuperado. Origen: {TempFile}, Destino: {FilePath}";

        // Debug
        private const string LOG_DEBUG_ARCHIVO_NO_ENCONTRADO = "Archivo no encontrado, devolviendo lista vacía. Archivo: {FilePath}";
        private const string LOG_DEBUG_TEMPORAL_LIMPIADO = "Archivo temporal limpiado. Archivo: {TempFile}";
        private const string LOG_DEBUG_BACKUP_LIMPIADO = "Backup antiguo limpiado. Archivo: {BackupFile}, Antigüedad: {Days} días";
        private const string LOG_DEBUG_PROVEEDOR_ID_SANITIZADO = "Proveedor ID sanitizado. Original: {Original}, Sanitizado: {Sanitized}";
        private const string LOG_DEBUG_HASH_GENERADO = "Hash generado para nombre de archivo. Original: {Original}, Hash: {Hash}";
        private const string LOG_DEBUG_ARCHIVO_EXISTE = "Verificando existencia de archivo. Archivo: {FilePath}, Existe: {Exists}";
        private const string LOG_DEBUG_SEMAFORO_ADQUIRIDO = "Semáforo de escritura adquirido. Operación: {Operacion}";
        private const string LOG_DEBUG_SEMAFORO_LIBERADO = "Semáforo de escritura liberado. Operación: {Operacion}";
        private const string LOG_DEBUG_BUSCANDO_PROVEEDORES = "Buscando archivos de proveedores. Patrón: {Pattern}";
        private const string LOG_DEBUG_PROVEEDORES_ENCONTRADOS = "Proveedores encontrados. Count: {Count}";

        // Advertencias
        private const string LOG_WARN_REGLAS_NULL_ADMIN = "Intento de guardar lista nula de reglas (admin), usando lista vacía";
        private const string LOG_WARN_REGLAS_NULL_PROVEEDOR = "Intento de guardar lista nula de reglas. ProveedorId: {ProveedorId}, usando lista vacía";
        private const string LOG_WARN_RECUPERACION_FALLIDA = "No se pudo recuperar archivo temporal. Archivo: {TempFile}";
        private const string LOG_WARN_ELIMINACION_TEMP_FALLIDA = "No se pudo eliminar archivo temporal. Archivo: {TempFile}";
        private const string LOG_WARN_CONFIG_NO_ENCONTRADA = "Configuración no encontrada para eliminar. Archivo: {FilePath}";
        private const string LOG_WARN_ELIMINACION_FALLIDA = "No se pudo eliminar configuración. Archivo: {FilePath}";
        private const string LOG_WARN_BACKUP_FOLDER_CREATE_FAILED = "No se pudo crear carpeta de backup. Ruta: {BackupFolder}";

        // Errores
        private const string LOG_ERROR_CREAR_DIRECTORIO = "Error al crear directorio de datos. Ruta: {DataFolder}";
        private const string LOG_ERROR_LEER_ARCHIVO = "Error al leer reglas de envío. Archivo: {FilePath}";
        private const string LOG_ERROR_ESCRIBIR_ARCHIVO = "Error al guardar reglas de envío. Archivo: {FilePath}";
        private const string LOG_ERROR_ELIMINAR_CONFIG = "Error al eliminar configuración. Archivo: {FilePath}";
        private const string LOG_ERROR_BACKUP_ARCHIVO = "Error al crear backup. Archivo: {FilePath}";
        private const string LOG_ERROR_CLEANUP_ARCHIVOS = "Error al limpiar archivos antiguos";
        private const string LOG_ERROR_LISTAR_DIRECTORIO = "Error al listar archivos de proveedores. Directorio: {DataFolder}";

        #endregion

        #region Constantes - Mensajes de Excepción

        private const string EXC_PROVEEDOR_ID_NULL = "El ID del proveedor no puede ser nulo o vacío";
        private const string EXC_BACKUP_FOLDER_NULL = "La carpeta de backup no puede ser nula o vacía";
        private const string EXC_RETENTION_DAYS_INVALID = "Los días de retención deben ser mayor o igual a cero";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del servicio de configuración de envíos
        /// </summary>
        /// <param name="env">Entorno de hosting</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public EnviosConfigService(IHostEnvironment env, ILogger<EnviosConfigService> logger)
        {
            _dataFolder = Path.Combine(env?.ContentRootPath ?? throw new ArgumentNullException(nameof(env)), "App_Data");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeDataDirectory();
        }

        /// <summary>
        /// Inicializa el directorio de datos
        /// </summary>
        private void InitializeDataDirectory()
        {
            try
            {
                if (!Directory.Exists(_dataFolder))
                {
                    Directory.CreateDirectory(_dataFolder);
                }

                _logger.LogInformation(LOG_INFO_DIRECTORIO_INICIALIZADO, _dataFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LOG_ERROR_CREAR_DIRECTORIO, _dataFolder);
                throw;
            }
        }

        #endregion

        #region Helpers - Rutas

        /// <summary>
        /// Obtiene la ruta del archivo de configuración del administrador
        /// </summary>
        private string GetAdminFilePath() => Path.Combine(_dataFolder, ADMIN_FILE_NAME);

        /// <summary>
        /// Obtiene la ruta del archivo de configuración de un proveedor
        /// </summary>
        private string GetProveedorFilePath(string proveedorId)
        {
            var safeId = SanitizeForFileName(ValidateProveedorId(proveedorId));
            return Path.Combine(_dataFolder, $"{PROVEEDOR_FILE_PREFIX}{safeId}{FILE_EXTENSION}");
        }

        /// <summary>
        /// Obtiene la ruta del archivo temporal
        /// </summary>
        private static string GetTempFilePath(string filePath) => filePath + TEMP_FILE_EXTENSION;

        /// <summary>
        /// Obtiene la ruta del archivo de backup con timestamp
        /// </summary>
        private static string GetBackupFilePath(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            return Path.Combine(directory ?? string.Empty, $"{fileNameWithoutExt}_{timestamp}{BACKUP_FILE_EXTENSION}");
        }

        #endregion

        #region Helpers - IO Lectura

        /// <summary>
        /// Lee una lista de reglas desde un archivo de forma segura
        /// </summary>
        private async Task<List<TarifaEnvioRegla>> ReadListSafeAsync(string filePath, string context, CancellationToken ct)
        {
            try
            {
                await TryRecoverTemporaryFileAsync(filePath).ConfigureAwait(false);

                if (!File.Exists(filePath))
                {
                    _logger.LogDebug(LOG_DEBUG_ARCHIVO_NO_ENCONTRADO, filePath);
                    return new List<TarifaEnvioRegla>();
                }

                await using var fileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    FILE_BUFFER_SIZE,
                    useAsync: true);

                var list = await JsonSerializer.DeserializeAsync<List<TarifaEnvioRegla>>(
                    fileStream, _jsonOptions, ct).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_REGLAS_LEIDAS, filePath, list?.Count ?? 0);

                return list ?? new List<TarifaEnvioRegla>();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_LEER_ARCHIVO, filePath);
                return new List<TarifaEnvioRegla>();
            }
        }

        #endregion

        #region Helpers - IO Escritura

        /// <summary>
        /// Escribe una lista de reglas a un archivo de forma atómica
        /// </summary>
        private async Task WriteListSafeAsync(string filePath, IEnumerable<TarifaEnvioRegla> list, string context, CancellationToken ct)
        {
            await _writeSemaphore.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                _logger.LogDebug(LOG_DEBUG_SEMAFORO_ADQUIRIDO, context);

                // Crear backup si el archivo existe
                if (File.Exists(filePath))
                {
                    await CreateBackupFileAsync(filePath).ConfigureAwait(false);
                }

                var tempFilePath = GetTempFilePath(filePath);
                await CleanupTemporaryFileAsync(tempFilePath).ConfigureAwait(false);

                var jsonData = JsonSerializer.Serialize(list, _jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(jsonData);

                await using (var tempStream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    FILE_BUFFER_SIZE,
                    useAsync: true))
                {
                    await tempStream.WriteAsync(bytes, ct).ConfigureAwait(false);
                    await tempStream.FlushAsync(ct).ConfigureAwait(false);
                }

                File.Move(tempFilePath, filePath, overwrite: true);

                _logger.LogInformation(LOG_INFO_REGLAS_GUARDADAS, filePath, list.Count(), bytes.Length);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ESCRIBIR_ARCHIVO, filePath);
                throw;
            }
            finally
            {
                _writeSemaphore.Release();
                _logger.LogDebug(LOG_DEBUG_SEMAFORO_LIBERADO, context);
            }
        }

        #endregion

        #region Helpers - Archivos Temporales

        /// <summary>
        /// Intenta recuperar un archivo temporal en caso de fallo previo
        /// </summary>
        private async Task TryRecoverTemporaryFileAsync(string filePath)
        {
            var tempFilePath = GetTempFilePath(filePath);

            if (!File.Exists(filePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Move(tempFilePath, filePath, overwrite: true);
                    _logger.LogInformation(LOG_INFO_TEMPORAL_RECUPERADO, tempFilePath, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, LOG_WARN_RECUPERACION_FALLIDA, tempFilePath);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Limpia un archivo temporal si existe
        /// </summary>
        private async Task CleanupTemporaryFileAsync(string tempFilePath)
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                    _logger.LogDebug(LOG_DEBUG_TEMPORAL_LIMPIADO, tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, LOG_WARN_ELIMINACION_TEMP_FALLIDA, tempFilePath);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Crea un backup del archivo antes de sobrescribirlo
        /// </summary>
        private async Task CreateBackupFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var backupFilePath = GetBackupFilePath(filePath);
                File.Copy(filePath, backupFilePath, overwrite: true);

                _logger.LogInformation(LOG_INFO_BACKUP_CREADO, backupFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, LOG_ERROR_BACKUP_ARCHIVO, filePath);
                // No lanzamos excepción, el backup es opcional
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Helpers - Validación

        /// <summary>
        /// Valida que el ID del proveedor no esté vacío
        /// </summary>
        private string ValidateProveedorId(string proveedorId)
        {
            if (string.IsNullOrWhiteSpace(proveedorId))
            {
                throw new ArgumentException(EXC_PROVEEDOR_ID_NULL, nameof(proveedorId));
            }

            return proveedorId.Trim();
        }

        #endregion

        #region Helpers - Sanitización

        /// <summary>
        /// Sanitiza un ID para usarlo como nombre de archivo
        /// </summary>
        private string SanitizeForFileName(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return "invalid";
            }

            var original = rawInput;
            var input = rawInput.Trim();
            var sb = new StringBuilder(input.Length);

            foreach (var ch in input)
            {
                sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-');
            }

            var result = sb.ToString().Trim('-');

            if (string.IsNullOrEmpty(result) || result.Length > MAX_FILENAME_LENGTH)
            {
                var hash = GenerateFileNameHash(input);
                _logger.LogDebug(LOG_DEBUG_HASH_GENERADO, original, hash);
                return hash;
            }

            if (original != result)
            {
                _logger.LogDebug(LOG_DEBUG_PROVEEDOR_ID_SANITIZADO, original, result);
            }

            return result;
        }

        /// <summary>
        /// Genera un hash SHA256 para nombres de archivo largos o inválidos
        /// </summary>
        private static string GenerateFileNameHash(string input)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
            return Convert.ToHexString(hash).ToLowerInvariant()[..HASH_DISPLAY_LENGTH];
        }

        #endregion

        #region API - Administrador

        /// <inheritdoc />
        public async Task<IReadOnlyList<TarifaEnvioRegla>> GetAdminAsync(CancellationToken ct = default)
        {
            var filePath = GetAdminFilePath();
            var list = await ReadListSafeAsync(filePath, "Admin", ct).ConfigureAwait(false);
            return list.AsReadOnly();
        }

        /// <inheritdoc />
        public async Task SetAdminAsync(IEnumerable<TarifaEnvioRegla> reglas, CancellationToken ct = default)
        {
            if (reglas == null)
            {
                _logger.LogWarning(LOG_WARN_REGLAS_NULL_ADMIN);
                reglas = new List<TarifaEnvioRegla>();
            }

            var filePath = GetAdminFilePath();
            await WriteListSafeAsync(filePath, reglas, "Admin", ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAdminAsync(CancellationToken ct = default)
        {
            var filePath = GetAdminFilePath();

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning(LOG_WARN_CONFIG_NO_ENCONTRADA, filePath);
                    return false;
                }

                // Crear backup antes de eliminar
                await CreateBackupFileAsync(filePath).ConfigureAwait(false);

                File.Delete(filePath);

                // Limpiar archivo temporal asociado
                var tempFilePath = GetTempFilePath(filePath);
                await CleanupTemporaryFileAsync(tempFilePath).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CONFIG_ELIMINADA, filePath);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ELIMINAR_CONFIG, filePath);
                return false;
            }
        }

        #endregion

        #region API - Proveedor

        /// <inheritdoc />
        public async Task<IReadOnlyList<TarifaEnvioRegla>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default)
        {
            var filePath = GetProveedorFilePath(proveedorId);
            var list = await ReadListSafeAsync(filePath, $"Proveedor_{proveedorId}", ct).ConfigureAwait(false);
            return list.AsReadOnly();
        }

        /// <inheritdoc />
        public async Task SetByProveedorAsync(string proveedorId, IEnumerable<TarifaEnvioRegla> reglas, CancellationToken ct = default)
        {
            if (reglas == null)
            {
                _logger.LogWarning(LOG_WARN_REGLAS_NULL_PROVEEDOR, proveedorId);
                reglas = new List<TarifaEnvioRegla>();
            }

            var filePath = GetProveedorFilePath(proveedorId);
            await WriteListSafeAsync(filePath, reglas, $"Proveedor_{proveedorId}", ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteProveedorAsync(string proveedorId, CancellationToken ct = default)
        {
            var filePath = GetProveedorFilePath(proveedorId);

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning(LOG_WARN_CONFIG_NO_ENCONTRADA, filePath);
                    return false;
                }

                // Crear backup antes de eliminar
                await CreateBackupFileAsync(filePath).ConfigureAwait(false);

                File.Delete(filePath);

                // Limpiar archivo temporal asociado
                var tempFilePath = GetTempFilePath(filePath);
                await CleanupTemporaryFileAsync(tempFilePath).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CONFIG_ELIMINADA, filePath);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_ELIMINAR_CONFIG, filePath);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsConfigForProveedorAsync(string proveedorId, CancellationToken ct = default)
        {
            try
            {
                var filePath = GetProveedorFilePath(proveedorId);
                var exists = File.Exists(filePath);

                _logger.LogDebug(LOG_DEBUG_ARCHIVO_EXISTE, filePath, exists);

                return exists;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al verificar existencia de configuración. ProveedorId: {ProveedorId}", proveedorId);
                return false;
            }
        }

        #endregion

        #region API - Utilidades

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetAllProveedorIdsAsync(CancellationToken ct = default)
        {
            try
            {
                var pattern = $"{PROVEEDOR_FILE_PREFIX}*{FILE_EXTENSION}";
                _logger.LogDebug(LOG_DEBUG_BUSCANDO_PROVEEDORES, pattern);

                if (!Directory.Exists(_dataFolder))
                {
                    return Array.Empty<string>();
                }

                var files = Directory.GetFiles(_dataFolder, pattern, SearchOption.TopDirectoryOnly);
                var proveedorIds = new List<string>();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.StartsWith(PROVEEDOR_FILE_PREFIX))
                    {
                        var proveedorId = fileName[PROVEEDOR_FILE_PREFIX.Length..];
                        proveedorIds.Add(proveedorId);
                    }
                }

                _logger.LogDebug(LOG_DEBUG_PROVEEDORES_ENCONTRADOS, proveedorIds.Count);

                return proveedorIds.AsReadOnly();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_LISTAR_DIRECTORIO, _dataFolder);
                return Array.Empty<string>();
            }
        }

        /// <inheritdoc />
        public async Task<int> BackupAllConfigsAsync(string backupFolderPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(backupFolderPath))
            {
                throw new ArgumentException(EXC_BACKUP_FOLDER_NULL, nameof(backupFolderPath));
            }

            try
            {
                // Crear carpeta de backup si no existe
                if (!Directory.Exists(backupFolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(backupFolderPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, LOG_WARN_BACKUP_FOLDER_CREATE_FAILED, backupFolderPath);
                        return 0;
                    }
                }

                var pattern = $"*{FILE_EXTENSION}";
                var files = Directory.GetFiles(_dataFolder, pattern, SearchOption.TopDirectoryOnly);
                var count = 0;

                foreach (var sourceFile in files)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var fileName = Path.GetFileName(sourceFile);
                        var destFile = Path.Combine(backupFolderPath, fileName);

                        File.Copy(sourceFile, destFile, overwrite: true);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al copiar archivo al backup. Archivo: {SourceFile}", sourceFile);
                        // Continuar con los demás archivos
                    }
                }

                _logger.LogInformation(LOG_INFO_BACKUP_COMPLETADO, count, backupFolderPath);

                return count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al crear backup de configuraciones. Carpeta: {BackupFolder}", backupFolderPath);
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<(int total, int temp, int backups)> CleanupOldFilesAsync(int daysToKeepBackups = DEFAULT_BACKUP_RETENTION_DAYS, CancellationToken ct = default)
        {
            if (daysToKeepBackups < 0)
            {
                throw new ArgumentException(EXC_RETENTION_DAYS_INVALID, nameof(daysToKeepBackups));
            }

            try
            {
                var tempCount = 0;
                var backupCount = 0;

                if (!Directory.Exists(_dataFolder))
                {
                    return (0, 0, 0);
                }

                // Limpiar archivos temporales
                var tempFiles = Directory.GetFiles(_dataFolder, $"*{TEMP_FILE_EXTENSION}", SearchOption.TopDirectoryOnly);
                foreach (var tempFile in tempFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        File.Delete(tempFile);
                        tempCount++;
                        _logger.LogDebug(LOG_DEBUG_TEMPORAL_LIMPIADO, tempFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, LOG_WARN_ELIMINACION_TEMP_FALLIDA, tempFile);
                    }
                }

                // Limpiar backups antiguos
                var backupFiles = Directory.GetFiles(_dataFolder, $"*{BACKUP_FILE_EXTENSION}", SearchOption.TopDirectoryOnly);
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeepBackups);

                foreach (var backupFile in backupFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new FileInfo(backupFile);
                        if (fileInfo.LastWriteTimeUtc < cutoffDate)
                        {
                            File.Delete(backupFile);
                            backupCount++;

                            var days = (DateTime.UtcNow - fileInfo.LastWriteTimeUtc).Days;
                            _logger.LogDebug(LOG_DEBUG_BACKUP_LIMPIADO, backupFile, days);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al eliminar backup antiguo. Archivo: {BackupFile}", backupFile);
                    }
                }

                var total = tempCount + backupCount;
                _logger.LogInformation(LOG_INFO_CLEANUP_COMPLETADO, total, tempCount, backupCount);

                return (total, tempCount, backupCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CLEANUP_ARCHIVOS);
                return (0, 0, 0);
            }
        }

        #endregion
    }

    #endregion
}