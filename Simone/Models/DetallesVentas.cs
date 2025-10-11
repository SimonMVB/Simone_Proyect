using Simone.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class DetalleVentas
{
    [Key]
    public int DetalleVentaID { get; set; }

    // === Venta ===
    public int VentaID { get; set; }

    [ForeignKey(nameof(VentaID))]
    public Ventas Venta { get; set; }

    // === Producto base ===
    public int ProductoID { get; set; }

    [ForeignKey(nameof(ProductoID))]
    public Producto Producto { get; set; }

    // === NUEVO: Variante seleccionada (Color+Talla) ===
    // Nullable para no romper ventas históricas; en nuevas ventas con variantes debe venir seteado.
    public int? ProductoVarianteID { get; set; }

    [ForeignKey(nameof(ProductoVarianteID))]
    public ProductoVariante? Variante { get; set; }

    // === Cantidad y precios ===
    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; }

    // Se “congela” al momento de facturar. Si la variante tiene PrecioVenta, úsalo; si no, el del Producto.
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue)]
    public decimal PrecioUnitario { get; set; }

    // Descuento por línea (monto, no %). Si usas %, mantenlo en la capa de servicio.
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue)]
    public decimal? Descuento { get; set; }

    // Se puede almacenar o recalcular (tu OnModelCreating ya estandariza decimal(18,2)).
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Subtotal { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // Conveniencia (no mapeado)
    [NotMapped]
    public decimal SubtotalCalculado => (PrecioUnitario * Cantidad) - (Descuento ?? 0);

    public ICollection<Devoluciones> Devoluciones { get; set; } = new List<Devoluciones>();
}
