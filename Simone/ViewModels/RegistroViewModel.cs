using System.ComponentModel.DataAnnotations;

namespace Simone.ViewModels
{
    public class RegistroViewModel
    {
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Phone]
        public string Telefono { get; set; }

        [Required]
        [StringLength(200)]
        public string Direccion { get; set; }

        [StringLength(300)]
        public string Referencia { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; }
    }
}

