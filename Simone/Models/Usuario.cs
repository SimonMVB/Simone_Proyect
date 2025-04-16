using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Usuario : IdentityUser
    {
        [Required, StringLength(50)]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string NombreCompleto { get; set; } = string.Empty;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public bool Activo { get; set; } = true;

        // Relación opcional con Roles personalizados
        public string? RolID { get; set; }

        [ForeignKey("RolID")]
        public Roles? Rol { get; set; }
    }
}
