// Simone/Models/Vendedor.cs
using System.Collections.Generic;

namespace Simone.Models
{
    public class Vendedor
    {
        public int VendedorId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;

        // -------- Hub de envío (donde entrega sus productos) --------
        public int? HubId { get; set; }
        public HubEnvio? Hub { get; set; }

        // -------- Alianza de envío (configuración compartida) --------
        public int? AlianzaId { get; set; }
        public AlianzaEnvio? Alianza { get; set; }

        // Contacto rápido (opcional)
        public ICollection<ContactoTienda> Contactos { get; set; } = new List<ContactoTienda>();
        public ICollection<CuentaBancaria> Cuentas { get; set; } = new List<CuentaBancaria>();
    }

    public class ContactoTienda
    {
        public int ContactoTiendaId { get; set; }
        public int VendedorId { get; set; }
        public Vendedor? Vendedor { get; set; }

        // "whatsapp", "telefono", "email"
        public string Tipo { get; set; } = "whatsapp";
        public string Valor { get; set; } = string.Empty;
        public bool Principal { get; set; } = true;
    }

    public class Banco
    {
        public int BancoId { get; set; }
        // "pichincha", "guayaquil", etc. (único)
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public bool Activo { get; set; } = true;

        public ICollection<CuentaBancaria> Cuentas { get; set; } = new List<CuentaBancaria>();
    }

    public class CuentaBancaria
    {
        public int CuentaBancariaId { get; set; }

        public int VendedorId { get; set; }
        public Vendedor? Vendedor { get; set; }

        public int BancoId { get; set; }
        public Banco? Banco { get; set; }

        public string Numero { get; set; } = string.Empty;            // 20-30
        public string Tipo { get; set; } = "Ahorros";                 // "Ahorros" | "Corriente"
        public string Titular { get; set; } = string.Empty;
        public string? Ruc { get; set; }
        public bool Activo { get; set; } = true;
        public int Orden { get; set; } = 0;                           // para ordenar en la vista
        public string? LogoPath { get; internal set; }
    }
}
