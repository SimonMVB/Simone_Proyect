using System;
using System.Collections.Generic;

namespace Simone.ViewModels.Reportes
{
    // Resumen para la lista (compradores/ventas)
    public class CompradorResumenVM
    {
        public int VentaID { get; set; }
        public int ClienteID { get; set; }
        public string Nombre { get; set; } = "(sin cliente)";
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = "";
        public string MetodoPago { get; set; } = "";
        public decimal Total { get; set; }
    }

    // Ítems de la venta
    public class DetalleFilaVM
    {
        public string Producto { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal Subtotal { get; set; }
    }

    // Detalle de una venta + persona + envío
    public class VentaDetalleVM
    {
        public int VentaID { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = "";
        public string MetodoPago { get; set; } = "";
        public decimal Total { get; set; }

        // Persona
        public int ClienteID { get; set; }
        public string Nombre { get; set; } = "(sin cliente)";
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }

        // Envío (direcciones guardadas del cliente)
        public List<DireccionVM> Direcciones { get; set; } = new();

        // Productos vendidos en esa venta
        public List<DetalleFilaVM> Detalles { get; set; } = new();
    }

    public class DireccionVM
    {
        public int DireccionID { get; set; }
        public string? Calle { get; set; }
        public string? Ciudad { get; set; }
        public string? EstadoProvincia { get; set; }
        public string? CodigoPostal { get; set; }
        public string? TelefonoContacto { get; set; }
        public DateTime FechaRegistro { get; set; }
    }
}
