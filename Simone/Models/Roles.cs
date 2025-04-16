using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    /// <summary>
    /// Extiende IdentityRole para añadir propiedades adicionales sin redefinir la clave primaria.
    /// </summary>
    public class Roles : IdentityRole
    {
        [Required, StringLength(50)]
        public string NombreRol { get; set; } = string.Empty;

        // Relación con Usuarios
        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}
