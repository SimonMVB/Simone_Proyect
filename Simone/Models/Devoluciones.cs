using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Devoluciones
    {
        [Key]
        public int DevolucionID { get; set; }

        // FK con DetalleVentas
        public int DetalleVentaID { get; set; }

        public DateTime FechaDevolucion { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string Motivo { get; set; } = string.Empty;   // "devolucion", "deposito_falso", "otro"

        public int CantidadDevuelta { get; set; }            // cantidad devuelta de ese detalle

        public bool Aprobada { get; set; } = true;

        [ForeignKey(nameof(DetalleVentaID))]
        public DetalleVentas? DetalleVenta { get; set; }
    }
}
