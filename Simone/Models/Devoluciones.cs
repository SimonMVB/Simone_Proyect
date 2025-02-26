using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Devoluciones
    {
        [Key] // ✅ Definir clave primaria
        public int DevolucionID { get; set; }

        public int DetalleVentaID { get; set; }  // Clave foránea con DetalleVentas
        public DateTime FechaDevolucion { get; set; }  // Fecha de devolución
        public string Motivo { get; set; }  // Motivo de la devolución
        public int CantidadDevuelta { get; set; }  // Cantidad de productos devueltos
        public bool Aprobada { get; set; }  // Indica si la devolución fue aprobada

        // Relación con DetalleVentas
        [ForeignKey("DetalleVentaID")]
        public DetalleVentas DetalleVenta { get; set; }
    }
}
