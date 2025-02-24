using System;

namespace Simone.Models
{
    public class ImagenesProductos
    {
        public int ImagenID { get; set; }  // Clave primaria
        public int ProductoID { get; set; }  // Clave foránea con Productos
        public string RutaImagen { get; set; }  // Ruta de la imagen

        // Relación con Productos
        public Productos Producto { get; set; }
    }
}

