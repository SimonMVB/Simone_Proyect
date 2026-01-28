using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Simone.Configuration;

namespace Simone.Services
{
    #region Interface

    /// <summary>
    /// Servicio de resolución de tarifas de envío con lógica de prioridad
    /// Resuelve tarifas por vendedor y destino con fallback a configuración de administrador
    /// Prioridad: (Vendedor+Ciudad) -> (Vendedor+Provincia) -> (Admin+Ciudad) -> (Admin+Provincia)
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface IEnviosResolver
    {
        #region Resolución de Tarifas

        /// <summary>
        /// Resuelve la tarifa de envío para un vendedor y destino
        /// </summary>
        /// <param name="vendedorId">ID del vendedor</param>
        /// <param name="provincia">Provincia de destino</param>
        /// <param name="ciudad">Ciudad de destino (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Precio de envío o null si no hay tarifa configurada</returns>
        Task<decimal?> GetTarifaAsync(string vendedorId, string provincia, string? ciudad, CancellationToken ct = default);

        /// <summary>
        /// Alias de GetTarifaAsync para compatibilidad
        /// </summary>
        Task<decimal?> GetCostoAsync(string vendedorId, string provincia, string? ciudad, CancellationToken ct = default);

        /// <summary>
        /// Resuelve la tarifa con información detallada sobre cómo se resolvió
        /// </summary>
        /// <param name="vendedorId">ID del vendedor</param>
        /// <param name="provincia">Provincia de destino</param>
        /// <param name="ciudad">Ciudad de destino (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        Task<TarifaResuelta> GetTarifaDetalladaAsync(string vendedorId, string provincia, string? ciudad, CancellationToken ct = default);

        #endregion

        #region Validación

        /// <summary>
        /// Valida si existe tarifa configurada para un vendedor y destino
        /// </summary>
        /// <param name="vendedorId">ID del vendedor</param>
        /// <param name="provincia">Provincia de destino</param>
        /// <param name="ciudad">Ciudad de destino (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> ValidateTarifaExistsAsync(string vendedorId, string provincia, string? ciudad, CancellationToken ct = default);

        #endregion

        #region Consultas

        /// <summary>
        /// Obtiene todas las tarifas activas de un vendedor
        /// </summary>
        /// <param name="vendedorId">ID del vendedor</param>
        /// <param name="ct">Token de cancelación</param>
        Task<IReadOnlyList<TarifaEnvioRegla>> GetAllTarifasForVendedorAsync(string vendedorId, CancellationToken ct = default);

        /// <summary>
        /// Obtiene información de debugging sobre el proceso de resolución
        /// </summary>
        /// <param name="vendedorId">ID del vendedor</param>
        /// <param name="provincia">Provincia de destino</param>
        /// <param name="ciudad">Ciudad de destino (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        Task<TarifaDebugInfo> DebugResolvePathAsync(string vendedorId, string provincia, string? ciudad, CancellationToken ct = default);

        #endregion

        #region Utilidades

        /// <summary>
        /// Normaliza texto para comparación (trim, lowercase, sin acentos)
        /// </summary>
        /// <param name="texto">Texto a normalizar</param>
        string NormalizarTexto(string? texto);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del resolver de tarifas de envío con logging y validación robusta
    /// </summary>
    public class EnviosResolver : IEnviosResolver
    {
        #region Dependencias

        private readonly IEnviosConfigService _envios;
        private readonly ILogger<EnviosResolver> _logger;

        #endregion

        #region Constantes - Configuración

        private const int MIN_PROVINCIA_LENGTH = 2;
        private const int MAX_PROVINCIA_LENGTH = 100;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_TARIFA_ENCONTRADA = "Tarifa encontrada. VendedorId: {VendedorId}, Provincia: {Provincia}, Ciudad: {Ciudad}, Precio: {Precio:C}, Fuente: {Fuente}, Nivel: {Nivel}";
        private const string LOG_INFO_TARIFA_NO_ENCONTRADA = "Tarifa no encontrada. VendedorId: {VendedorId}, Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_INFO_USANDO_FALLBACK_ADMIN = "Usando fallback a reglas de administrador. VendedorId: {VendedorId}";

        // Debug
        private const string LOG_DEBUG_INICIANDO_RESOLUCION = "Iniciando resolución de tarifa. VendedorId: {VendedorId}, Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_DEBUG_PROVINCIA_NORMALIZADA = "Provincia normalizada. Original: {Original}, Normalizada: {Normalizada}";
        private const string LOG_DEBUG_CIUDAD_NORMALIZADA = "Ciudad normalizada. Original: {Original}, Normalizada: {Normalizada}";
        private const string LOG_DEBUG_REGLAS_VENDEDOR_CARGADAS = "Reglas de vendedor cargadas. VendedorId: {VendedorId}, Total: {Total}, Activas: {Activas}";
        private const string LOG_DEBUG_REGLAS_ADMIN_CARGADAS = "Reglas de administrador cargadas. Total: {Total}, Activas: {Activas}";
        private const string LOG_DEBUG_BUSCANDO_POR_CIUDAD = "Buscando tarifa por ciudad. Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_DEBUG_BUSCANDO_POR_PROVINCIA = "Buscando tarifa por provincia. Provincia: {Provincia}";
        private const string LOG_DEBUG_REGLA_ENCONTRADA = "Regla encontrada. Provincia: {Provincia}, Ciudad: {Ciudad}, Precio: {Precio:C}, Activo: {Activo}";
        private const string LOG_DEBUG_VALIDANDO_EXISTENCIA = "Validando existencia de tarifa. VendedorId: {VendedorId}, Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_DEBUG_OBTENIENDO_TODAS_TARIFAS = "Obteniendo todas las tarifas. VendedorId: {VendedorId}";

        // Advertencias
        private const string LOG_WARN_VENDEDOR_ID_VACIO = "VendedorId vacío o nulo en GetTarifaAsync";
        private const string LOG_WARN_PROVINCIA_VACIA = "Provincia vacía o nula. VendedorId: {VendedorId}";
        private const string LOG_WARN_PROVINCIA_INVALIDA = "Provincia con longitud inválida. Provincia: {Provincia}, Longitud: {Length}";
        private const string LOG_WARN_SIN_REGLAS_VENDEDOR = "Vendedor sin reglas configuradas. VendedorId: {VendedorId}";
        private const string LOG_WARN_SIN_REGLAS_ADMIN = "Sin reglas de administrador configuradas";
        private const string LOG_WARN_TODAS_REGLAS_INACTIVAS = "Todas las reglas están inactivas. VendedorId: {VendedorId}, Total: {Total}";

        // Errores
        private const string LOG_ERROR_RESOLVER_TARIFA = "Error al resolver tarifa. VendedorId: {VendedorId}, Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_ERROR_OBTENER_REGLAS_VENDEDOR = "Error al obtener reglas de vendedor. VendedorId: {VendedorId}";
        private const string LOG_ERROR_OBTENER_REGLAS_ADMIN = "Error al obtener reglas de administrador";
        private const string LOG_ERROR_VALIDAR_EXISTENCIA = "Error al validar existencia de tarifa. VendedorId: {VendedorId}";
        private const string LOG_ERROR_OBTENER_TODAS_TARIFAS = "Error al obtener todas las tarifas. VendedorId: {VendedorId}";
        private const string LOG_ERROR_DEBUG_RESOLVE = "Error en debug de resolución. VendedorId: {VendedorId}";

        #endregion

        #region Constantes - Mensajes de Excepción

        private const string EXC_VENDEDOR_ID_NULL = "El ID del vendedor no puede ser nulo o vacío";
        private const string EXC_PROVINCIA_NULL = "La provincia no puede ser nula o vacía";
        private const string EXC_PROVINCIA_LENGTH = "La provincia debe tener entre {0} y {1} caracteres";

        #endregion

        #region Constantes - Fuentes de Tarifa

        private const string FUENTE_VENDEDOR = "Vendedor";
        private const string FUENTE_ADMIN = "Administrador";
        private const string NIVEL_CIUDAD = "Ciudad";
        private const string NIVEL_PROVINCIA = "Provincia";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del resolver de envíos
        /// </summary>
        /// <param name="envios">Servicio de configuración de envíos</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public EnviosResolver(IEnviosConfigService envios, ILogger<EnviosResolver> logger)
        {
            _envios = envios ?? throw new ArgumentNullException(nameof(envios));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Helpers - Validación

        /// <summary>
        /// Valida que el vendedorId no esté vacío
        /// </summary>
        private void ValidateVendedorId(string vendedorId)
        {
            if (string.IsNullOrWhiteSpace(vendedorId))
            {
                _logger.LogWarning(LOG_WARN_VENDEDOR_ID_VACIO);
                throw new ArgumentException(EXC_VENDEDOR_ID_NULL, nameof(vendedorId));
            }
        }

        /// <summary>
        /// Valida que la provincia no esté vacía
        /// </summary>
        private void ValidateProvincia(string provincia)
        {
            if (string.IsNullOrWhiteSpace(provincia))
            {
                _logger.LogWarning(LOG_WARN_PROVINCIA_VACIA, "N/A");
                throw new ArgumentException(EXC_PROVINCIA_NULL, nameof(provincia));
            }

            var trimmed = provincia.Trim();
            if (trimmed.Length < MIN_PROVINCIA_LENGTH || trimmed.Length > MAX_PROVINCIA_LENGTH)
            {
                _logger.LogWarning(LOG_WARN_PROVINCIA_INVALIDA, provincia, trimmed.Length);
                throw new ArgumentException(
                    string.Format(EXC_PROVINCIA_LENGTH, MIN_PROVINCIA_LENGTH, MAX_PROVINCIA_LENGTH),
                    nameof(provincia));
            }
        }

        #endregion

        #region Helpers - Normalización

        /// <inheritdoc />
        public string NormalizarTexto(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            var trimmed = texto.Trim().ToLowerInvariant();
            var normalized = trimmed.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        #endregion

        #region Helpers - Carga de Reglas

        /// <summary>
        /// Carga y filtra las reglas activas de un vendedor
        /// </summary>
        private async Task<List<TarifaEnvioRegla>> CargarReglasVendedorAsync(string vendedorId, CancellationToken ct)
        {
            try
            {
                var reglas = await _envios.GetByProveedorAsync(vendedorId, ct).ConfigureAwait(false);
                var reglasActivas = reglas?.Where(r => r.Activo).ToList() ?? new List<TarifaEnvioRegla>();

                _logger.LogDebug(LOG_DEBUG_REGLAS_VENDEDOR_CARGADAS,
                    vendedorId, reglas?.Count ?? 0, reglasActivas.Count);

                if (reglas != null && reglas.Count > 0 && reglasActivas.Count == 0)
                {
                    _logger.LogWarning(LOG_WARN_TODAS_REGLAS_INACTIVAS, vendedorId, reglas.Count);
                }
                else if (reglasActivas.Count == 0)
                {
                    _logger.LogWarning(LOG_WARN_SIN_REGLAS_VENDEDOR, vendedorId);
                }

                return reglasActivas;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_REGLAS_VENDEDOR, vendedorId);
                return new List<TarifaEnvioRegla>();
            }
        }

        /// <summary>
        /// Carga y filtra las reglas activas del administrador
        /// </summary>
        private async Task<List<TarifaEnvioRegla>> CargarReglasAdminAsync(CancellationToken ct)
        {
            try
            {
                var reglas = await _envios.GetAdminAsync(ct).ConfigureAwait(false);
                var reglasActivas = reglas?.Where(r => r.Activo).ToList() ?? new List<TarifaEnvioRegla>();

                _logger.LogDebug(LOG_DEBUG_REGLAS_ADMIN_CARGADAS,
                    reglas?.Count ?? 0, reglasActivas.Count);

                if (reglasActivas.Count == 0)
                {
                    _logger.LogWarning(LOG_WARN_SIN_REGLAS_ADMIN);
                }

                return reglasActivas;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_REGLAS_ADMIN);
                return new List<TarifaEnvioRegla>();
            }
        }

        #endregion

        #region Helpers - Búsqueda de Tarifas

        /// <summary>
        /// Busca una tarifa por provincia y ciudad
        /// </summary>
        private TarifaEnvioRegla? BuscarTarifaPorCiudad(
            List<TarifaEnvioRegla> reglas,
            string provinciaNormalizada,
            string ciudadNormalizada)
        {
            if (string.IsNullOrEmpty(ciudadNormalizada))
            {
                return null;
            }

            _logger.LogDebug(LOG_DEBUG_BUSCANDO_POR_CIUDAD, provinciaNormalizada, ciudadNormalizada);

            var regla = reglas.FirstOrDefault(r =>
                NormalizarTexto(r.Provincia) == provinciaNormalizada &&
                !string.IsNullOrWhiteSpace(r.Ciudad) &&
                NormalizarTexto(r.Ciudad) == ciudadNormalizada
            );

            if (regla != null)
            {
                _logger.LogDebug(LOG_DEBUG_REGLA_ENCONTRADA,
                    regla.Provincia, regla.Ciudad ?? "N/A", regla.Precio, regla.Activo);
            }

            return regla;
        }

        /// <summary>
        /// Busca una tarifa por provincia (sin ciudad específica)
        /// </summary>
        private TarifaEnvioRegla? BuscarTarifaPorProvincia(
            List<TarifaEnvioRegla> reglas,
            string provinciaNormalizada)
        {
            _logger.LogDebug(LOG_DEBUG_BUSCANDO_POR_PROVINCIA, provinciaNormalizada);

            var regla = reglas.FirstOrDefault(r =>
                NormalizarTexto(r.Provincia) == provinciaNormalizada &&
                string.IsNullOrWhiteSpace(r.Ciudad)
            );

            if (regla != null)
            {
                _logger.LogDebug(LOG_DEBUG_REGLA_ENCONTRADA,
                    regla.Provincia, regla.Ciudad ?? "N/A", regla.Precio, regla.Activo);
            }

            return regla;
        }

        #endregion

        #region Resolución de Tarifas

        /// <inheritdoc />
        public async Task<decimal?> GetTarifaAsync(
            string vendedorId,
            string provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vendedorId) || string.IsNullOrWhiteSpace(provincia))
            {
                return null;
            }

            try
            {
                _logger.LogDebug(LOG_DEBUG_INICIANDO_RESOLUCION, vendedorId, provincia, ciudad ?? "null");

                var provinciaOriginal = provincia;
                var ciudadOriginal = ciudad;

                var provinciaNorm = NormalizarTexto(provincia);
                var ciudadNorm = NormalizarTexto(ciudad);

                _logger.LogDebug(LOG_DEBUG_PROVINCIA_NORMALIZADA, provinciaOriginal, provinciaNorm);
                if (!string.IsNullOrEmpty(ciudadOriginal))
                {
                    _logger.LogDebug(LOG_DEBUG_CIUDAD_NORMALIZADA, ciudadOriginal, ciudadNorm);
                }

                // 1. Reglas del vendedor
                var reglasVendedor = await CargarReglasVendedorAsync(vendedorId, ct).ConfigureAwait(false);

                // 1a. Coincidencia por ciudad (vendedor)
                var reglaCiudadVendedor = BuscarTarifaPorCiudad(reglasVendedor, provinciaNorm, ciudadNorm);
                if (reglaCiudadVendedor != null)
                {
                    _logger.LogInformation(LOG_INFO_TARIFA_ENCONTRADA,
                        vendedorId, provincia, ciudad ?? "N/A",
                        reglaCiudadVendedor.Precio, FUENTE_VENDEDOR, NIVEL_CIUDAD);
                    return reglaCiudadVendedor.Precio;
                }

                // 1b. Coincidencia por provincia (vendedor)
                var reglaProvinciaVendedor = BuscarTarifaPorProvincia(reglasVendedor, provinciaNorm);
                if (reglaProvinciaVendedor != null)
                {
                    _logger.LogInformation(LOG_INFO_TARIFA_ENCONTRADA,
                        vendedorId, provincia, ciudad ?? "N/A",
                        reglaProvinciaVendedor.Precio, FUENTE_VENDEDOR, NIVEL_PROVINCIA);
                    return reglaProvinciaVendedor.Precio;
                }

                // 2. Fallback a reglas de admin
                _logger.LogInformation(LOG_INFO_USANDO_FALLBACK_ADMIN, vendedorId);
                var reglasAdmin = await CargarReglasAdminAsync(ct).ConfigureAwait(false);

                // 2a. Coincidencia por ciudad (admin)
                var reglaCiudadAdmin = BuscarTarifaPorCiudad(reglasAdmin, provinciaNorm, ciudadNorm);
                if (reglaCiudadAdmin != null)
                {
                    _logger.LogInformation(LOG_INFO_TARIFA_ENCONTRADA,
                        vendedorId, provincia, ciudad ?? "N/A",
                        reglaCiudadAdmin.Precio, FUENTE_ADMIN, NIVEL_CIUDAD);
                    return reglaCiudadAdmin.Precio;
                }

                // 2b. Coincidencia por provincia (admin)
                var reglaProvinciaAdmin = BuscarTarifaPorProvincia(reglasAdmin, provinciaNorm);
                if (reglaProvinciaAdmin != null)
                {
                    _logger.LogInformation(LOG_INFO_TARIFA_ENCONTRADA,
                        vendedorId, provincia, ciudad ?? "N/A",
                        reglaProvinciaAdmin.Precio, FUENTE_ADMIN, NIVEL_PROVINCIA);
                    return reglaProvinciaAdmin.Precio;
                }

                _logger.LogInformation(LOG_INFO_TARIFA_NO_ENCONTRADA, vendedorId, provincia, ciudad ?? "null");
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_RESOLVER_TARIFA, vendedorId, provincia, ciudad ?? "null");
                return null;
            }
        }

        /// <inheritdoc />
        public Task<decimal?> GetCostoAsync(
            string vendedorId,
            string provincia,
            string? ciudad,
            CancellationToken ct = default)
            => GetTarifaAsync(vendedorId, provincia, ciudad, ct);

        /// <inheritdoc />
        public async Task<TarifaResuelta> GetTarifaDetalladaAsync(
            string vendedorId,
            string provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            var resultado = new TarifaResuelta
            {
                VendedorId = vendedorId,
                Provincia = provincia,
                Ciudad = ciudad
            };

            if (string.IsNullOrWhiteSpace(vendedorId) || string.IsNullOrWhiteSpace(provincia))
            {
                return resultado;
            }

            try
            {
                var provinciaNorm = NormalizarTexto(provincia);
                var ciudadNorm = NormalizarTexto(ciudad);

                // Cargar reglas
                var reglasVendedor = await CargarReglasVendedorAsync(vendedorId, ct).ConfigureAwait(false);
                resultado.ReglasVendedorTotal = reglasVendedor.Count;

                var reglasAdmin = await CargarReglasAdminAsync(ct).ConfigureAwait(false);
                resultado.ReglasAdminTotal = reglasAdmin.Count;

                // Buscar en vendedor
                var reglaCiudadVendedor = BuscarTarifaPorCiudad(reglasVendedor, provinciaNorm, ciudadNorm);
                if (reglaCiudadVendedor != null)
                {
                    resultado.Precio = reglaCiudadVendedor.Precio;
                    resultado.Encontrada = true;
                    resultado.Fuente = FUENTE_VENDEDOR;
                    resultado.Nivel = NIVEL_CIUDAD;
                    resultado.ReglaAplicada = reglaCiudadVendedor;
                    return resultado;
                }

                var reglaProvinciaVendedor = BuscarTarifaPorProvincia(reglasVendedor, provinciaNorm);
                if (reglaProvinciaVendedor != null)
                {
                    resultado.Precio = reglaProvinciaVendedor.Precio;
                    resultado.Encontrada = true;
                    resultado.Fuente = FUENTE_VENDEDOR;
                    resultado.Nivel = NIVEL_PROVINCIA;
                    resultado.ReglaAplicada = reglaProvinciaVendedor;
                    return resultado;
                }

                // Buscar en admin
                resultado.UsaFallbackAdmin = true;

                var reglaCiudadAdmin = BuscarTarifaPorCiudad(reglasAdmin, provinciaNorm, ciudadNorm);
                if (reglaCiudadAdmin != null)
                {
                    resultado.Precio = reglaCiudadAdmin.Precio;
                    resultado.Encontrada = true;
                    resultado.Fuente = FUENTE_ADMIN;
                    resultado.Nivel = NIVEL_CIUDAD;
                    resultado.ReglaAplicada = reglaCiudadAdmin;
                    return resultado;
                }

                var reglaProvinciaAdmin = BuscarTarifaPorProvincia(reglasAdmin, provinciaNorm);
                if (reglaProvinciaAdmin != null)
                {
                    resultado.Precio = reglaProvinciaAdmin.Precio;
                    resultado.Encontrada = true;
                    resultado.Fuente = FUENTE_ADMIN;
                    resultado.Nivel = NIVEL_PROVINCIA;
                    resultado.ReglaAplicada = reglaProvinciaAdmin;
                    return resultado;
                }

                return resultado;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_RESOLVER_TARIFA, vendedorId, provincia, ciudad ?? "null");
                return resultado;
            }
        }

        #endregion

        #region Validación

        /// <inheritdoc />
        public async Task<bool> ValidateTarifaExistsAsync(
            string vendedorId,
            string provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vendedorId) || string.IsNullOrWhiteSpace(provincia))
            {
                return false;
            }

            try
            {
                _logger.LogDebug(LOG_DEBUG_VALIDANDO_EXISTENCIA, vendedorId, provincia, ciudad ?? "null");

                var tarifa = await GetTarifaAsync(vendedorId, provincia, ciudad, ct).ConfigureAwait(false);

                return tarifa.HasValue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VALIDAR_EXISTENCIA, vendedorId);
                return false;
            }
        }

        #endregion

        #region Consultas

        /// <inheritdoc />
        public async Task<IReadOnlyList<TarifaEnvioRegla>> GetAllTarifasForVendedorAsync(
            string vendedorId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vendedorId))
            {
                return Array.Empty<TarifaEnvioRegla>();
            }

            try
            {
                _logger.LogDebug(LOG_DEBUG_OBTENIENDO_TODAS_TARIFAS, vendedorId);

                var reglas = await CargarReglasVendedorAsync(vendedorId, ct).ConfigureAwait(false);

                return reglas.AsReadOnly();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_OBTENER_TODAS_TARIFAS, vendedorId);
                return Array.Empty<TarifaEnvioRegla>();
            }
        }

        /// <inheritdoc />
        public async Task<TarifaDebugInfo> DebugResolvePathAsync(
            string vendedorId,
            string provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            var debug = new TarifaDebugInfo
            {
                VendedorId = vendedorId,
                Provincia = provincia,
                Ciudad = ciudad
            };

            if (string.IsNullOrWhiteSpace(vendedorId) || string.IsNullOrWhiteSpace(provincia))
            {
                debug.Pasos.Add("❌ VendedorId o Provincia vacíos");
                return debug;
            }

            try
            {
                var provinciaNorm = NormalizarTexto(provincia);
                var ciudadNorm = NormalizarTexto(ciudad);

                debug.ProvinciaNormalizada = provinciaNorm;
                debug.CiudadNormalizada = ciudadNorm;

                debug.Pasos.Add($"✓ Provincia normalizada: '{provincia}' -> '{provinciaNorm}'");
                if (!string.IsNullOrEmpty(ciudad))
                {
                    debug.Pasos.Add($"✓ Ciudad normalizada: '{ciudad}' -> '{ciudadNorm}'");
                }

                // Cargar reglas vendedor
                var reglasVendedor = await CargarReglasVendedorAsync(vendedorId, ct).ConfigureAwait(false);
                debug.Pasos.Add($"✓ Reglas de vendedor cargadas: {reglasVendedor.Count} activas");

                // Buscar en vendedor
                var reglaCiudadVendedor = BuscarTarifaPorCiudad(reglasVendedor, provinciaNorm, ciudadNorm);
                if (reglaCiudadVendedor != null)
                {
                    debug.Pasos.Add($"✓ Encontrada en vendedor por ciudad: {reglaCiudadVendedor.Precio:C}");
                    debug.TarifaEncontrada = reglaCiudadVendedor.Precio;
                    debug.Fuente = FUENTE_VENDEDOR;
                    debug.Nivel = NIVEL_CIUDAD;
                    return debug;
                }
                else
                {
                    debug.Pasos.Add("✗ No encontrada en vendedor por ciudad");
                }

                var reglaProvinciaVendedor = BuscarTarifaPorProvincia(reglasVendedor, provinciaNorm);
                if (reglaProvinciaVendedor != null)
                {
                    debug.Pasos.Add($"✓ Encontrada en vendedor por provincia: {reglaProvinciaVendedor.Precio:C}");
                    debug.TarifaEncontrada = reglaProvinciaVendedor.Precio;
                    debug.Fuente = FUENTE_VENDEDOR;
                    debug.Nivel = NIVEL_PROVINCIA;
                    return debug;
                }
                else
                {
                    debug.Pasos.Add("✗ No encontrada en vendedor por provincia");
                }

                // Cargar reglas admin
                debug.Pasos.Add("→ Intentando fallback a administrador");
                var reglasAdmin = await CargarReglasAdminAsync(ct).ConfigureAwait(false);
                debug.Pasos.Add($"✓ Reglas de administrador cargadas: {reglasAdmin.Count} activas");

                // Buscar en admin
                var reglaCiudadAdmin = BuscarTarifaPorCiudad(reglasAdmin, provinciaNorm, ciudadNorm);
                if (reglaCiudadAdmin != null)
                {
                    debug.Pasos.Add($"✓ Encontrada en admin por ciudad: {reglaCiudadAdmin.Precio:C}");
                    debug.TarifaEncontrada = reglaCiudadAdmin.Precio;
                    debug.Fuente = FUENTE_ADMIN;
                    debug.Nivel = NIVEL_CIUDAD;
                    return debug;
                }
                else
                {
                    debug.Pasos.Add("✗ No encontrada en admin por ciudad");
                }

                var reglaProvinciaAdmin = BuscarTarifaPorProvincia(reglasAdmin, provinciaNorm);
                if (reglaProvinciaAdmin != null)
                {
                    debug.Pasos.Add($"✓ Encontrada en admin por provincia: {reglaProvinciaAdmin.Precio:C}");
                    debug.TarifaEncontrada = reglaProvinciaAdmin.Precio;
                    debug.Fuente = FUENTE_ADMIN;
                    debug.Nivel = NIVEL_PROVINCIA;
                    return debug;
                }
                else
                {
                    debug.Pasos.Add("✗ No encontrada en admin por provincia");
                }

                debug.Pasos.Add("❌ No se encontró ninguna tarifa configurada");
                return debug;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_DEBUG_RESOLVE, vendedorId);
                debug.Pasos.Add($"❌ Error: {ex.Message}");
                return debug;
            }
        }

        #endregion
    }

    #endregion

    #region DTOs

    /// <summary>
    /// Resultado detallado de la resolución de tarifa
    /// </summary>
    public class TarifaResuelta
    {
        /// <summary>
        /// ID del vendedor
        /// </summary>
        public string VendedorId { get; set; }

        /// <summary>
        /// Provincia de destino
        /// </summary>
        public string Provincia { get; set; }

        /// <summary>
        /// Ciudad de destino
        /// </summary>
        public string? Ciudad { get; set; }

        /// <summary>
        /// Precio encontrado
        /// </summary>
        public decimal? Precio { get; set; }

        /// <summary>
        /// Indica si se encontró una tarifa
        /// </summary>
        public bool Encontrada { get; set; }

        /// <summary>
        /// Fuente de la tarifa (Vendedor/Administrador)
        /// </summary>
        public string? Fuente { get; set; }

        /// <summary>
        /// Nivel de coincidencia (Ciudad/Provincia)
        /// </summary>
        public string? Nivel { get; set; }

        /// <summary>
        /// Regla aplicada
        /// </summary>
        public TarifaEnvioRegla? ReglaAplicada { get; set; }

        /// <summary>
        /// Indica si se usó fallback a administrador
        /// </summary>
        public bool UsaFallbackAdmin { get; set; }

        /// <summary>
        /// Total de reglas del vendedor
        /// </summary>
        public int ReglasVendedorTotal { get; set; }

        /// <summary>
        /// Total de reglas del administrador
        /// </summary>
        public int ReglasAdminTotal { get; set; }
    }

    /// <summary>
    /// Información de debugging del proceso de resolución
    /// </summary>
    public class TarifaDebugInfo
    {
        /// <summary>
        /// ID del vendedor
        /// </summary>
        public string VendedorId { get; set; }

        /// <summary>
        /// Provincia original
        /// </summary>
        public string Provincia { get; set; }

        /// <summary>
        /// Ciudad original
        /// </summary>
        public string? Ciudad { get; set; }

        /// <summary>
        /// Provincia normalizada
        /// </summary>
        public string ProvinciaNormalizada { get; set; }

        /// <summary>
        /// Ciudad normalizada
        /// </summary>
        public string? CiudadNormalizada { get; set; }

        /// <summary>
        /// Tarifa encontrada
        /// </summary>
        public decimal? TarifaEncontrada { get; set; }

        /// <summary>
        /// Fuente de la tarifa
        /// </summary>
        public string? Fuente { get; set; }

        /// <summary>
        /// Nivel de coincidencia
        /// </summary>
        public string? Nivel { get; set; }

        /// <summary>
        /// Pasos de la resolución
        /// </summary>
        public List<string> Pasos { get; } = new();
    }

    #endregion
}