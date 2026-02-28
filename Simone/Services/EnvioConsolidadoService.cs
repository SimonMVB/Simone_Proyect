using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

namespace Simone.Services;

public interface IEnvioConsolidadoService
{
    /// <summary>
    /// Procesa una venta confirmada y crea el EnvioConsolidado con sus SubPedidos
    /// </summary>
    Task<EnvioConsolidado?> ProcesarVentaAsync(
        int ventaId,
        string? provincia = null,
        string? ciudad = null,
        string? direccion = null,
        string? telefono = null,
        CancellationToken ct = default);

    /// <summary>
    /// Calcula el costo de envío para un carrito antes de confirmar
    /// </summary>
    Task<CostoEnvioResult> CalcularCostoEnvioCarritoAsync(
        string usuarioId,
        string provincia,
        string? ciudad,
        CancellationToken ct = default);
}

public class CostoEnvioResult
{
    public bool Exito { get; set; }
    public string? Error { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal CostoFinal => CostoTotal - Descuento;
    public int DiasEstimados { get; set; }
    public string? HubAsignado { get; set; }
    public int? HubId { get; set; }
    public List<CostoEnvioPorTienda> DetallePorTienda { get; set; } = new();
}

public class CostoEnvioPorTienda
{
    public string VendedorUserId { get; set; } = string.Empty; // Usuario.Id (string)
    public int? VendedorEntidadId { get; set; } // Vendedor.VendedorId (int)
    public string NombreTienda { get; set; } = string.Empty;
    public decimal CostoIndividual { get; set; }
    public decimal CostoProporcional { get; set; }
    public decimal PesoEstimado { get; set; }
}

public class EnvioConsolidadoService : IEnvioConsolidadoService
{
    private readonly TiendaDbContext _context;
    private readonly ILogger<EnvioConsolidadoService> _logger;

    // Peso por defecto si no hay configuración
    private const decimal PESO_DEFAULT_KG = 0.5m;
    private const decimal PRECIO_BASE_DEFAULT = 5.00m;
    private const decimal PRECIO_KG_EXTRA_DEFAULT = 1.50m;

