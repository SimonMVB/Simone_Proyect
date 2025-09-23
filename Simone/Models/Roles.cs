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
        // Constructores
        public Roles() : base() { }

        public Roles(string name, string? descripcion) : base(name) => Descripcion = descripcion;

        // Variables adicionales
        [Required, StringLength(100)]
        public string Descripcion { get; set; } = string.Empty;
    }
}
