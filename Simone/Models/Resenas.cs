using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Reseñas
    {
        [Key] public int ReseñaID { get; set; }

        [Required] public int ProductoID { get; set; }
        [ForeignKey(nameof(ProductoID))] public Producto Producto { get; set; } = null!;

        // ---- Cambiado a Usuario ----
        [Required] public string UsuarioId { get; set; } = default!;
        [ForeignKey(nameof(UsuarioId))] public Usuario Usuario { get; set; } = null!;

        public int Calificacion { get; set; }
        public string? Comentario { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
    }
}
