using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simone.Configuration;

namespace Simone.Services
{
    #region Interface

    /// <summary>
    /// Servicio de configuración de cuentas bancarias con operaciones thread-safe
    /// Gestiona configuraciones para:
    ///  - Administrador (App_Data/bancos-admin.json)
    ///  - Vendedor (App_Data/bancos-proveedor-{id}.json)
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface IBancosConfigService
    {
        /// <summary>
        /// Obtiene las cuentas bancarias del administrador
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Lista inmutable de cuentas bancarias</returns>
        Task<IReadOnlyList<CuentaBancaria>> GetAdminAsync(CancellationToken ct = default);

        /// <summary>
        /// Guarda las cuentas bancarias del administrador
        /// </summary>
        /// <param name="cuentas">Cuentas a guardar</param>
        /// <param name="ct">Token de cancelación</param>
        Task SetAdminAsync(IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default);

        /// <summary>
        /// Obtiene las cuentas bancarias de un proveedor específico
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Lista inmutable de cuentas bancarias</returns>
        Task<IReadOnlyList<CuentaBancaria>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default);

        /// <summary>
        /// Guarda las cuentas bancarias de un proveedor específico
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <param name="cuentas">Cuentas a guardar</param>
        /// <param name="ct">Token de cancelación</param>
        Task SetByProveedorAsync(string proveedorId, IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default);

        /// <summary>
        /// Elimina todas las cuentas bancarias de un proveedor
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <param name="ct">Token de cancelación</param>
        Task DeleteProveedorConfigAsync(string proveedorId, CancellationToken ct = default);

        /// <summary>
        /// Verifica si un proveedor tiene configuración de cuentas
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <returns>True si existe configuración, False en caso contrario</returns>
        Task<bool> ExistsConfigForProveedorAsync(string proveedorId);

        /// <summary>
        /// Obtiene todos los IDs de proveedores que tienen configuración
        /// </summary>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Lista de IDs de proveedores</returns>
        Task<IReadOnlyList<string>> GetAllProveedorIdsAsync(CancellationToken ct = default);

        /// <summary>
        /// Crea un respaldo de todas las configuraciones
        /// </summary>
        /// <param name="backupFolderPath">Ruta de la carpeta de respaldo</param>
        /// <param name="ct">Token de cancelación</param>
        Task<int> BackupAllConfigsAsync(string backupFolderPath, CancellationToken ct = default);

        /// <summary>
        /// Limpia archivos temporales y respaldos antiguos
        /// </summary>
        /// <param name="daysToKeepBackups">Días para mantener respaldos (0 = no eliminar respaldos)</param>
        /// <param name="ct">Token de cancelación</param>
        Task<int> CleanupOldFilesAsync(int daysToKeepBackups = 30, CancellationToken ct = default);
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del servicio de configuración de cuentas bancarias
    /// con operaciones atómicas, thread-safe y resilientes
    /// </summary>
    public class BancosConfigService : IBancosConfigService
    {
        #region Dependencias

        private readonly string _dataFolder;
        private readonly ILogger<BancosConfigService> _logger;

        #endregion

        #region Constantes - Configuración

        private const string ADMIN_FILE_NAME = "bancos-admin.json";
        private const string PROVEEDOR_FILE_PREFIX = "bancos-proveedor-";
        private const string FILE_EXTENSION = ".json";
        private const string TEMP_FILE_EXTENSION = ".tmp";
        private const string BACKUP_FILE_EXTENSION = ".bak";
        private const string BACKUP_TIMESTAMP_FORMAT = "yyyyMMdd-HHmmss";

