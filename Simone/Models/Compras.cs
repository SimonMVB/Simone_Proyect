using System;
using Simone.Models;

namespace Simone.Models
{
    public class Compras
    {
        public int CompraID { get; set; }  // Clave primaria
        public int ProveedorID { get; set; }  // Clave foránea con Proveedores
        public DateTime? FechaCompra { get; set; }  // Puede ser nulo
        public decimal? Total { get; set; }  // Puede ser nulo

        // Relación con Proveedores
        public Proveedores Proveedor { get; set; }
        public object DetallesCompra { get; internal set; }
    }
}
