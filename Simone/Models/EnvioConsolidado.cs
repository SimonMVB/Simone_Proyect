using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class MovimientosInventario
    {
        [Key]  // ✅ Clave primaria
        public int MovimientoID { get; set; }

        // === Producto base (requerido, para compatibilidad) ===
        public int ProductoID { get; set; }  // FK -> Productos
        [ForeignKey(nameof(ProductoID))]
        public Producto Producto { get; set; }

        // === NUEVO: Variante seleccionada (opcional) ===
        // Nullable para no romper movimientos históricos. En productos con variantes,
        // cuando el movimiento aplique a una combinación (Color+Talla), se debe setear.
        public int? ProductoVarianteID { get; set; }

        [ForeignKey(nameof(ProductoVarianteID))]
        public ProductoVariante? Variante { get; set; }

        // === Datos del movimiento ===
        public int Cantidad { get; set; }            // +Entrada / -Salida (tu capa de servicio controla el signo)
        public string TipoMovimiento { get; set; }   // "Entrada" / "Salida" (o lo que ya uses)
        public DateTime? FechaMovimiento { get; set; }  // Puede ser nulo
        public string? Descripcion { get; set; }     // Observaciones opcionales
    }
}
