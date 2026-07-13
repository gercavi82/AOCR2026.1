using System.Collections.Generic;
using CapaDatos.Models;

namespace CapaNegocio.DTOs
{
    public sealed class ObservacionDevolucionDcavRequest
    {
        public string TipoDocumento { get; set; }
        public string Seccion { get; set; }
        public string Campo { get; set; }
        public string Texto { get; set; }
    }

    public sealed class DevolverDocumentosDcavRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public long VersionExpediente { get; set; }
        public int AocrId { get; set; }
        public int VersionAocr { get; set; }
        public int AocrPdfId { get; set; }
        public int CondicionesId { get; set; }
        public int VersionCondiciones { get; set; }
        public int CondicionesPdfId { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public string Rol { get; set; }
        public string Ip { get; set; }
        public string CorrelationId { get; set; }
        public IList<ObservacionDevolucionDcavRequest> Observaciones { get; set; } = new List<ObservacionDevolucionDcavRequest>();
    }

    public sealed class DevolucionDocumentosDcavResultado
    {
        public bool Exitoso { get; set; }
        public bool YaProcesado { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public string EstadoNuevo { get; set; }
        public VersionCorreccionDcavRegistro Aocr { get; set; }
        public VersionCorreccionDcavRegistro Condiciones { get; set; }
    }

    public sealed class ValidacionDevolucionDocumentosDcav
    {
        public bool Valido { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public IList<string> Errores { get; set; } = new List<string>();
    }

    public sealed class CambiarEstadoObservacionDcavRequest
    {
        public int ObservacionId { get; set; }
        public int SolicitudId { get; set; }
        public int DocumentoCorreccionId { get; set; }
        public int UsuarioId { get; set; }
        public string Rol { get; set; }
    }
}
