using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.ViewModels.Devoluciones
{
    public class CrearDevolucionVM
    {
        [Required]
        public int VentaId { get; set; }

        [Required]
        public string Motivo { get; set; } = "devolucion"; // "devolucion" | "deposito_falso" | "otro"

        [StringLength(500)]
        public string? Nota { get; set; }

        public string? ReturnUrl { get; set; }

        // Líneas de la venta con tope de devolución
        public List<LineaVM> Lineas { get; set; } = new();

        public class LineaVM
        {
            public int DetalleVentaID { get; set; }
            public string Producto { get; set; } = string.Empty;
            public int CantidadVendida { get; set; }
            public int CantidadDevueltaAcumulada { get; set; }
            public int CantidadMaxDevolucion => CantidadVendida - CantidadDevueltaAcumulada;

            // Valor a devolver en esta solicitud (input del usuario)
            [Range(0, int.MaxValue)]
            public int CantidadADevolver { get; set; }
        }
    }
}
