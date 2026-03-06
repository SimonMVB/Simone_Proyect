using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simone.Data;
using Simone.Models;

/// <summary>
/// Filtro global que carga el carrito activo del usuario en cada request.
/// Expone los datos en Context.Items para uso en el layout (_CartPartial).
/// Aplica limpieza automática de ítems sin stock y valida stock por variante.
/// </summary>
public class CarritoActionFilter : IAsyncActionFilter
{
    private readonly UserManager<Usuario> _userManager;
    private readonly TiendaDbContext _context;
    private readonly ILogger<CarritoActionFilter> _logger;

    private const string ESTADO_CERRADO = "Cerrado";

    public CarritoActionFilter(
        UserManager<Usuario> userManager,
        TiendaDbContext context,
        ILogger<CarritoActionFilter> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Solo procesar si hay usuario autenticado
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            await next();
            return;
        }

        try
        {
            var usuario = await _userManager.GetUserAsync(context.HttpContext.User);
            if (usuario == null)
            {
                await next();
                return;
            }

            // ── Una sola query con todos los includes necesarios ──────────
            // AsSplitQuery evita el producto cartesiano cuando hay múltiples
            // colecciones incluidas (Producto + Variante en cada detalle).
            var carritoDetalles = await _context.CarritoDetalle
                .AsSplitQuery()
                .Include(cd => cd.Carrito)
                .Include(cd => cd.Producto)
                .Include(cd => cd.Variante)
                .Where(cd => cd.Carrito.UsuarioId == usuario.Id
                          && cd.Carrito.EstadoCarrito != ESTADO_CERRADO)
                .OrderByDescending(cd => cd.CarritoDetalleID)
                .ToListAsync();

            // ── Limpieza y validación ─────────────────────────────────────
            var (detallesLimpios, mensaje) = await LimpiarYValidarAsync(carritoDetalles);

            // ── Exponer al layout ─────────────────────────────────────────
            var itemCount = detallesLimpios.Sum(d => d.Cantidad);

            context.HttpContext.Items["Carrito"] = detallesLimpios;
            context.HttpContext.Items["CartCount"] = itemCount;

            if (mensaje != null)
                context.HttpContext.Items["CartMessage"] = mensaje;
        }
        catch (Exception ex)
        {
            // No romper la request por un error del carrito — solo loguear
            _logger.LogError(ex, "Error al cargar el carrito del usuario en CarritoActionFilter");

            context.HttpContext.Items["Carrito"] = new List<CarritoDetalle>();
            context.HttpContext.Items["CartCount"] = 0;
        }

        await next();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Limpia ítems sin stock y ajusta cantidades que excedan el stock real.
    // Considera stock de variante cuando aplica, stock de producto si no.
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<(List<CarritoDetalle> Detalles, string? Mensaje)>
        LimpiarYValidarAsync(List<CarritoDetalle> detalles)
    {
        var itemsAEliminar = new List<CarritoDetalle>();
        bool cantidadesAjustadas = false;

        foreach (var item in detalles)
        {
            // ── Stock efectivo: variante tiene prioridad sobre producto ──
            int stockEfectivo = item.Variante?.Stock
                             ?? item.Producto?.Stock
                             ?? 0;

            if (item.Producto == null || stockEfectivo <= 0)
            {
                itemsAEliminar.Add(item);
                continue;
            }

            if (item.Cantidad > stockEfectivo)
            {
                _logger.LogDebug(
                    "Ajustando cantidad. DetalleId={Id}, Antes={Antes}, Ahora={Ahora}",
                    item.CarritoDetalleID, item.Cantidad, stockEfectivo);

                item.Cantidad = stockEfectivo;
                cantidadesAjustadas = true;
            }
        }

        // ── Persistir cambios en una sola transacción ────────────────────
        bool huboCambios = cantidadesAjustadas || itemsAEliminar.Count > 0;

        if (itemsAEliminar.Count > 0)
        {
            _logger.LogInformation(
                "Eliminando {Count} ítems sin stock del carrito", itemsAEliminar.Count);
            _context.CarritoDetalle.RemoveRange(itemsAEliminar);
        }

        if (huboCambios)
        {
            await _context.SaveChangesAsync();
        }

        // ── Lista final sin los ítems eliminados ─────────────────────────
        var detallesLimpios = detalles
            .Except(itemsAEliminar)
            .ToList();

        // ── Mensaje para el usuario ──────────────────────────────────────
        string? mensaje = (itemsAEliminar.Count > 0, cantidadesAjustadas) switch
        {
            (true, true) => "Algunos productos se eliminaron o ajustaron su cantidad por cambios de stock.",
            (true, false) => "Algunos productos se eliminaron del carrito por falta de stock.",
            (false, true) => "Algunas cantidades se ajustaron por cambios de stock.",
            _ => null
        };

        return (detallesLimpios, mensaje);
    }
}
