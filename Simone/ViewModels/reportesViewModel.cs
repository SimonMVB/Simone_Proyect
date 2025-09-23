using System;
using System.Collections.Generic;

namespace Simone.ViewModels.Reportes
{
    /// <summary>Resumen para la lista (compradores/ventas).</summary>
    public sealed class CompradorResumenVM
    {
        public int VentaID { get; set; }

        /// <summary>Id del usuario (AspNetUsers.Id).</summary>
        public string UsuarioId { get; set; } = string.Empty;

        public string Nombre { get; set; } = "(sin usuario)";
        public string? Email { get; set; }
        public string? Telefono { get; set; }

        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;

        // Si tu entidad usa decimal? proyecta con GetValueOrDefault() o ?? 0m
        public decimal Total { get; set; }

        /// <summary>URL de foto de perfil (AspNetUsers.FotoPerfil).</summary>
        public string? FotoPerfil { get; set; }
    }

    /// <summary>Ítem (línea) de la venta.</summary>
    public sealed class DetalleFilaVM
    {
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }

        // Igual que arriba: si tu Subtotal viene como decimal?, proyecta con ?? 0m.
        public decimal Subtotal { get; set; }
    }

    /// <summary>Detalle completo de la venta + persona + envío + pago.</summary>
    public sealed class VentaDetalleVM
    {
        // Pago / depósito (desde Usuario)
        public string? Banco { get; set; }
        public string? Depositante { get; set; }
        public string? ComprobanteUrl { get; set; }

        // Fallback desde perfil Identity (cuando no hay dirección histórica)
        public string? PerfilCiudad { get; set; }
        public string? PerfilProvincia { get; set; }
        public string? PerfilReferencia { get; set; }

        // Datos de la venta
        public int VentaID { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;
        public decimal Total { get; set; }

        // Persona (centralizada en Usuario)
        public string UsuarioId { get; set; } = string.Empty;
        public string Nombre { get; set; } = "(sin usuario)";
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }

        // Envío (direcciones a mostrar)
        public List<DireccionVM> Direcciones { get; set; } = new();

        // Productos vendidos en esa venta
        public List<DetalleFilaVM> Detalles { get; set; } = new();
    }

    /// <summary>Dirección del usuario para mostrar en reportes.</summary>
    public sealed class DireccionVM
    {
        public string? Calle { get; set; }
        public string? Ciudad { get; set; }
        public string? EstadoProvincia { get; set; }
        public string? CodigoPostal { get; set; }
        public string? TelefonoContacto { get; set; }
        public DateTime FechaRegistro { get; set; }
    }
}
