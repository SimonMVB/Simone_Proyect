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
            // Retrieve the cart items for the current user
            var carritoDetalles = await _context.CarritoDetalle
                .Where(c => c.Carrito.ClienteID == user.Id)
                .Where(c => c.Carrito.EstadoCarrito != "Cerrado")
                .Include(cd => cd.Carrito)  // Eagerly load the related Carrito
                .Include(cd => cd.Producto) // Eagerly load the related Producto
                .ToListAsync();

            int itemCount = 0;
            bool itemsUpdated = false; // Flag to track if any items were updated
            List<CarritoDetalle> itemsToRemove = new List<CarritoDetalle>(); // List to track items to remove

            foreach (var item in carritoDetalles)
            {
                // Check if the product quantity exceeds stock
                if (item.Cantidad > item.Producto.Stock)
                {
                    // If the quantity exceeds the stock, update the quantity in the cart
                    item.Cantidad = item.Producto.Stock;

                    // Mark that the items were updated
                    itemsUpdated = true;
                }

                // Check if the stock is zero and remove the item from the cart if so
                if (item.Producto.Stock == 0)
                {
                    itemsToRemove.Add(item); // Add the item to the removal list
                }
                else
                {
                    // Update the total item count
                    itemCount += item.Cantidad;
                }
            }

            // If any items were updated, save the changes to the database
            if (itemsUpdated)
            {
                await _context.SaveChangesAsync();

                // Set the message in TempData for the view
                context.HttpContext.Items["CartMessage"] = "Algunos elementos han sido retirados de su carrito por cambios en su disponibilidad.";
            }

            // Remove items from the cart if their stock is zero
            if (itemsToRemove.Any())
            {
                _context.CarritoDetalle.RemoveRange(itemsToRemove);
                await _context.SaveChangesAsync();

                // Set the message in TempData for the view
                context.HttpContext.Items["CartMessage"] = "Algunos productos han sido eliminados del carrito debido a que ya no están disponibles en stock.";
            }

            // Set the "Carrito" value for the view
            context.HttpContext.Items["Carrito"] = carritoDetalles;

            // Set the cart item count in HttpContext
            context.HttpContext.Items["CartCount"] = itemCount;
        }

        // Proceed with the next action
        await next();
    }

}
