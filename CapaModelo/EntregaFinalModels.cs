using System;
using System.Collections.Generic;

namespace CapaModelo
{
    public static class EstadosEntregaFinal
    {
        public const string NoSolicitada = "ENTREGA_NO_SOLICITADA";
        public const string Encolada = "ENTREGA_ENCOLADA";
        public const string EnProceso = "ENTREGA_EN_PROCESO";
        public const string Parcial = "ENTREGA_PARCIAL";
        public const string Completa = "ENTREGA_COMPLETA";
        public const string FallidaReintentable = "ENTREGA_FALLIDA_REINTENTABLE";
        public const string FallidaDefinitiva = "ENTREGA_FALLIDA_DEFINITIVA";
    }

    public sealed class EntregaFinalActor
    {
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public string RolActivo { get; set; }
        public string CompaniaCodigo { get; set; }
        public string CompaniaNombre { get; set; }
        public string Ip { get; set; }
        public bool TienePermiso { get; set; }
    }

    public sealed class SolicitarEntregaFinalRequest
    {
        public int SolicitudId { get; set; }
        public long VersionExpedienteEsperada { get; set; }
        public string IdempotencyKey { get; set; }
        public string BaseUrl { get; set; }
        public EntregaFinalActor Actor { get; set; }
    }

    public sealed class EntregaFinalResult
    {
        public bool Exito { get; set; }
        public bool Idempotente { get; set; }
        public int HttpStatusCode { get; set; }
        public string Codigo { get; set; }
        public string Mensaje { get; set; }
        public string EstadoEntrega { get; set; }
        public string EstadoExpediente { get; set; }
        public long VersionExpediente { get; set; }
        public long? EntregaId { get; set; }
        public string CorrelationId { get; set; }

        public static EntregaFinalResult Error(int status, string codigo, string mensaje)
        {
            return new EntregaFinalResult { HttpStatusCode = status, Codigo = codigo, Mensaje = mensaje };
        }
    }

    public sealed class DocumentoFinalDisponibleViewModel
    {
        public long EntregaId { get; set; }
        public int SolicitudId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public int DocumentoId { get; set; }
        public string TipoDocumento { get; set; }
        public string NombreArchivo { get; set; }
        public int Version { get; set; }
        public string Firmante { get; set; }
        public string RolFirmante { get; set; }
        public DateTime? FechaFirma { get; set; }
        public string EstadoEntrega { get; set; }
        public string EstadoCorreo { get; set; }
        public string TipoDestinatario { get; set; }
    }

    public sealed class DocumentosFinalesViewModel
    {
        public string Rol { get; set; }
        public IList<DocumentoFinalDisponibleViewModel> Documentos { get; set; } = new List<DocumentoFinalDisponibleViewModel>();
    }

    public sealed class DescargaFinalAutorizada
    {
        public bool Autorizada { get; set; }
        public int HttpStatusCode { get; set; }
        public string Codigo { get; set; }
        public string Mensaje { get; set; }
        public string RutaFisica { get; set; }
        public string NombreArchivo { get; set; }
        public string MimeType { get; set; }
        public string HashSha256 { get; set; }
        public long Tamanio { get; set; }
    }

    public sealed class EstadoEntregaFinalViewModel
    {
        public long EntregaId { get; set; }
        public int SolicitudId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public int VersionAocr { get; set; }
        public int VersionCl { get; set; }
        public string Estado { get; set; }
        public string CorrelationId { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaCompletada { get; set; }
        public int Destinatarios { get; set; }
        public int CorreosEnviados { get; set; }
        public int CorreosFallidos { get; set; }
    }
}
