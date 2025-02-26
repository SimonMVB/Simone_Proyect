using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Agregar esta directiva
using System.ComponentModel.DataAnnotations.Schema;

namespace Simone.Models
{
    public class ProgramasFidelizacion
    {
        [Key]  // ✅ Define la clave primaria
        public int ProgramaID { get; set; }  // Clave primaria

        public string NombrePrograma { get; set; }  // Nombre del programa
        public string? Descripcion { get; set; }  // Puede ser nulo
        public decimal? Descuento { get; set; }  // Puede ser nulo

        // Relación con Clientes (Clientes pueden estar en múltiples programas)
        public ICollection<ClientesProgramas> ClientesProgramas { get; set; } = new List<ClientesProgramas>();
    }
}
