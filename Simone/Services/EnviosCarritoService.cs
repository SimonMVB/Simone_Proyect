using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Simone.Models;

namespace Simone.Services
{
    #region Interface

    /// <summary>
    /// Servicio de cálculo de costos de envío agrupados por vendedor
    /// Implementa lógica de resolución por provincia/ciudad con prioridades
    /// Versión optimizada con mejores prácticas empresariales
    /// </summary>
    public interface IEnviosCarritoService
    {
        #region Cálculo de Envíos

        /// <summary>
        /// Calcula el costo total de envío y el detalle por vendedor
        /// </summary>
        /// <param name="vendedorIds">IDs de vendedores en el carrito (se eliminan duplicados automáticamente)</param>
        /// <param name="provincia">Provincia de destino</param>
        /// <param name="ciudad">Ciudad de destino (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Resultado con total, detalle por vendedor y mensajes informativos</returns>
        Task<EnvioResultado> CalcularAsync(
            IEnumerable<string> vendedorIds,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default);

        /// <summary>
        /// Calcula el costo de envío usando la dirección del usuario
        /// </summary>
        /// <param name="vendedorIds">IDs de vendedores en el carrito</param>
        /// <param name="usuario">Usuario con provincia y ciudad en su perfil</param>
        /// <param name="ct">Token de cancelación</param>
        Task<EnvioResultado> CalcularParaUsuarioAsync(
            IEnumerable<string> vendedorIds,
            Usuario? usuario,
            CancellationToken ct = default);

        /// <summary>
        /// Calcula el costo de envío para un solo vendedor
        /// </summary>
        /// <param name="vendedorId">ID del vendedor</param>
        /// <param name="provincia">Provincia de destino</param>
        /// <param name="ciudad">Ciudad de destino (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        Task<decimal?> CalcularParaVendedorAsync(
            string vendedorId,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default);

        #endregion

        #region Validación

        /// <summary>
        /// Verifica si todos los vendedores tienen tarifa configurada para un destino
        /// </summary>
        /// <param name="vendedorIds">IDs de vendedores a verificar</param>
        /// <param name="provincia">Provincia de destino</param>
        /// <param name="ciudad">Ciudad de destino (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        /// <returns>Tupla con resultado y lista de vendedores sin tarifa</returns>
        Task<(bool todosConfigurados, List<string> vendedoresSinTarifa)> ValidarTarifasAsync(
            IEnumerable<string> vendedorIds,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default);

        /// <summary>
        /// Verifica si un vendedor tiene tarifa configurada para un destino
        /// </summary>
        /// <param name="vendedorId">ID del vendedor</param>
        /// <param name="provincia">Provincia de destino</param>
        /// <param name="ciudad">Ciudad de destino (opcional)</param>
        /// <param name="ct">Token de cancelación</param>
        Task<bool> TieneTarifaConfiguradaAsync(
            string vendedorId,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default);

        #endregion

        #region Estadísticas

        /// <summary>
        /// Obtiene estadísticas del cálculo de envío
        /// </summary>
        /// <param name="resultado">Resultado del cálculo</param>
        EnvioEstadisticas ObtenerEstadisticas(EnvioResultado resultado);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Implementación del servicio de cálculo de envíos con logging y validación robusta
    /// Calcula costos agrupando por vendedor con reglas de prioridad (Provincia+Ciudad) -> (Provincia)
    /// Máximo UNA tarifa por vendedor, no por ítem
    /// </summary>
    public class EnviosCarritoService : IEnviosCarritoService
    {
        #region Dependencias

        private readonly EnviosResolver _resolver;
        private readonly ILogger<EnviosCarritoService> _logger;

        #endregion

        #region Constantes - Configuración

        private const decimal COSTO_MINIMO_VALIDO = 0m;
        private const int MAX_VENDEDORES_POR_CALCULO = 100;

        #endregion

        #region Constantes - Mensajes de Log