        private const int FILE_BUFFER_SIZE = 8192;
        private const int MAX_FILENAME_LENGTH = 80;
        private const int HASH_SUBSTRING_LENGTH = 16;
        private const int DEFAULT_BACKUP_RETENTION_DAYS = 30;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_DIRECTORY_INITIALIZED = "Directorio de datos inicializado. Ruta: {DataFolder}";
        private const string LOG_INFO_ACCOUNTS_READ = "Leídas {Count} cuentas bancarias. Archivo: {FilePath}";
        private const string LOG_INFO_ACCOUNTS_SAVED = "Guardadas {Count} cuentas bancarias. Archivo: {FilePath}, Tamaño: {Size} bytes";
        private const string LOG_INFO_TEMP_FILE_RECOVERED = "Archivo temporal recuperado. Origen: {TempFilePath}, Destino: {FilePath}";
        private const string LOG_INFO_CONFIG_DELETED = "Configuración de proveedor eliminada. ProveedorId: {ProveedorId}, Archivo: {FilePath}";
        private const string LOG_INFO_BACKUP_CREATED = "Respaldo creado. Archivo: {BackupPath}";
        private const string LOG_INFO_BACKUP_COMPLETED = "Respaldo completado. Archivos: {Count}, Destino: {BackupFolder}";
        private const string LOG_INFO_CLEANUP_COMPLETED = "Limpieza completada. Archivos eliminados: {DeletedCount}, Temp: {TempCount}, Backups: {BackupCount}";

        // Debug
        private const string LOG_DEBUG_FILE_NOT_FOUND = "Archivo no encontrado, devolviendo lista vacía. Archivo: {FilePath}";
        private const string LOG_DEBUG_TEMP_FILE_CLEANED = "Archivo temporal limpiado. Archivo: {TempFilePath}";
        private const string LOG_DEBUG_BACKUP_FILE_CLEANED = "Archivo de respaldo limpiado. Archivo: {BackupFilePath}";
        private const string LOG_DEBUG_PROVIDER_ID_SANITIZED = "ID de proveedor sanitizado. Original: {OriginalId}, Sanitizado: {SanitizedId}";
        private const string LOG_DEBUG_HASH_GENERATED = "Hash generado para ID largo. Original: {OriginalId}, Hash: {Hash}";
        private const string LOG_DEBUG_FILE_EXISTS_CHECK = "Verificación de existencia. Archivo: {FilePath}, Existe: {Exists}";
        private const string LOG_DEBUG_SEMAPHORE_ACQUIRED = "Semáforo de escritura adquirido. Archivo: {FilePath}";
        private const string LOG_DEBUG_SEMAPHORE_RELEASED = "Semáforo de escritura liberado. Archivo: {FilePath}";

        // Advertencias
        private const string LOG_WARN_NULL_ACCOUNTS_ADMIN = "Intento de guardar lista nula de cuentas para administrador, usando lista vacía";
        private const string LOG_WARN_NULL_ACCOUNTS_PROVEEDOR = "Intento de guardar lista nula de cuentas. ProveedorId: {ProveedorId}, usando lista vacía";
        private const string LOG_WARN_TEMP_FILE_RECOVERY_FAILED = "No se pudo recuperar archivo temporal. TempFile: {TempFilePath}, Error: {Error}";
        private const string LOG_WARN_TEMP_FILE_DELETE_FAILED = "No se pudo eliminar archivo temporal. TempFile: {TempFilePath}, Error: {Error}";
        private const string LOG_WARN_BACKUP_FILE_DELETE_FAILED = "No se pudo eliminar archivo de respaldo. BackupFile: {BackupFilePath}, Error: {Error}";
        private const string LOG_WARN_CONFIG_NOT_FOUND_DELETE = "Configuración no encontrada al intentar eliminar. ProveedorId: {ProveedorId}, Archivo: {FilePath}";
        private const string LOG_WARN_INVALID_JSON_STRUCTURE = "Estructura JSON inválida, devolviendo lista vacía. Archivo: {FilePath}";
        private const string LOG_WARN_BACKUP_FOLDER_CREATE_FAILED = "No se pudo crear carpeta de respaldo. Ruta: {BackupFolder}, Error: {Error}";

