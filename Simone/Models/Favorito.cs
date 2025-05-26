using System;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class Favorito
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = null!;

        public Usuario Usuario { get; set; } = null!;

        [Required]
        public int ProductoId { get; set; }

        public Producto Producto { get; set; } = null!;

        public DateTime FechaGuardado { get; set; } = DateTime.UtcNow;
    }
}