        // Información
        private const string LOG_INFO_CALCULO_INICIADO = "Iniciando cálculo de envío. Vendedores: {VendedoresCount}, Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_INFO_CALCULO_COMPLETADO = "Cálculo completado. Total: {Total:C}, VendedoresConfigurados: {Configurados}/{Total}, VendedoresSinTarifa: {SinTarifa}";
        private const string LOG_INFO_VENDEDOR_PROCESADO = "Vendedor procesado. VendedorId: {VendedorId}, Costo: {Costo:C}, TieneTarifa: {TieneTarifa}";
        private const string LOG_INFO_VALIDACION_TARIFAS = "Validación de tarifas. TotalVendedores: {Total}, Configurados: {Configurados}, SinTarifa: {SinTarifa}";

        // Debug
        private const string LOG_DEBUG_VENDEDORES_DISTINTOS = "Vendedores únicos después de Distinct: {Count}";
        private const string LOG_DEBUG_PROVINCIA_NORMALIZADA = "Provincia normalizada: '{Original}' -> '{Normalizada}'";
        private const string LOG_DEBUG_CIUDAD_NORMALIZADA = "Ciudad normalizada: '{Original}' -> '{Normalizada}'";
        private const string LOG_DEBUG_COSTO_RESOLVER = "Llamando a resolver. VendedorId: {VendedorId}, Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_DEBUG_COSTO_OBTENIDO = "Costo obtenido del resolver. VendedorId: {VendedorId}, Costo: {Costo}, EsNulo: {EsNulo}";
        private const string LOG_DEBUG_VENDEDOR_SIN_TARIFA = "Vendedor sin tarifa configurada. VendedorId: {VendedorId}, Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_DEBUG_DIRECCION_USUARIO = "Usando dirección de usuario. UsuarioId: {UsuarioId}, Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_DEBUG_ESTADISTICAS = "Estadísticas calculadas. Total: {Total:C}, Promedio: {Promedio:C}, Max: {Max:C}, Min: {Min:C}";

        // Advertencias
        private const string LOG_WARN_VENDEDORES_NULL = "Lista de vendedores es null, retornando resultado vacío";
        private const string LOG_WARN_VENDEDORES_VACIOS = "Lista de vendedores está vacía después de filtrar, retornando resultado vacío";
        private const string LOG_WARN_PROVINCIA_VACIA = "Provincia vacía o null. Provincia: '{Provincia}'";
        private const string LOG_WARN_USUARIO_NULL = "Usuario es null en CalcularParaUsuarioAsync";
        private const string LOG_WARN_USUARIO_SIN_DIRECCION = "Usuario sin provincia configurada. UsuarioId: {UsuarioId}";
        private const string LOG_WARN_VENDEDOR_ID_VACIO = "VendedorId vacío o null ignorado en cálculo";
        private const string LOG_WARN_COSTO_NEGATIVO = "Costo negativo obtenido del resolver. VendedorId: {VendedorId}, Costo: {Costo}";
        private const string LOG_WARN_MUCHOS_VENDEDORES = "Número alto de vendedores en un solo cálculo. Count: {Count}, Máximo recomendado: {Max}";

        // Errores
        private const string LOG_ERROR_CALCULAR = "Error al calcular envío. Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_ERROR_CALCULAR_VENDEDOR = "Error al calcular envío para vendedor. VendedorId: {VendedorId}";
        private const string LOG_ERROR_VALIDAR_TARIFAS = "Error al validar tarifas. Provincia: {Provincia}, Ciudad: {Ciudad}";
        private const string LOG_ERROR_RESOLVER = "Error al llamar al resolver para vendedor. VendedorId: {VendedorId}";
        private const string LOG_ERROR_ESTADISTICAS = "Error al calcular estadísticas del resultado";

        #endregion

        #region Constantes - Mensajes de Error

        private const string ERR_RESOLVER_NULL = "El resolver de envíos no puede ser nulo";
        private const string ERR_LOGGER_NULL = "El logger no puede ser nulo";
        private const string ERR_VENDEDOR_ID_VACIO = "El ID del vendedor no puede estar vacío";
        private const string ERR_RESULTADO_NULL = "El resultado no puede ser nulo";

        #endregion

        #region Constantes - Mensajes de Usuario

