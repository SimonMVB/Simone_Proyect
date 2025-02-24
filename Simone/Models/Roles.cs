using System;
using System.Collections.Generic;

namespace Simone.Models
{
    public class Roles
    {
        public int RolID { get; set; }  // Clave primaria
        public string NombreRol { get; set; }  // Nombre del rol

        // Relación con Usuarios
        public ICollection<Usuario> Usuarios { get; set; }
    }
}
