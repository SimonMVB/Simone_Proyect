using Simone.Models;

public class VentaReversion
{
    public int Id { get; set; }
    public int VentaID { get; set; }
    public string? Motivo { get; set; }  // "deposito_falso" | "devolucion" | "otro"
    public string? Nota { get; set; }
    public string? AdminId { get; set; } // Usuario (Id de AspNetUsers)
    public DateTime CreatedAt { get; set; }

    public Ventas Venta { get; set; } = default!;
    public Usuario? Admin { get; set; }
}