        private const string MSG_VENDEDOR_SIN_TARIFA = "El vendedor {0} no tiene tarifa configurada para {1}{2}.";
        private const string MSG_VENDEDOR_ERROR = "No se pudo calcular el envío para el vendedor {0}.";

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor del servicio de envíos de carrito
        /// </summary>
        /// <param name="resolver">Resolver de tarifas de envío</param>
        /// <param name="logger">Logger para registro de eventos</param>
        /// <exception cref="ArgumentNullException">Si alguna dependencia es null</exception>
        public EnviosCarritoService(EnviosResolver resolver, ILogger<EnviosCarritoService> logger)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver), ERR_RESOLVER_NULL);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), ERR_LOGGER_NULL);
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
                throw new ArgumentException(ERR_VENDEDOR_ID_VACIO, nameof(vendedorId));
            }
        }

        /// <summary>
        /// Valida que el resultado no sea nulo
        /// </summary>
        private static void ValidateResultado(EnvioResultado resultado)
        {
            if (resultado == null)
            {
                throw new ArgumentNullException(nameof(resultado), ERR_RESULTADO_NULL);
            }
        }

        #endregion

        #region Helpers - Normalización

        /// <summary>
        /// Normaliza el nombre de provincia (trim, manejo de null/empty)
        /// </summary>
        private string NormalizarProvincia(string? provincia)
        {
            var original = provincia;
            var normalizada = (provincia ?? string.Empty).Trim();

            if (original != normalizada)
            {
                _logger.LogDebug(LOG_DEBUG_PROVINCIA_NORMALIZADA, original ?? "null", normalizada);
            }

            if (string.IsNullOrWhiteSpace(normalizada))
            {
                _logger.LogWarning(LOG_WARN_PROVINCIA_VACIA, provincia ?? "null");
            }

            return normalizada;
        }

        /// <summary>
        /// Normaliza el nombre de ciudad (trim, null si está vacío)
        /// </summary>
        private string? NormalizarCiudad(string? ciudad)
        {
            var original = ciudad;
            var normalizada = string.IsNullOrWhiteSpace(ciudad) ? null : ciudad!.Trim();

            if (original != normalizada)
            {
                _logger.LogDebug(LOG_DEBUG_CIUDAD_NORMALIZADA, original ?? "null", normalizada ?? "null");
            }

            return normalizada;
        }

        #endregion

        #region Helpers - Mensajes

        /// <summary>
        /// Genera mensaje de vendedor sin tarifa
        /// </summary>
        private string GenerarMensajeSinTarifa(string vendedorId, string provincia, string? ciudad)
        {
            var ciudadPart = ciudad != null ? $" / {ciudad}" : string.Empty;
            return string.Format(MSG_VENDEDOR_SIN_TARIFA, vendedorId, provincia, ciudadPart);
        }

        /// <summary>
        /// Genera mensaje de error de vendedor
        /// </summary>
        private string GenerarMensajeError(string vendedorId)
        {
            return string.Format(MSG_VENDEDOR_ERROR, vendedorId);
        }

        #endregion

        #region Helpers - Procesamiento

        /// <summary>
        /// Filtra y obtiene vendedores únicos válidos
        /// </summary>
        private List<string> ObtenerVendedoresUnicos(IEnumerable<string> vendedorIds)
        {
            var vendedores = vendedorIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            _logger.LogDebug(LOG_DEBUG_VENDEDORES_DISTINTOS, vendedores.Count);

            if (vendedores.Count > MAX_VENDEDORES_POR_CALCULO)
            {
                _logger.LogWarning(LOG_WARN_MUCHOS_VENDEDORES, vendedores.Count, MAX_VENDEDORES_POR_CALCULO);
            }

            return vendedores;
        }

        /// <summary>
        /// Procesa el costo de un vendedor individual
        /// </summary>
        private async Task ProcesarVendedorAsync(
            string vendedorId,
            string provincia,
            string? ciudad,
            EnvioResultado resultado,
            CancellationToken ct)
        {
            try
            {
                _logger.LogDebug(LOG_DEBUG_COSTO_RESOLVER, vendedorId, provincia, ciudad ?? "null");

                var costo = await _resolver.GetCostoAsync(vendedorId, provincia, ciudad, ct)
                    .ConfigureAwait(false);

                _logger.LogDebug(LOG_DEBUG_COSTO_OBTENIDO, vendedorId, costo, !costo.HasValue);

                if (costo.HasValue)
                {
                    if (costo.Value < COSTO_MINIMO_VALIDO)
                    {
                        _logger.LogWarning(LOG_WARN_COSTO_NEGATIVO, vendedorId, costo.Value);
                        resultado.PorVendedor[vendedorId] = 0m;
                        resultado.Mensajes.Add(GenerarMensajeError(vendedorId));
                    }
                    else
                    {
                        resultado.PorVendedor[vendedorId] = costo.Value;
                        resultado.TotalEnvio += costo.Value;

                        _logger.LogInformation(LOG_INFO_VENDEDOR_PROCESADO, vendedorId, costo.Value, true);
                    }
                }
                else
                {
                    // Sin tarifa configurada
                    _logger.LogDebug(LOG_DEBUG_VENDEDOR_SIN_TARIFA, vendedorId, provincia, ciudad ?? "null");

                    resultado.PorVendedor[vendedorId] = 0m;
                    resultado.Mensajes.Add(GenerarMensajeSinTarifa(vendedorId, provincia, ciudad));

                    _logger.LogInformation(LOG_INFO_VENDEDOR_PROCESADO, vendedorId, 0m, false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_RESOLVER, vendedorId);

                resultado.PorVendedor[vendedorId] = 0m;
                resultado.Mensajes.Add(GenerarMensajeError(vendedorId));
            }
        }

        #endregion

        #region Cálculo de Envíos

        /// <inheritdoc />
        public async Task<EnvioResultado> CalcularAsync(
            IEnumerable<string> vendedorIds,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            var resultado = new EnvioResultado();

            if (vendedorIds == null)
            {
                _logger.LogWarning(LOG_WARN_VENDEDORES_NULL);
                return resultado;
            }

            try
            {
                var vendedores = ObtenerVendedoresUnicos(vendedorIds);

                if (vendedores.Count == 0)
                {
                    _logger.LogWarning(LOG_WARN_VENDEDORES_VACIOS);
                    return resultado;
                }

                var prov = NormalizarProvincia(provincia);
                var city = NormalizarCiudad(ciudad);

                _logger.LogInformation(LOG_INFO_CALCULO_INICIADO, vendedores.Count, prov, city ?? "null");

                foreach (var vendedorId in vendedores)
                {
                    ct.ThrowIfCancellationRequested();
                    await ProcesarVendedorAsync(vendedorId, prov, city, resultado, ct).ConfigureAwait(false);
                }

                var configurados = resultado.PorVendedor.Count(kvp => kvp.Value > 0m);
                var sinTarifa = resultado.PorVendedor.Count - configurados;

                _logger.LogInformation(LOG_INFO_CALCULO_COMPLETADO,
                    resultado.TotalEnvio, configurados, resultado.PorVendedor.Count, sinTarifa);

                return resultado;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CALCULAR, provincia ?? "null", ciudad ?? "null");
                return resultado;
            }
        }

        /// <inheritdoc />
        public Task<EnvioResultado> CalcularParaUsuarioAsync(
            IEnumerable<string> vendedorIds,
            Usuario? usuario,
            CancellationToken ct = default)
        {
            if (usuario == null)
            {
                _logger.LogWarning(LOG_WARN_USUARIO_NULL);
                return Task.FromResult(new EnvioResultado());
            }

            if (string.IsNullOrWhiteSpace(usuario.Provincia))
            {
                _logger.LogWarning(LOG_WARN_USUARIO_SIN_DIRECCION, usuario.Id ?? "null");
            }

            _logger.LogDebug(LOG_DEBUG_DIRECCION_USUARIO,
                usuario.Id ?? "null", usuario.Provincia ?? "null", usuario.Ciudad ?? "null");

            return CalcularAsync(vendedorIds, usuario.Provincia, usuario.Ciudad, ct);
        }

        /// <inheritdoc />
        public async Task<decimal?> CalcularParaVendedorAsync(
            string vendedorId,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            ValidateVendedorId(vendedorId);

            try
            {
                var prov = NormalizarProvincia(provincia);
                var city = NormalizarCiudad(ciudad);

                _logger.LogDebug(LOG_DEBUG_COSTO_RESOLVER, vendedorId, prov, city ?? "null");

                var costo = await _resolver.GetCostoAsync(vendedorId, prov, city, ct)
                    .ConfigureAwait(false);

                if (costo.HasValue && costo.Value < COSTO_MINIMO_VALIDO)
                {
                    _logger.LogWarning(LOG_WARN_COSTO_NEGATIVO, vendedorId, costo.Value);
                    return null;
                }

                _logger.LogDebug(LOG_DEBUG_COSTO_OBTENIDO, vendedorId, costo, !costo.HasValue);

                return costo;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_CALCULAR_VENDEDOR, vendedorId);
                return null;
            }
        }

        #endregion

        #region Validación

        /// <inheritdoc />
        public async Task<(bool todosConfigurados, List<string> vendedoresSinTarifa)> ValidarTarifasAsync(
            IEnumerable<string> vendedorIds,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            var vendedoresSinTarifa = new List<string>();

            if (vendedorIds == null)
            {
                _logger.LogWarning(LOG_WARN_VENDEDORES_NULL);
                return (true, vendedoresSinTarifa);
            }

            try
            {
                var vendedores = ObtenerVendedoresUnicos(vendedorIds);

                if (vendedores.Count == 0)
                {
                    _logger.LogWarning(LOG_WARN_VENDEDORES_VACIOS);
                    return (true, vendedoresSinTarifa);
                }

                var prov = NormalizarProvincia(provincia);
                var city = NormalizarCiudad(ciudad);

                foreach (var vendedorId in vendedores)
                {
                    ct.ThrowIfCancellationRequested();

                    var tieneTarifa = await TieneTarifaConfiguradaAsync(vendedorId, prov, city, ct)
                        .ConfigureAwait(false);

                    if (!tieneTarifa)
                    {
                        vendedoresSinTarifa.Add(vendedorId);
                    }
                }

                var todosConfigurados = vendedoresSinTarifa.Count == 0;

                _logger.LogInformation(LOG_INFO_VALIDACION_TARIFAS,
                    vendedores.Count, vendedores.Count - vendedoresSinTarifa.Count, vendedoresSinTarifa.Count);

                return (todosConfigurados, vendedoresSinTarifa);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_VALIDAR_TARIFAS, provincia ?? "null", ciudad ?? "null");
                return (false, vendedoresSinTarifa);
            }
        }

        /// <inheritdoc />
        public async Task<bool> TieneTarifaConfiguradaAsync(
            string vendedorId,
            string? provincia,
            string? ciudad,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vendedorId))
            {
                return false;
            }

            try
            {
                var prov = NormalizarProvincia(provincia);
                var city = NormalizarCiudad(ciudad);

                var costo = await _resolver.GetCostoAsync(vendedorId, prov, city, ct)
                    .ConfigureAwait(false);

                return costo.HasValue && costo.Value >= COSTO_MINIMO_VALIDO;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, LOG_ERROR_RESOLVER, vendedorId);
                return false;
            }
        }

        #endregion

        #region Estadísticas

        /// <inheritdoc />
        public EnvioEstadisticas ObtenerEstadisticas(EnvioResultado resultado)
        {
            ValidateResultado(resultado);

            try
            {
                var estadisticas = new EnvioEstadisticas
                {
                    TotalEnvio = resultado.TotalEnvio,
                    CantidadVendedores = resultado.PorVendedor.Count,
                    VendedoresConTarifa = resultado.PorVendedor.Count(kvp => kvp.Value > 0m),
                    VendedoresSinTarifa = resultado.PorVendedor.Count(kvp => kvp.Value == 0m),
                    CantidadMensajes = resultado.Mensajes.Count
                };

                if (estadisticas.VendedoresConTarifa > 0)
                {
                    var costosConTarifa = resultado.PorVendedor
                        .Where(kvp => kvp.Value > 0m)
                        .Select(kvp => kvp.Value)
                        .ToList();

                    estadisticas.PromedioEnvioPorVendedor = costosConTarifa.Average();
                    estadisticas.EnvioMaximo = costosConTarifa.Max();
                    estadisticas.EnvioMinimo = costosConTarifa.Min();
                }

                _logger.LogDebug(LOG_DEBUG_ESTADISTICAS,
                    estadisticas.TotalEnvio,
                    estadisticas.PromedioEnvioPorVendedor,
                    estadisticas.EnvioMaximo,
                    estadisticas.EnvioMinimo);

                return estadisticas;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LOG_ERROR_ESTADISTICAS);
                return new EnvioEstadisticas
                {
                    TotalEnvio = resultado.TotalEnvio,
                    CantidadVendedores = resultado.PorVendedor.Count,
                    CantidadMensajes = resultado.Mensajes.Count
                };
            }
        }

        #endregion
    }

    #endregion

    #region DTOs

    /// <summary>
    /// Resultado del cálculo de envío con total, detalle por vendedor y mensajes
    /// </summary>
    public sealed class EnvioResultado
    {
        /// <summary>
        /// Total a cobrar por concepto de envío (suma de todos los vendedores)
        /// </summary>
        public decimal TotalEnvio { get; set; } = 0m;

        /// <summary>
        /// Detalle de costo por vendedor
        /// Key: VendedorId
        /// Value: Costo aplicado (0 si no tiene tarifa configurada)
        /// </summary>
        public Dictionary<string, decimal> PorVendedor { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Mensajes informativos o de error
        /// Ejemplos: vendedores sin tarifa, errores de cálculo
        /// </summary>
        public List<string> Mensajes { get; } = new();

        /// <summary>
        /// Indica si el cálculo fue exitoso para todos los vendedores
        /// </summary>
        public bool EsExitoso => Mensajes.Count == 0;

        /// <summary>
        /// Cantidad de vendedores con tarifa configurada
        /// </summary>
        public int VendedoresConfigurados => PorVendedor.Count(kvp => kvp.Value > 0m);

        /// <summary>
        /// Cantidad de vendedores sin tarifa configurada
        /// </summary>
        public int VendedoresSinTarifa => PorVendedor.Count(kvp => kvp.Value == 0m);
    }

    /// <summary>
    /// Estadísticas del cálculo de envío
    /// </summary>
    public sealed class EnvioEstadisticas
    {
        /// <summary>
        /// Total de envío calculado
        /// </summary>
        public decimal TotalEnvio { get; set; }

        /// <summary>
        /// Cantidad total de vendedores procesados
        /// </summary>
        public int CantidadVendedores { get; set; }

        /// <summary>
        /// Vendedores con tarifa configurada
        /// </summary>
        public int VendedoresConTarifa { get; set; }

        /// <summary>
        /// Vendedores sin tarifa configurada
        /// </summary>
        public int VendedoresSinTarifa { get; set; }

        /// <summary>
        /// Promedio de envío por vendedor (solo vendedores con tarifa > 0)
        /// </summary>
        public decimal PromedioEnvioPorVendedor { get; set; }

        /// <summary>
        /// Costo de envío máximo entre vendedores
        /// </summary>
        public decimal EnvioMaximo { get; set; }

        /// <summary>
        /// Costo de envío mínimo entre vendedores (excluyendo 0)
        /// </summary>
        public decimal EnvioMinimo { get; set; }

        /// <summary>
        /// Cantidad de mensajes informativos/errores
        /// </summary>
        public int CantidadMensajes { get; set; }

        /// <summary>
        /// Porcentaje de vendedores con tarifa configurada
        /// </summary>
        public decimal PorcentajeConfigurados =>
            CantidadVendedores > 0
                ? (decimal)VendedoresConTarifa / CantidadVendedores * 100m
                : 0m;
    }

    #endregion
}