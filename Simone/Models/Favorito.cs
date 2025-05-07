namespace Simone.Models
{
    public class Favorito
    {
        public int Id { get; set; }

        public string UsuarioId { get; set; } = null!;
        public int ProductoId { get; set; }

        public Producto Producto { get; set; } = null!;
    }
}