    public EnvioConsolidadoService(TiendaDbContext context, ILogger<EnvioConsolidadoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EnvioConsolidado?> ProcesarVentaAsync(
        int ventaId,
        string? provincia = null,
        string? ciudad = null,
        string? direccion = null,
        string? telefono = null,
        CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            _logger.LogInformation("Iniciando procesamiento de venta para envío. VentaId: {VentaId}", ventaId);

            // 1. Obtener la venta con sus detalles
            var venta = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.DetalleVentas)
                    .ThenInclude(d => d.Producto)
                .Include(v => v.DetalleVentas)
                    .ThenInclude(d => d.Variante)
                .FirstOrDefaultAsync(v => v.VentaID == ventaId, ct);

            if (venta == null)
            {
                _logger.LogWarning("Venta no encontrada. VentaId: {VentaId}", ventaId);
                return null;
            }

            if (venta.DetalleVentas == null || !venta.DetalleVentas.Any())
            {
                _logger.LogWarning("Venta sin detalles. VentaId: {VentaId}", ventaId);
                return null;
            }

            // 2. Verificar que no exista ya un envío para esta venta
            var envioExistente = await _context.EnviosConsolidados
                .AnyAsync(e => e.VentaId == ventaId, ct);

            if (envioExistente)
            {
                _logger.LogWarning("Ya existe un envío para esta venta. VentaId: {VentaId}", ventaId);
                return await _context.EnviosConsolidados
                    .FirstOrDefaultAsync(e => e.VentaId == ventaId, ct);
            }

            // 3. Obtener datos de envío
            var provinciaEnvio = !string.IsNullOrWhiteSpace(provincia) ? provincia : venta.Usuario?.Provincia ?? "Sin especificar";
            var ciudadEnvio = !string.IsNullOrWhiteSpace(ciudad) ? ciudad : venta.Usuario?.Ciudad ?? "Sin especificar";
            var direccionEnvio = !string.IsNullOrWhiteSpace(direccion) ? direccion : venta.Usuario?.Direccion ?? "Sin especificar";
            var telefonoEnvio = !string.IsNullOrWhiteSpace(telefono) ? telefono : venta.Usuario?.Telefono ?? "";

            // 4. Asignar Hub
            var hub = await ObtenerHubParaDestino(provinciaEnvio, ciudadEnvio, ct);

            // 5. Agrupar items por VendedorID (string - UserId del Usuario dueño del producto)
            var itemsPorVendedor = venta.DetalleVentas
                .Where(d => d.Producto != null && !string.IsNullOrEmpty(d.Producto.VendedorID))
                .GroupBy(d => d.Producto!.VendedorID)
                .ToList();

            if (!itemsPorVendedor.Any())
            {
                _logger.LogWarning("No hay items con vendedor asignado. VentaId: {VentaId}", ventaId);
                return null;
            }

            // 6. Obtener información de vendedores (Usuario -> Vendedor)
            var vendedorUserIds = itemsPorVendedor.Select(g => g.Key).ToList();
            var usuariosVendedores = await _context.Usuarios
                .AsNoTracking()
                .Where(u => vendedorUserIds.Contains(u.Id))
                .Select(u => new
                {
                    UserId = u.Id,
                    u.NombreCompleto,
                    u.Email,
                    u.VendedorId // int? - FK a Vendedor
                })
                .ToListAsync(ct);

            // Obtener entidades Vendedor para los que tienen VendedorId
            var vendedorEntidadIds = usuariosVendedores
                .Where(u => u.VendedorId.HasValue)
                .Select(u => u.VendedorId!.Value)
                .ToList();

            var vendedoresEntidad = await _context.Vendedores
                .AsNoTracking()
                .Where(v => vendedorEntidadIds.Contains(v.VendedorId))
                .ToListAsync(ct);

            // 7. Calcular costos de envío
            var costoTotal = 0m;
            var pesoTotal = 0m;
            var subPedidosData = new List<(string UserId, int? VendedorEntidadId, string NombreTienda, List<DetalleVentas> Items, decimal Costo, decimal Peso)>();

            foreach (var grupo in itemsPorVendedor)
            {
                var userId = grupo.Key;
                var items = grupo.ToList();

                // Obtener info del vendedor
                var usuarioVendedor = usuariosVendedores.FirstOrDefault(u => u.UserId == userId);
                var vendedorEntidad = usuarioVendedor?.VendedorId.HasValue == true
                    ? vendedoresEntidad.FirstOrDefault(v => v.VendedorId == usuarioVendedor.VendedorId)
                    : null;

                var nombreTienda = vendedorEntidad?.Nombre
                    ?? usuarioVendedor?.NombreCompleto
                    ?? usuarioVendedor?.Email
                    ?? "Tienda";

                // Calcular peso estimado
                var peso = items.Sum(i => PESO_DEFAULT_KG * i.Cantidad);

                // Obtener tarifa de envío
                var tarifa = await ObtenerTarifaEnvio(vendedorEntidad?.VendedorId, vendedorEntidad?.AlianzaId, provinciaEnvio, ciudadEnvio, ct);
                var costoEnvio = CalcularCostoEnvio(tarifa, peso);

                costoTotal += costoEnvio;
                pesoTotal += peso;

                subPedidosData.Add((userId, vendedorEntidad?.VendedorId, nombreTienda, items, costoEnvio, peso));
            }

            // 8. Aplicar descuento por consolidación
            var descuento = 0m;
            if (subPedidosData.Count >= 3)
                descuento = costoTotal * 0.15m;
            else if (subPedidosData.Count >= 2)
                descuento = costoTotal * 0.10m;

            var costoFinal = costoTotal - descuento;

            // 9. Crear EnvioConsolidado
            var envio = new EnvioConsolidado
            {
                Codigo = GenerarCodigoEnvio(),
                VentaId = ventaId,
                ClienteId = venta.UsuarioId,
                HubId = hub?.HubId,
                Estado = "Pendiente",
                Provincia = provinciaEnvio,
                Ciudad = ciudadEnvio,
                DireccionEntrega = direccionEnvio,
                TelefonoContacto = telefonoEnvio,
                PesoTotalKg = pesoTotal,
                CostoEnvioTotal = costoTotal,
                DescuentoEnvio = descuento,
                CostoEnvioFinal = costoFinal,
                FechaCreacion = DateTime.UtcNow
            };

            _context.EnviosConsolidados.Add(envio);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("EnvioConsolidado creado. EnvioId: {EnvioId}, Codigo: {Codigo}",
                envio.EnvioId, envio.Codigo);

            // 10. Crear SubPedidos
            foreach (var (userId, vendedorEntidadId, nombreTienda, items, costoEnvio, peso) in subPedidosData)
            {
                // Si no hay VendedorEntidadId, crear uno automáticamente
                var vendedorIdParaSubPedido = vendedorEntidadId;

                if (!vendedorIdParaSubPedido.HasValue)
                {
                    // Buscar o crear Vendedor para este Usuario
                    var vendedorExistente = await _context.Vendedores
                        .FirstOrDefaultAsync(v => v.Nombre == nombreTienda, ct);

                    if (vendedorExistente != null)
                    {
                        vendedorIdParaSubPedido = vendedorExistente.VendedorId;
                    }
                    else
                    {
                        // Crear nuevo Vendedor
                        var nuevoVendedor = new Vendedor
                        {
                            Nombre = nombreTienda,
                            Activo = true,
                            HubId = hub?.HubId
                        };
                        _context.Vendedores.Add(nuevoVendedor);
                        await _context.SaveChangesAsync(ct);
                        vendedorIdParaSubPedido = nuevoVendedor.VendedorId;

                        // Actualizar Usuario con el nuevo VendedorId
                        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == userId, ct);
                        if (usuario != null)
                        {
                            usuario.VendedorId = nuevoVendedor.VendedorId;
                        }
                    }
                }

                var subtotal = items.Sum(i => i.PrecioUnitario * i.Cantidad);
                var proporcion = costoTotal > 0 ? costoEnvio / costoTotal : 0;
                var costoEnvioProporcional = costoFinal * proporcion;

                var subPedido = new SubPedido
                {
                    Codigo = GenerarCodigoSubPedido(vendedorIdParaSubPedido ?? 0),
                    EnvioId = envio.EnvioId,
                    VendedorId = vendedorIdParaSubPedido!.Value,
                    Estado = "Pendiente",
                    Subtotal = subtotal,
                    CostoEnvioProporcional = costoEnvioProporcional,
                    PesoEstimadoKg = peso,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.SubPedidos.Add(subPedido);
                await _context.SaveChangesAsync(ct);

                // 11. Crear SubPedidoItems
                foreach (var detalle in items)
                {
                    var producto = detalle.Producto!;

                    var subPedidoItem = new SubPedidoItem
                    {
                        SubPedidoId = subPedido.SubPedidoId,
                        ProductoId = detalle.ProductoID,
                        NombreProducto = producto.Nombre,
                        SKU = $"SKU-{producto.ProductoID}",
                        Cantidad = detalle.Cantidad,
                        PrecioUnitario = detalle.PrecioUnitario,
                        Color = detalle.Variante?.Color,
                        Talla = detalle.Variante?.Talla,
                        ImagenPath = producto.ImagenPath
                    };

                    _context.SubPedidoItems.Add(subPedidoItem);
                }

                // 12. Registrar en historial
                var historial = new SubPedidoHistorial
                {
                    SubPedidoId = subPedido.SubPedidoId,
                    EstadoAnterior = "",
                    EstadoNuevo = "Pendiente",
                    Comentario = "SubPedido creado automáticamente desde venta",
                    TipoUsuario = "Sistema",
                    FechaCambio = DateTime.UtcNow
                };
                _context.SubPedidoHistorial.Add(historial);

                _logger.LogInformation("SubPedido creado. SubPedidoId: {SubPedidoId}, VendedorId: {VendedorId}, Items: {Items}",
                    subPedido.SubPedidoId, vendedorIdParaSubPedido, items.Count);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Venta procesada exitosamente. VentaId: {VentaId}, EnvioId: {EnvioId}, SubPedidos: {Count}",
                ventaId, envio.EnvioId, subPedidosData.Count);

            return envio;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Error al procesar venta. VentaId: {VentaId}", ventaId);
            throw;
        }
    }

