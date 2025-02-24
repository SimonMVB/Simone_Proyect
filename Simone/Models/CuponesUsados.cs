using System;
using Simone.Models;

namespace Simone.Models
{
    public class CuponesUsados
    {
        public int ClienteID { get; set; }  // Clave foránea con Clientes
        public int PromocionID { get; set; }  // Clave foránea con Promociones
        public DateTime? FechaUso { get; set; }  // Puede ser nulo

        // Relación con Clientes
        public Cliente Clientes { get; set; }

        // Relación con Promociones
        public Promociones Promocion { get; set; }
    }
}
