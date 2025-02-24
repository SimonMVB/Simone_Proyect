using System;
using System.Collections.Generic;

namespace Simone.Models
{
    public class ProgramasFidelizacion
    {
        public int ProgramaID { get; set; }  // Clave primaria
        public string NombrePrograma { get; set; }  // Nombre del programa
        public string? Descripcion { get; set; }  // Puede ser nulo
        public decimal? Descuento { get; set; }  // Puede ser nulo

        // Relación con Clientes (Clientes pueden estar en múltiples programas)
        public ICollection<ClientesProgramas> ClientesProgramas { get; set; }
    }
}