    public async Task<CostoEnvioResult> CalcularCostoEnvioCarritoAsync(
        string usuarioId,
        string provincia,
        string? ciudad,
        CancellationToken ct = default)
    {
        try
        {
            // Obtener carrito del usuario
            var carrito = await _context.Carrito
                .AsNoTracking()
                .Include(c => c.CarritoDetalles)
                    .ThenInclude(cd => cd.Producto)
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.EstadoCarrito == "En Uso", ct);

            if (carrito == null || !carrito.CarritoDetalles.Any())
            {
                return new CostoEnvioResult
                {
                    Exito = false,
                    Error = "El carrito está vacío"
                };
            }

            // Agrupar por VendedorID (string)
            var itemsPorVendedor = carrito.CarritoDetalles
                .Where(cd => cd.Producto != null && !string.IsNullOrEmpty(cd.Producto.VendedorID))
                .GroupBy(cd => cd.Producto!.VendedorID)
                .ToList();

            if (!itemsPorVendedor.Any())
            {
                return new CostoEnvioResult
                {
                    Exito = false,
                    Error = "No hay productos con vendedor asignado"
                };
            }

            // Obtener información de usuarios/vendedores
            var vendedorUserIds = itemsPorVendedor.Select(g => g.Key).ToList();
            var usuariosVendedores = await _context.Usuarios
                .AsNoTracking()
                .Where(u => vendedorUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.NombreCompleto, u.Email, u.VendedorId })
                .ToListAsync(ct);

