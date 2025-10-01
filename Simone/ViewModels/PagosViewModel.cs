namespace Simone.ViewModels.Pagos
{
    public class CuentaPagoVM
    {
        public int BancoId { get; set; }
        public string? BancoCodigo { get; set; }
        public string BancoNombre { get; set; } = "";
        public string? BancoLogoUrl { get; set; }

        public string Numero { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Titular { get; set; } = "";
        public string? Ruc { get; set; }
    }
}