        // Errores
        private const string LOG_ERROR_CREATE_DIRECTORY = "Error al crear el directorio de datos. Ruta: {DataFolder}";
        private const string LOG_ERROR_READ_FILE = "Error al leer archivo de cuentas bancarias. Archivo: {FilePath}";
        private const string LOG_ERROR_WRITE_FILE = "Error al guardar archivo de cuentas bancarias. Archivo: {FilePath}";
        private const string LOG_ERROR_DELETE_FILE = "Error al eliminar archivo de configuración. Archivo: {FilePath}";
        private const string LOG_ERROR_BACKUP_FILE = "Error al crear respaldo. Archivo: {FilePath}";
        private const string LOG_ERROR_CLEANUP_FILES = "Error durante la limpieza de archivos";
        private const string LOG_ERROR_LIST_DIRECTORY = "Error al listar archivos del directorio. Directorio: {Directory}";

        #endregion

        #region Constantes - Mensajes de Excepción

        private const string EX_MSG_PROVIDER_ID_NULL = "El ID del proveedor no puede ser nulo o vacío.";
        private const string EX_MSG_BACKUP_FOLDER_NULL = "La ruta de la carpeta de respaldo no puede ser nula o vacía.";
        private const string EX_MSG_INVALID_RETENTION_DAYS = "Los días de retención deben ser mayores o iguales a 0.";

        #endregion

        #region Configuración JSON

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        #endregion

        #region Thread Safety

        // Semáforo para operaciones de escritura (async-friendly)
        private static readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del servicio de configuración de cuentas bancarias
        /// </summary>
        /// <param name="env">Entorno de hospedaje</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <exception cref="ArgumentNullException">Si env o logger son null</exception>
        public BancosConfigService(IHostEnvironment env, ILogger<BancosConfigService> logger)
        {
            _dataFolder = Path.Combine(
                env?.ContentRootPath ?? throw new ArgumentNullException(nameof(env)),
                "App_Data"
            );
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeDataDirectory();
        }

        /// <summary>
        /// Inicializa el directorio de datos asegurando que exista
        /// </summary>
        private void InitializeDataDirectory()
        {
            try
            {
                if (!Directory.Exists(_dataFolder))
                {
                    Directory.CreateDirectory(_dataFolder);
                }

                _logger.LogInformation(LOG_INFO_DIRECTORY_INITIALIZED, _dataFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LOG_ERROR_CREATE_DIRECTORY, _dataFolder);
                throw;
            }
        }

        #endregion

        #region Helpers - Rutas de Archivos

        /// <summary>
        /// Obtiene la ruta del archivo de configuración del administrador
        /// </summary>
        private string GetAdminFilePath() => Path.Combine(_dataFolder, ADMIN_FILE_NAME);

        /// <summary>
        /// Genera una ruta de archivo segura para el proveedorId
        /// </summary>
        /// <param name="proveedorId">ID del proveedor</param>
        /// <returns>Ruta completa del archivo de configuración</returns>
        private string GetProveedorFilePath(string proveedorId)
        {
            var validatedId = ValidateProveedorId(proveedorId);
            var safeId = SanitizeForFileName(validatedId);

            _logger.LogDebug(LOG_DEBUG_PROVIDER_ID_SANITIZED, proveedorId, safeId);

            return Path.Combine(_dataFolder, $"{PROVEEDOR_FILE_PREFIX}{safeId}{FILE_EXTENSION}");
        }

        /// <summary>
        /// Obtiene la ruta del archivo temporal para una ruta dada
        /// </summary>
        private static string GetTempFilePath(string filePath) => filePath + TEMP_FILE_EXTENSION;

        /// <summary>
        /// Obtiene la ruta del archivo de respaldo para una ruta dada
        /// </summary>
        private static string GetBackupFilePath(string filePath)
        {
            var timestamp = DateTime.Now.ToString(BACKUP_TIMESTAMP_FORMAT);
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            return Path.Combine(directory, $"{fileNameWithoutExt}_{timestamp}{BACKUP_FILE_EXTENSION}{extension}");
        }

