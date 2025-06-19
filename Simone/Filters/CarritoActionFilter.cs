using Simone.Models;
using Simone.Data;
using System.Collections.Generic;
using System.Linq;
using Simone.Models;                     // Para acceder al modelo de Usuario y Carrito
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Filters;  // Para implementar el filtro de acción

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
        var user = await _userManager.GetUserAsync(context.HttpContext.User);
        if (user != null)
        {
            var carritoDetalles = await _context.CarritoDetalle
                .Where(c => c.Carrito.ClienteID == user.Id)
                .Where(c => c.Carrito.EstadoCarrito != "Cerrado")
                .Include(cd => cd.Carrito)  // Eagerly load the related Carrito
                .Include(cd => cd.Producto) // Eagerly load the related Producto
                .ToListAsync();

            int itemCount = carritoDetalles.Sum(cd => cd.Cantidad);

            // Set the "Carrito" value for the view
            context.HttpContext.Items["Carrito"] = carritoDetalles;

            context.HttpContext.Items["CartCount"] = itemCount;
        }

        // Proceed with the next action
        await next();
    }
}