            var vendedorEntidadIds = usuariosVendedores
                .Where(u => u.VendedorId.HasValue)
                .Select(u => u.VendedorId!.Value)
                .ToList();

            var vendedoresEntidad = await _context.Vendedores
                .AsNoTracking()
                .Where(v => vendedorEntidadIds.Contains(v.VendedorId))
                .ToListAsync(ct);

            var costoTotal = 0m;
            var diasMaximo = 0;
            var detalle = new List<CostoEnvioPorTienda>();

            foreach (var grupo in itemsPorVendedor)
            {
                var userId = grupo.Key;
                var items = grupo.ToList();

                var usuarioVendedor = usuariosVendedores.FirstOrDefault(u => u.Id == userId);
                var vendedorEntidad = usuarioVendedor?.VendedorId.HasValue == true
                    ? vendedoresEntidad.FirstOrDefault(v => v.VendedorId == usuarioVendedor.VendedorId)
                    : null;

                var nombreTienda = vendedorEntidad?.Nombre
                    ?? usuarioVendedor?.NombreCompleto
                    ?? usuarioVendedor?.Email
                    ?? "Tienda";

                var peso = items.Sum(i => PESO_DEFAULT_KG * i.Cantidad);
                var tarifa = await ObtenerTarifaEnvio(vendedorEntidad?.VendedorId, vendedorEntidad?.AlianzaId, provincia, ciudad, ct);
                var costoIndividual = CalcularCostoEnvio(tarifa, peso);

                costoTotal += costoIndividual;

                if (tarifa?.DiasEntregaEstimados > diasMaximo)
                    diasMaximo = tarifa.DiasEntregaEstimados;

                detalle.Add(new CostoEnvioPorTienda
                {
                    VendedorUserId = userId,
                    VendedorEntidadId = vendedorEntidad?.VendedorId,
                    NombreTienda = nombreTienda,
                    CostoIndividual = costoIndividual,
                    PesoEstimado = peso
                });
            }

            // Aplicar descuento
            var descuento = 0m;
            if (detalle.Count >= 3)
                descuento = costoTotal * 0.15m;
            else if (detalle.Count >= 2)
                descuento = costoTotal * 0.10m;

            var costoFinal = costoTotal - descuento;

            // Calcular costo proporcional
            foreach (var d in detalle)
            {
                var proporcion = costoTotal > 0 ? d.CostoIndividual / costoTotal : 0;
                d.CostoProporcional = costoFinal * proporcion;
            }

            // Obtener Hub
            var hub = await ObtenerHubParaDestino(provincia, ciudad, ct);

