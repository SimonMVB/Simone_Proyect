using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Simone.Data;
using Simone.Models;

public class CarritoActionFilter : IAsyncActionFilter
{
    private readonly UserManager<Usuario> _userManager;
    private readonly TiendaDbContext _context;

    public CarritoActionFilter(UserManager<Usuario> userManager, TiendaDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var usuario = await _userManager.GetUserAsync(context.HttpContext.User);
        if (usuario != null)
        {
            // Carga el carrito no cerrado del usuario (centralizado en UsuarioId)
            var carritoDetalles = await _context.CarritoDetalle
                .Include(cd => cd.Carrito)
                .Include(cd => cd.Producto)
                .Where(cd => cd.Carrito.UsuarioId == usuario.Id)
                .Where(cd => cd.Carrito.EstadoCarrito != "Cerrado")
                .ToListAsync();

            int itemCount = 0;
            bool cantidadesAjustadas = false;
            var itemsAEliminar = new List<CarritoDetalle>();

            foreach (var item in carritoDetalles)
            {
                // Si el producto no existe o no tiene stock, marcar para eliminar
                if (item.Producto == null || item.Producto.Stock <= 0)
                {
                    itemsAEliminar.Add(item);
                    continue;
                }

                // Ajustar cantidad si excede el stock disponible
                if (item.Cantidad > item.Producto.Stock)
                {
                    item.Cantidad = item.Producto.Stock;
                    cantidadesAjustadas = true;
                }

                itemCount += item.Cantidad;
            }

            // Guardar ajustes de cantidades
            if (cantidadesAjustadas)
            {
                await _context.SaveChangesAsync();
                context.HttpContext.Items["CartMessage"] = "Algunos productos ajustaron su cantidad por cambios de stock.";
            }

            // Eliminar ítems sin stock
            if (itemsAEliminar.Count > 0)
            {
                _context.CarritoDetalle.RemoveRange(itemsAEliminar);
                await _context.SaveChangesAsync();

                // Reflejar eliminación en memoria y recalc del conteo
                foreach (var x in itemsAEliminar)
                    carritoDetalles.Remove(x);

                itemCount = carritoDetalles.Sum(d => d.Cantidad);

                // Si ya había mensaje por ajustes, se sobrescribirá por el de eliminación (más relevante)
                context.HttpContext.Items["CartMessage"] = "Algunos productos se eliminaron del carrito por falta de stock.";
            }

            // Exponer a la vista
            context.HttpContext.Items["Carrito"] = carritoDetalles;
            context.HttpContext.Items["CartCount"] = itemCount;
        }

        // Continuar con la ejecución de la acción
        await next();
    }
}
