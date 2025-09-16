using System;
using System.Collections.Generic;

namespace Simone.ViewModels.Reportes
{
    /// <summary>
    /// Resumen para la lista (compradores/ventas).
    /// </summary>
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

        // Foto de perfil (desde AspNetUsers) para el listado de compradores
        public string? FotoPerfil { get; set; }
    }

    /// <summary>
    /// Ítem (línea) de la venta.
    /// </summary>
    public class DetalleFilaVM
    {
        public string Producto { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal Subtotal { get; set; }
    }

    /// <summary>
    /// Detalle completo de la venta + persona + envío + pago.
    /// </summary>
    public class VentaDetalleVM
    {
        // Pago / depósito
        public string? Banco { get; set; }             // p.ej. "Banco Pichincha"
        public string? Depositante { get; set; }       // nombre de quien deposita
        public string? ComprobanteUrl { get; set; }    // ruta/URL del comprobante (imagen o PDF)

        // Fallback desde perfil Identity (cuando no hay dirección guardada en la fecha de la venta)
        public string? PerfilCiudad { get; set; }
        public string? PerfilProvincia { get; set; }
        public string? PerfilReferencia { get; set; }

        // Datos de la venta
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
        public List<DireccionVM> Direcciones { get; set; } = new List<DireccionVM>();

        // Productos vendidos en esa venta
        public List<DetalleFilaVM> Detalles { get; set; } = new List<DetalleFilaVM>();
    }

    /// <summary>
    /// Dirección del cliente (histórica).
    /// </summary>
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