        #endregion

        #region API Pública - Administrador

        /// <inheritdoc />
        public async Task<IReadOnlyList<CuentaBancaria>> GetAdminAsync(CancellationToken ct = default)
        {
            _logger.LogDebug("Obteniendo cuentas bancarias del administrador");

            var filePath = GetAdminFilePath();
            var list = await ReadListSafeAsync(filePath, "Administrador", ct).ConfigureAwait(false);

            return list.AsReadOnly();
        }

        /// <inheritdoc />
        public async Task SetAdminAsync(IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default)
        {
            if (cuentas == null)
            {
                _logger.LogWarning(LOG_WARN_NULL_ACCOUNTS_ADMIN);
                cuentas = new List<CuentaBancaria>();
            }

            _logger.LogDebug("Guardando {Count} cuentas bancarias del administrador", cuentas.Count());

            var filePath = GetAdminFilePath();
            await WriteListSafeAsync(filePath, cuentas, "Administrador", ct).ConfigureAwait(false);
        }

        #endregion

        #region API Pública - Proveedor

        /// <inheritdoc />
        public async Task<IReadOnlyList<CuentaBancaria>> GetByProveedorAsync(string proveedorId, CancellationToken ct = default)
        {
            _logger.LogDebug("Obteniendo cuentas bancarias. ProveedorId: {ProveedorId}", proveedorId);

            var filePath = GetProveedorFilePath(proveedorId);
            var list = await ReadListSafeAsync(filePath, $"Proveedor_{proveedorId}", ct).ConfigureAwait(false);

            return list.AsReadOnly();
        }

        /// <inheritdoc />
        public async Task SetByProveedorAsync(string proveedorId, IEnumerable<CuentaBancaria> cuentas, CancellationToken ct = default)
        {
            if (cuentas == null)
            {
                _logger.LogWarning(LOG_WARN_NULL_ACCOUNTS_PROVEEDOR, proveedorId);
                cuentas = new List<CuentaBancaria>();
            }

            _logger.LogDebug("Guardando {Count} cuentas bancarias. ProveedorId: {ProveedorId}",
                cuentas.Count(), proveedorId);

            var filePath = GetProveedorFilePath(proveedorId);
            await WriteListSafeAsync(filePath, cuentas, $"Proveedor_{proveedorId}", ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteProveedorConfigAsync(string proveedorId, CancellationToken ct = default)
        {
            _logger.LogInformation("Eliminando configuración de proveedor. ProveedorId: {ProveedorId}", proveedorId);

            var filePath = GetProveedorFilePath(proveedorId);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning(LOG_WARN_CONFIG_NOT_FOUND_DELETE, proveedorId, filePath);
                return;
            }

            await _writeSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _logger.LogDebug(LOG_DEBUG_SEMAPHORE_ACQUIRED, filePath);

                // Crear respaldo antes de eliminar
                await CreateBackupFileAsync(filePath, ct).ConfigureAwait(false);

                // Eliminar archivo principal
                File.Delete(filePath);

                // Limpiar archivos temporales asociados
                await CleanupTemporaryFileAsync(GetTempFilePath(filePath)).ConfigureAwait(false);

                _logger.LogInformation(LOG_INFO_CONFIG_DELETED, proveedorId, filePath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_DELETE_FILE, filePath);
                throw;
            }
            finally
            {
                _writeSemaphore.Release();
                _logger.LogDebug(LOG_DEBUG_SEMAPHORE_RELEASED, filePath);
            }
        }

