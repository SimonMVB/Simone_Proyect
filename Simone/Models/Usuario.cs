using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class Usuario : IdentityUser
    {
        [Key]
        public int UsuarioID { get; set; }  // Clave primaria

        [Required, StringLength(50)]
        public string NombreUsuario { get; set; }  // Nombre de usuario (único)

        [Required, EmailAddress]
        public string Email { get; set; }  // Correo electrónico del usuario

        [Required]
        public string PasswordHash { get; set; }  // Hash de la contraseña

        public DateTime FechaRegistro { get; set; } = DateTime.Now; // Fecha de registro

        public bool Activo { get; set; } = true;  // Estado de la cuenta

        public bool EmailConfirmed { get; set; } = false;  // Confirmación del email

        // Relación con Roles
        [ForeignKey("RolID")]
        public int RolID { get; set; }
        public Roles Rol { get; set; }

        // NUEVO: Agregando Nombre Completo
        [Required, StringLength(100)]
        public string NombreCompleto { get; set; }
        public string UserName { get; internal set; }
    }
}
