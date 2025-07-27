using Simone.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class DetalleVentas
{
    [Key]
    public int DetalleVentaID { get; set; }

    public int VentaID { get; set; }
    public int ProductoID { get; set; }

    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PrecioUnitario { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Descuento { get; set; }

    public decimal? Subtotal { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    [ForeignKey("VentaID")]
    public Ventas Venta { get; set; }

    [ForeignKey("ProductoID")]
    public Producto Producto { get; set; }

    [NotMapped]
    public decimal SubtotalCalculado => (PrecioUnitario * Cantidad) - (Descuento ?? 0);
}
