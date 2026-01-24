using System;

namespace CapaModelo
{
    public class Hallazgo
    {
        public int CodigoHallazgo { get; set; }
        public int CodigoInspeccion { get; set; }

        public string Descripcion { get; set; }
        public string Criticidad { get; set; }   // ALTA / MEDIA / BAJA
        public string Estado { get; set; }       // ABIERTO | CERRADO

        // ✅ En BD puede venir null, por eso debe ser nullable
        public DateTime? FechaDeteccion { get; set; }
        public DateTime? FechaCierre { get; set; }

        // ✅ Campos que el DAO usa (faltaban)
        public string AccionCorrectiva { get; set; }
        public string Responsable { get; set; }

        // AUDITORÍA (en BD pueden venir null también)
        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }

        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
    }
}
