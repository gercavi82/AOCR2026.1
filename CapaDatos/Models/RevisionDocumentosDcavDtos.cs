using System;
using System.Collections.Generic;

namespace CapaDatos.Models
{
    public class DocumentosPendientesDcavDto
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Explotador { get; set; }
        public string Pais { get; set; }
        public string TipoTramite { get; set; }
        public int InspectorId { get; set; }
        public string InspectorNombre { get; set; }
        public DateTime FechaEnvio { get; set; }
        public string EstadoFuncional { get; set; }
        public long VersionExpediente { get; set; }
        public int AocrId { get; set; }
        public int VersionAocrEnviada { get; set; }
        public int AocrPdfId { get; set; }
        public string EstadoAocr { get; set; }
        public int InspectorAocrId { get; set; }
        public string CompaniaAocr { get; set; }
        public int CondicionesId { get; set; }
        public int VersionCondicionesEnviada { get; set; }
        public int CondicionesPdfId { get; set; }
        public string EstadoCondiciones { get; set; }
        public int InspectorCondicionesId { get; set; }
        public string CompaniaCondiciones { get; set; }
        public int InformeTecnicoId { get; set; }
        public int InformeTecnicoPdfId { get; set; }
        public string InformeRuta { get; set; }
        public string InformeHash { get; set; }
        public int LvEaeId { get; set; }
        public int LvEaePdfId { get; set; }
        public string LvEaeRuta { get; set; }
        public string LvEaeHash { get; set; }
        public int ObservacionesAbiertas { get; set; }
        public string UltimaAccion { get; set; }
        public DateTime FechaUltimaAccion { get; set; }
        public string CodigoCompania { get; set; }
    }

    public sealed class RevisionDocumentosDcavDto : DocumentosPendientesDcavDto
    {
        public IList<HistorialDocumentoDcavDto> Historial { get; set; } = new List<HistorialDocumentoDcavDto>();
    }

    public sealed class HistorialDocumentoDcavDto
    {
        public int Id { get; set; }
        public string Accion { get; set; }
        public int? UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public string Rol { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Observacion { get; set; }
        public DateTime Fecha { get; set; }
        public string CorrelationId { get; set; }
    }
}
