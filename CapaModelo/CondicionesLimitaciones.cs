using System;

namespace CapaModelo
{
    /// <summary>
    /// AC-10: Entidad de dominio para Condiciones y Limitaciones (CL).
    /// Mantiene la versión, contenido, estados independientes y trazabilidad institucional.
    /// </summary>
    public class CondicionesLimitaciones
    {
        public int Id { get; set; }
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public int? CodigoInforme { get; set; }
        public string NumeroAocr { get; set; }
        public int Version { get; set; } = 1;
        public string Estado { get; set; } = AocrEstadoCl.ClBorrador;
        public bool Vigente { get; set; } = true;

        // Contenido técnico canónico
        public string Compania { get; set; }
        public string OperadorExtranjero { get; set; }
        public string RepresentanteTecnico { get; set; }
        public string TipoOperacion { get; set; }
        public string RutasAutorizadas { get; set; }
        public string AlcanceAutorizado { get; set; }
        public string CondicionesAprobadas { get; set; }
        public string Limitaciones { get; set; }
        public string Observaciones { get; set; }

        // Inspector
        public int? InspectorUsuarioId { get; set; }
        public string InspectorNombre { get; set; }
        public DateTime FechaGeneracion { get; set; } = DateTime.Now;

        // Coordinador
        public int? CoordinadorUsuarioId { get; set; }
        public string CoordinadorNombre { get; set; }
        public string ObservacionCoordinador { get; set; }
        public DateTime? FechaRevisionCoordinador { get; set; }

        // DIRCAV
        public int? DircavUsuarioId { get; set; }
        public string DircavNombre { get; set; }
        public string ObservacionDircav { get; set; }
        public DateTime? FechaFirmaDircav { get; set; }

        // Almacenamiento e Integridad
        public string RutaPdfBorrador { get; set; }
        public string RutaPdfFirmado { get; set; }
        public string HashPdf { get; set; }
        public string HashPdfFirmado { get; set; }
        public long? TamanioPdf { get; set; }
        public string CodigoVerificacion { get; set; }
        public long VersionConcurrencia { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
