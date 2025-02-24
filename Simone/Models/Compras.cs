using System;
using System.ComponentModel.DataAnnotations.Schema;
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
        [ForeignKey("ProveedorID")]
        public Proveedores Proveedor { get; set; }

        // 🔹 Relación con DetallesCompra (una Compra tiene muchos Detalles)
        public ICollection<DetallesCompra> DetallesCompra { get; set; } = new List<DetallesCompra>();
    }
}
