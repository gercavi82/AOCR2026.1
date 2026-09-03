using System;

namespace CapaModelo
{
    /// <summary>
    /// AC-02: Entidad que representa una estación solicitada y sus fechas de inspección independientes.
    /// Relación: Solicitud -> Estaciones solicitadas -> Inspecciones -> Fechas.
    /// </summary>
    public class SolicitudEstacionInspeccion
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public string EstacionCodigo { get; set; }
        public string EstacionNombre { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int? InspectorId { get; set; }
        public string InspectorNombre { get; set; }
        public int? InspeccionId { get; set; }
        public string Estado { get; set; } = "SOLICITADA";
        public int Version { get; set; } = 1;
        public bool Activo { get; set; } = true;
        public string Observacion { get; set; }
        public DateTime CreadoEn { get; set; } = DateTime.Now;
        public int? CreadoPor { get; set; }
        public DateTime? ActualizadoEn { get; set; }
        public int? ActualizadoPor { get; set; }

        // Propiedad calculada para formato legible de fechas en vistas y PDF
        public string RangoFechasTexto
        {
            get
            {
                if (FechaInicio == default(DateTime))
                {
                    return "Fecha no definida";
                }

                if (FechaFin == default(DateTime) || FechaFin.Date == FechaInicio.Date)
                {
                    return FechaInicio.ToString("dd/MM/yyyy");
                }

                return string.Format("{0:dd/MM/yyyy} al {1:dd/MM/yyyy}", FechaInicio, FechaFin);
            }
        }
    }
}