        /// <inheritdoc />
        public Task<bool> ExistsConfigForProveedorAsync(string proveedorId)
        {
            var filePath = GetProveedorFilePath(proveedorId);
            var exists = File.Exists(filePath);

            _logger.LogDebug(LOG_DEBUG_FILE_EXISTS_CHECK, filePath, exists);

            return Task.FromResult(exists);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetAllProveedorIdsAsync(CancellationToken ct = default)
        {
            _logger.LogDebug("Obteniendo todos los IDs de proveedores con configuración");

            try
            {
                if (!Directory.Exists(_dataFolder))
                {
                    _logger.LogDebug("Directorio de datos no existe, devolviendo lista vacía");
                    return new List<string>().AsReadOnly();
                }

                var files = Directory.GetFiles(_dataFolder, $"{PROVEEDOR_FILE_PREFIX}*{FILE_EXTENSION}");
                var proveedorIds = new List<string>();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.StartsWith(PROVEEDOR_FILE_PREFIX))
                    {
                        var id = fileName.Substring(PROVEEDOR_FILE_PREFIX.Length);
                        proveedorIds.Add(id);
                    }
                }

                _logger.LogInformation("Encontrados {Count} proveedores con configuración", proveedorIds.Count);

                return proveedorIds.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LOG_ERROR_LIST_DIRECTORY, _dataFolder);
                return new List<string>().AsReadOnly();
            }
        }

        #endregion

        #region API Pública - Utilidades

        /// <inheritdoc />
        public async Task<int> BackupAllConfigsAsync(string backupFolderPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(backupFolderPath))
            {
                throw new ArgumentException(EX_MSG_BACKUP_FOLDER_NULL, nameof(backupFolderPath));
            }

            _logger.LogInformation("Creando respaldo de todas las configuraciones. Destino: {BackupFolder}", backupFolderPath);

            try
            {
                // Crear carpeta de respaldo si no existe
                if (!Directory.Exists(backupFolderPath))
                {
                    Directory.CreateDirectory(backupFolderPath);
                }

                var files = Directory.GetFiles(_dataFolder, $"*{FILE_EXTENSION}");
                var backedUpCount = 0;

                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(file);
                    var backupPath = Path.Combine(backupFolderPath, fileName);

                    try
                    {
                        File.Copy(file, backupPath, overwrite: true);
                        backedUpCount++;

                        _logger.LogDebug("Archivo respaldado. Origen: {Source}, Destino: {Destination}",
                            file, backupPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al respaldar archivo. Archivo: {File}", file);
                    }
                }

                _logger.LogInformation(LOG_INFO_BACKUP_COMPLETED, backedUpCount, backupFolderPath);

                return backedUpCount;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al crear respaldo de configuraciones");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> CleanupOldFilesAsync(int daysToKeepBackups = DEFAULT_BACKUP_RETENTION_DAYS, CancellationToken ct = default)
        {
            if (daysToKeepBackups < 0)
            {
                throw new ArgumentException(EX_MSG_INVALID_RETENTION_DAYS, nameof(daysToKeepBackups));
            }

            _logger.LogInformation("Iniciando limpieza de archivos antiguos. Días de retención: {Days}", daysToKeepBackups);

            try
            {
                if (!Directory.Exists(_dataFolder))
                {
                    _logger.LogDebug("Directorio de datos no existe, no hay archivos para limpiar");
                    return 0;
                }

                var deletedCount = 0;
                var tempFilesDeleted = 0;
                var backupFilesDeleted = 0;

                // Limpiar archivos temporales
                var tempFiles = Directory.GetFiles(_dataFolder, $"*{TEMP_FILE_EXTENSION}");
                foreach (var tempFile in tempFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        File.Delete(tempFile);
                        tempFilesDeleted++;
                        _logger.LogDebug(LOG_DEBUG_TEMP_FILE_CLEANED, tempFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, LOG_WARN_TEMP_FILE_DELETE_FAILED, tempFile, ex.Message);
                    }
                }

                // Limpiar archivos de respaldo antiguos si daysToKeepBackups > 0
                if (daysToKeepBackups > 0)
                {
                    var cutoffDate = DateTime.Now.AddDays(-daysToKeepBackups);
                    var backupFiles = Directory.GetFiles(_dataFolder, $"*{BACKUP_FILE_EXTENSION}*");

                    foreach (var backupFile in backupFiles)
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            var fileInfo = new FileInfo(backupFile);
                            if (fileInfo.LastWriteTime < cutoffDate)
                            {
                                File.Delete(backupFile);
                                backupFilesDeleted++;
                                _logger.LogDebug(LOG_DEBUG_BACKUP_FILE_CLEANED, backupFile);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, LOG_WARN_BACKUP_FILE_DELETE_FAILED, backupFile, ex.Message);
                        }
                    }
                }

                deletedCount = tempFilesDeleted + backupFilesDeleted;

                _logger.LogInformation(LOG_INFO_CLEANUP_COMPLETED, deletedCount, tempFilesDeleted, backupFilesDeleted);

                return deletedCount;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CLEANUP_FILES);
                throw;
            }
        }

        #endregion

        #region Helpers - IO Lectura

        /// <summary>
        /// Lee una lista de cuentas bancarias de forma segura
        /// </summary>
        private async Task<List<CuentaBancaria>> ReadListSafeAsync(
            string filePath,
            string context,
            CancellationToken ct)
        {
            try
            {
                // Recuperar archivo temporal si existe y el principal no
                await TryRecoverTemporaryFileAsync(filePath).ConfigureAwait(false);

                if (!File.Exists(filePath))
                {
                    _logger.LogDebug(LOG_DEBUG_FILE_NOT_FOUND, filePath);
                    return new List<CuentaBancaria>();
                }

                // Verificar integridad del archivo antes de leer
                if (!await ValidateFileIntegrityAsync(filePath, ct).ConfigureAwait(false))
                {
                    _logger.LogWarning(LOG_WARN_INVALID_JSON_STRUCTURE, filePath);
                    return new List<CuentaBancaria>();
                }

                await using var fileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: FILE_BUFFER_SIZE,
                    useAsync: true
                );

                var list = await JsonSerializer.DeserializeAsync<List<CuentaBancaria>>(
                    fileStream,
                    _jsonOptions,
                    ct
                ).ConfigureAwait(false);

                var count = list?.Count ?? 0;

                _logger.LogInformation(LOG_INFO_ACCOUNTS_READ, count, filePath);

                return list ?? new List<CuentaBancaria>();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_READ_FILE, filePath);
                // Fail-safe: devolver lista vacía en caso de error
                return new List<CuentaBancaria>();
            }
        }

        /// <summary>
        /// Valida la integridad del archivo JSON
        /// </summary>
        private async Task<bool> ValidateFileIntegrityAsync(string filePath, CancellationToken ct)
        {
            try
            {
                await using var fileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: FILE_BUFFER_SIZE,
                    useAsync: true
                );

                // Intentar deserializar sin cargar en memoria
                var document = await JsonDocument.ParseAsync(fileStream, cancellationToken: ct)
                    .ConfigureAwait(false);

                using (document)
                {
                    // Validar que sea un array
                    return document.RootElement.ValueKind == JsonValueKind.Array;
                }
            }
            catch (JsonException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al validar integridad del archivo. Archivo: {FilePath}", filePath);
                return false;
            }
        }

        #endregion

        #region Helpers - IO Escritura

        /// <summary>
        /// Escribe una lista de cuentas bancarias de forma segura y atómica
        /// </summary>
        private async Task WriteListSafeAsync(
            string filePath,
            IEnumerable<CuentaBancaria> list,
            string context,
            CancellationToken ct)
        {
            await _writeSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _logger.LogDebug(LOG_DEBUG_SEMAPHORE_ACQUIRED, filePath);

                var tempFilePath = GetTempFilePath(filePath);

                // Limpiar archivo temporal existente
                await CleanupTemporaryFileAsync(tempFilePath).ConfigureAwait(false);

                // Crear respaldo del archivo existente antes de sobrescribir
                if (File.Exists(filePath))
                {
                    await CreateBackupFileAsync(filePath, ct).ConfigureAwait(false);
                }

                // Serializar datos
                var jsonData = JsonSerializer.Serialize(list, _jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(jsonData);

                // Escribir archivo temporal
                await using (var tempStream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: FILE_BUFFER_SIZE,
                    useAsync: true
                ))
                {
                    await tempStream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                    await tempStream.FlushAsync(ct).ConfigureAwait(false);
                }

                // Reemplazar archivo principal de forma atómica
                File.Move(tempFilePath, filePath, overwrite: true);

                _logger.LogInformation(LOG_INFO_ACCOUNTS_SAVED,
                    list.Count(), filePath, bytes.Length);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_WRITE_FILE, filePath);
                throw;
            }
            finally
            {
                _writeSemaphore.Release();
                _logger.LogDebug(LOG_DEBUG_SEMAPHORE_RELEASED, filePath);
            }
        }

        #endregion

        #region Helpers - Archivos Temporales y Respaldo

        /// <summary>
        /// Intenta recuperar un archivo temporal si el principal no existe
        /// </summary>
        private async Task TryRecoverTemporaryFileAsync(string filePath)
        {
            var tempFilePath = GetTempFilePath(filePath);

            if (!File.Exists(filePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Move(tempFilePath, filePath, overwrite: true);
                    _logger.LogInformation(LOG_INFO_TEMP_FILE_RECOVERED, tempFilePath, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, LOG_WARN_TEMP_FILE_RECOVERY_FAILED, tempFilePath, ex.Message);
                }
            }
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
                    _logger.LogDebug(LOG_DEBUG_TEMP_FILE_CLEANED, tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, LOG_WARN_TEMP_FILE_DELETE_FAILED, tempFilePath, ex.Message);
                }
            }
        }

        /// <summary>
        /// Crea un archivo de respaldo con timestamp
        /// </summary>
        private async Task CreateBackupFileAsync(string filePath, CancellationToken ct)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var backupPath = GetBackupFilePath(filePath);

            try
            {
                File.Copy(filePath, backupPath, overwrite: false);
                _logger.LogInformation(LOG_INFO_BACKUP_CREATED, backupPath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, LOG_ERROR_BACKUP_FILE, filePath);
                // No lanzar excepción, el respaldo es opcional
            }
        }

        #endregion

        #region Helpers - Validación

        /// <summary>
        /// Valida que el ID del proveedor no sea nulo o vacío
        /// </summary>
        private static string ValidateProveedorId(string proveedorId)
        {
            if (string.IsNullOrWhiteSpace(proveedorId))
            {
                throw new ArgumentException(EX_MSG_PROVIDER_ID_NULL, nameof(proveedorId));
            }

            return proveedorId.Trim();
        }

        #endregion

        #region Helpers - Sanitización de Nombres

        /// <summary>
        /// Limpia proveedorId para que sea seguro como parte del nombre de archivo.
        /// Caracteres no válidos se reemplazan por '-'.
        /// Si el resultado es muy largo o queda vacío, se usa un hash estable.
        /// </summary>
        private string SanitizeForFileName(string rawInput)
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
            if (string.IsNullOrEmpty(result) || result.Length > MAX_FILENAME_LENGTH)
            {
                var hash = GenerateFileNameHash(input);
                _logger.LogDebug(LOG_DEBUG_HASH_GENERATED, input, hash);
                return hash;
            }

            return result;
        }

        /// <summary>
        /// Genera un hash SHA256 para usar como nombre de archivo
        /// </summary>
        private static string GenerateFileNameHash(string input)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
            return Convert.ToHexString(hashBytes).ToLowerInvariant()[..HASH_SUBSTRING_LENGTH];
        }

        #endregion
    }

    #endregion
}