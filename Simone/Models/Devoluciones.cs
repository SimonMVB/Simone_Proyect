using System;

namespace Simone.Models
{
    public class Devoluciones
    {
        public int DevolucionID { get; set; }  // Clave primaria
        public int DetalleVentaID { get; set; }  // Clave foránea con DetalleVentas
        public DateTime FechaDevolucion { get; set; }  // Fecha de devolución
        public string Motivo { get; set; }  // Motivo de la devolución
        public int CantidadDevuelta { get; set; }  // Cantidad de productos devueltos
        public bool Aprobada { get; set; }  // Indica si la devolución fue aprobada

        // Relación con DetalleVentas
        public DetalleVentas DetalleVenta { get; set; }
    }
}