            return new CostoEnvioResult
            {
                Exito = true,
                CostoTotal = costoTotal,
                Descuento = descuento,
                DiasEstimados = diasMaximo > 0 ? diasMaximo : 5,
                HubAsignado = hub?.Nombre,
                HubId = hub?.HubId,
                DetallePorTienda = detalle
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular costo de envío. UsuarioId: {UsuarioId}", usuarioId);
            return new CostoEnvioResult
            {
                Exito = false,
                Error = "Error al calcular el costo de envío"
            };
        }
    }

    #region Métodos Privados

    private async Task<HubEnvio?> ObtenerHubParaDestino(string provincia, string? ciudad, CancellationToken ct)
    {
        // Buscar Hub en la misma provincia
        var hub = await _context.HubsEnvio
            .AsNoTracking()
            .Where(h => h.Activo && h.Provincia.ToLower() == provincia.ToLower())
            .FirstOrDefaultAsync(ct);

        // Si no hay, tomar el primero activo
        hub ??= await _context.HubsEnvio
            .AsNoTracking()
            .Where(h => h.Activo)
            .FirstOrDefaultAsync(ct);

        return hub;
    }

    private async Task<TarifaEnvioAlianza?> ObtenerTarifaEnvio(
        int? vendedorId,
        int? alianzaId,
        string provincia,
        string? ciudad,
        CancellationToken ct)
    {
        TarifaEnvioAlianza? tarifa = null;
        var provinciaLower = provincia.ToLower();
        var ciudadLower = ciudad?.ToLower();

        // 1. Buscar tarifa de alianza primero (si tiene alianza)
        if (alianzaId.HasValue)
        {
            // Por ciudad específica
            if (!string.IsNullOrWhiteSpace(ciudadLower))
            {
                tarifa = await _context.TarifasEnvioAlianza
                    .AsNoTracking()
                    .Where(t => t.AlianzaId == alianzaId && t.Activo &&
                                t.Provincia.ToLower() == provinciaLower &&
                                t.Ciudad != null && t.Ciudad.ToLower() == ciudadLower)
                    .FirstOrDefaultAsync(ct);
            }

            // Por provincia
            tarifa ??= await _context.TarifasEnvioAlianza
                .AsNoTracking()
                .Where(t => t.AlianzaId == alianzaId && t.Activo &&
                            t.Provincia.ToLower() == provinciaLower &&
                            (t.Ciudad == null || t.Ciudad == ""))
                .FirstOrDefaultAsync(ct);
        }

        // 2. Si no hay tarifa de alianza, buscar del vendedor
        if (tarifa == null && vendedorId.HasValue)
        {
            // Por ciudad específica
            if (!string.IsNullOrWhiteSpace(ciudadLower))
            {
                tarifa = await _context.TarifasEnvioAlianza
                    .AsNoTracking()
                    .Where(t => t.VendedorId == vendedorId && t.Activo &&
                                t.Provincia.ToLower() == provinciaLower &&
                                t.Ciudad != null && t.Ciudad.ToLower() == ciudadLower)
                    .FirstOrDefaultAsync(ct);
            }

            // Por provincia
            tarifa ??= await _context.TarifasEnvioAlianza
                .AsNoTracking()
                .Where(t => t.VendedorId == vendedorId && t.Activo &&
                            t.Provincia.ToLower() == provinciaLower &&
                            (t.Ciudad == null || t.Ciudad == ""))
                .FirstOrDefaultAsync(ct);
        }

        return tarifa;
    }

    private static decimal CalcularCostoEnvio(TarifaEnvioAlianza? tarifa, decimal pesoKg)
    {
        if (tarifa == null)
        {
            // Tarifa por defecto
            return PRECIO_BASE_DEFAULT + (pesoKg * PRECIO_KG_EXTRA_DEFAULT);
        }

        var costoBase = tarifa.PrecioBase;

        if (pesoKg > tarifa.PesoIncluidoKg)
        {
            var pesoExtra = pesoKg - tarifa.PesoIncluidoKg;
            costoBase += pesoExtra * tarifa.PrecioPorKgExtra;
        }

        return Math.Round(costoBase, 2);
    }

    private static string GenerarCodigoEnvio()
    {
        var timestamp = DateTime.UtcNow.ToString("yyMMdd");
        var random = new Random().Next(1000, 9999);
        return $"ENV-{timestamp}-{random}";
    }

    private static string GenerarCodigoSubPedido(int vendedorId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyMMddHHmm");
        var random = new Random().Next(100, 999);
        return $"SP-{vendedorId}-{timestamp}-{random}";
    }

    #endregion
}