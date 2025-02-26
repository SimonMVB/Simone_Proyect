using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class Roles
    {
        [Key] // ✅ Define la clave primaria correctamente
        public int RolID { get; set; }

        public string NombreRol { get; set; }

        // Relación con Usuarios
        public ICollection<Usuario> Usuarios { get; set; }
    }
}
