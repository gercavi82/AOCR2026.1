using System;
using System.Collections.Generic;

namespace CapaNegocio.DTOs
{
    public sealed class AprobarDocumentosDcavRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public int UsuarioDcavId { get; set; }
        public string Rol { get; set; }
        public string EstadoEsperado { get; set; }
        public int AocrId { get; set; }
        public int AocrPdfId { get; set; }
        public int VersionAocr { get; set; }
        public int CondicionesId { get; set; }
        public int CondicionesPdfId { get; set; }
        public int VersionCondiciones { get; set; }
        public long VersionExpediente { get; set; }
        public string ClaveIdempotencia { get; set; }
        public string Ip { get; set; }
        public string CorrelationId { get; set; }
    }

    public sealed class ResultadoAprobacionDocumentosDcav
    {
        public bool Exitoso { get; set; }
        public bool YaProcesado { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public int AocrId { get; set; }
        public int CondicionesId { get; set; }
        public DateTime Fecha { get; set; }
    }

    public sealed class ResultadoValidacionAprobacion
    {
        public bool Valido { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public IList<string> Errores { get; set; } = new List<string>();
    }
}
