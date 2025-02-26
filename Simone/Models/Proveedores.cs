using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Simone.Models
{
    public class Proveedores
    {
        [Key]
        public int ProveedorID { get; set; }

        [Required]
        public string NombreProveedor { get; set; }

        public string Contacto { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
        public string Direccion { get; set; }

        // ✅ Relación con Compras - Asegurar que sea una lista y no un objeto
        public ICollection<Compras> Compras { get; set; } = new List<Compras>();
        public ICollection<Productos> Productos { get; set; } = new List<Productos>();
    }
}
