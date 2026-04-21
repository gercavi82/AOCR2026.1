using System;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Evento de la línea de tiempo unificada del expediente AOCR.
    /// Consumido por el detalle de "Revisar solicitud" para mostrar
    /// trazabilidad completa: cambios de estado, decisiones documentales,
    /// cargas del RT, subsanaciones, etc.
    /// </summary>
    public class EventoTrazabilidad
    {
        public int CodigoSolicitud { get; set; }
        public DateTime FechaEvento { get; set; }
        public int? UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public string Rol { get; set; }
        public string Modulo { get; set; }
        public string Accion { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Observacion { get; set; }
        public int? CodigoDocumento { get; set; }
        public string DocumentoAfectado { get; set; }
        public string Fuente { get; set; }
    }

    /// <summary>
    /// Registro de un documento cargado por el RT como respuesta a
    /// una subsanación (tabla aocr_tbdocumento_subsanacion).
    /// </summary>
    public class DocumentoSubsanacionRegistro
    {
        public int CodigoDocumento { get; set; }
        public int CodigoSubsanacion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaArchivo { get; set; }
        public string TipoDocumento { get; set; }
        public long? TamanioBytes { get; set; }
        public DateTime FechaCarga { get; set; }
        public int? CodigoUsuarioCarga { get; set; }
        public string UsuarioCargaNombre { get; set; }
        public string ObservacionMotivo { get; set; }
        public DateTime? FechaSubsanacionSolicitada { get; set; }
    }
}
