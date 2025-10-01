using System;
using System.Collections.Generic;

namespace Simone.ViewModels.Reportes
{
    // =======================
    //  Resumen para listados
    // =======================
    public sealed class CompradorResumenVM
    {
        public int VentaID { get; set; }

        /// Id del usuario (AspNetUsers.Id)
        public string UsuarioId { get; set; } = string.Empty;

        public string Nombre { get; set; } = "(sin usuario)";
        public string? Email { get; set; }
        public string? Telefono { get; set; }

        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;

        /// Total de la venta (si en la proyección el origen es decimal?, usa ?? 0m)
        public decimal Total { get; set; }

        /// URL de foto de perfil (AspNetUsers.FotoPerfil)
        public string? FotoPerfil { get; set; }
    }

    // =======================
    //  Ítem de detalle
    // =======================
    public sealed class DetalleVentaVM
    {
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        /// Si el origen es decimal?, proyecta con ?? 0m
        public decimal Subtotal { get; set; }
    }

    /// <summary>
    /// Compatibilidad: nombre histórico usado en el proyecto.
    /// Se mantiene como tipo propio con la misma forma (sin herencia).
    /// </summary>
    public sealed class DetalleFilaVM
    {
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal Subtotal { get; set; }
    }

    // =======================
    //  Dirección para reportes
    // =======================
    public sealed class DireccionVM
    {
        public string? Calle { get; set; }
        public string? Ciudad { get; set; }
        public string? EstadoProvincia { get; set; }
        public string? CodigoPostal { get; set; }
        public string? TelefonoContacto { get; set; }
        public DateTime FechaRegistro { get; set; }
    }

    // =======================
    //  Detalle completo
    // =======================
    public sealed class VentaDetalleVM
    {
        // ---- Datos de la venta
        public int VentaID { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;
        public decimal Total { get; set; }

        // ---- Persona (usuario)
        public string UsuarioId { get; set; } = string.Empty;
        public string Nombre { get; set; } = "(sin usuario)";
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? Cedula { get; set; }         // ← añadida
        public string? Direccion { get; set; }
        public string? FotoPerfil { get; set; }

        // ---- Envío (perfil como fallback)
        public List<DireccionVM> Direcciones { get; set; } = new();
        public string? PerfilCiudad { get; set; }
        public string? PerfilProvincia { get; set; }
        public string? PerfilReferencia { get; set; }

        // ---- Pago / depósito
        public string? Banco { get; set; }
        public string? Depositante { get; set; }
        public string? ComprobanteUrl { get; set; }

        public bool TieneComprobante =>
            !string.IsNullOrWhiteSpace(ComprobanteUrl);

        public bool EsPagoPorTransferencia =>
            !string.IsNullOrWhiteSpace(MetodoPago) &&
            (MetodoPago.Contains("trans", StringComparison.OrdinalIgnoreCase) ||
             MetodoPago.Contains("dep", StringComparison.OrdinalIgnoreCase) ||
             MetodoPago.Contains("transfer", StringComparison.OrdinalIgnoreCase));

        // ---- Detalle de productos
        public List<DetalleVentaVM> Detalles { get; set; } = new();
    }
}
