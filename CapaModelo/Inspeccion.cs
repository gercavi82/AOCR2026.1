using System;

namespace CapaModelo
{
    public class Inspeccion
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }

        public int? CodigoInspector { get; set; }
        public string NombreInspector { get; set; } // ✅ NUEVA propiedad para mostrar el nombre

        public DateTime? FechaProgramada { get; set; }
        public TimeSpan? HoraProgramada { get; set; }
        public int? DuracionEstimada { get; set; }

        public string Lugar { get; set; }
        public string Latitud { get; set; }         // ✅ Añadida para planificación GPS
        public string Longitud { get; set; }        // ✅ Añadida para planificación GPS

        public string Tipo { get; set; }

        public string ObservacionesGenerales { get; set; }
        public string Comentarios { get; set; }
        public string HallazgosPrincipales { get; set; }

        public string Estado { get; set; }
        public string Resultado { get; set; }
        public string RutaInforme { get; set; }

        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Alias de compatibilidad para vistas antiguas
        public string InformePdf
        {
            get { return RutaInforme; }
            set { RutaInforme = value; }
        }
    }
}
