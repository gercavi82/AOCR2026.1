using System;

namespace CapaModelo
{
    public class Inspeccion
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }

        public string NumeroInspeccion { get; set; }
        public string Tipo { get; set; }

        public DateTime? FechaProgramada { get; set; }
        public TimeSpan? HoraProgramada { get; set; }

        public DateTime? FechaRealizada { get; set; }
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFin { get; set; }

        // ✅ este es tu campo real de asignación
        public int? CodigoInspector { get; set; }

        public string Lugar { get; set; }
        public string Resultado { get; set; }
        public string Comentarios { get; set; }
        public string ObservacionesGenerales { get; set; }
        public string HallazgosPrincipales { get; set; }
        public string Recomendaciones { get; set; }

        public string Estado { get; set; }
        public bool? Completada { get; set; }
        public bool? Aprobada { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
        public string InformePdf { get; set; }  // aocr_tbinspeccion.informe_pdf

    }
}
