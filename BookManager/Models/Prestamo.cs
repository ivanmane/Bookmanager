using System;

namespace BookManager.Models
{
    public class Prestamo
    {
        public int Id { get; set; }
        public int LibroId { get; set; }
        public int LectorId { get; set; }
        public DateTime FechaPrestamo { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public DateTime? FechaDevolucion { get; set; }
    }
}
