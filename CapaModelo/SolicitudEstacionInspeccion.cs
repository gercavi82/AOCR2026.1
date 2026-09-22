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
        
        // Fechas de rango (legacy, mantener para backward compatibility)
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        
        // Nueva: Fecha única de inspección por ubicación (Punto 4 - refactor)
        public DateTime? FechaInspeccion { get; set; }
        
        // Para "Otra provincia/localidad"
        public string ProvinciaOtrosNombre { get; set; }
        
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
        // Prioriza FechaInspeccion (nueva) sobre rango legacy
        public string RangoFechasTexto
        {
            get
            {
                if (FechaInspeccion.HasValue && FechaInspeccion != default(DateTime))
                {
                    return FechaInspeccion.Value.ToString("dd/MM/yyyy");
                }

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

    /// <summary>
    /// Excepción lanzada cuando la versión enviada no coincide con la versión persistida en BD (AC-02 concurrencia optimista).
    /// </summary>
    public class EstacionVersionConflictException : Exception
    {
        public EstacionVersionConflictException(string message) : base(message) { }
    }

    /// <summary>
    /// Excepción lanzada cuando se intenta registrar una estación duplicada en la misma solicitud (AC-02 unicidad).
    /// </summary>
    public class EstacionDuplicadaException : Exception
    {
        public EstacionDuplicadaException(string message) : base(message) { }
    }
}
