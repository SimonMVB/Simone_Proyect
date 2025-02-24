using System;
using System.Collections.Generic;

namespace Simone.Models
{
    public class Proveedores
    {
        public int ProveedorID { get; set; }  // Clave primaria
        public string NombreProveedor { get; set; }  // Nombre del proveedor
        public string? Contacto { get; set; }  // Persona de contacto (puede ser nulo)
        public string? Telefono { get; set; }  // Puede ser nulo
        public string? Email { get; set; }  // Puede ser nulo
        public string? Direccion { get; set; }  // Puede ser nulo

        // Relación con Productos
        public ICollection<Productos> Productos { get; set; }
    }
}
