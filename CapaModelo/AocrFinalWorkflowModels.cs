using System;
using System.Collections.Generic;

namespace CapaModelo
{
    public sealed class AocrWorkflowActor
    {
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public string RolActivo { get; set; }
        public string Ip { get; set; }
        public bool TienePermiso { get; set; }
    }

    public abstract class AocrWorkflowRequestBase
    {
        public int SolicitudId { get; set; }
        public long VersionEsperada { get; set; }
        public string IdempotencyKey { get; set; }
        public AocrWorkflowActor Actor { get; set; }
    }

    public sealed class RemitirAocrDirdacRequest : AocrWorkflowRequestBase
    {
        public int DocumentoId { get; set; }
        public int VersionAocrEsperada { get; set; }
        public string Observacion { get; set; }
        public string BaseUrl { get; set; }
    }

    public sealed class DevolverAocrDircavRequest : AocrWorkflowRequestBase
    {
        public string Observacion { get; set; }
        public string BaseUrl { get; set; }
    }

    public sealed class FirmarLegalizarAocrRequest : AocrWorkflowRequestBase
    {
        public int DocumentoId { get; set; }
        public int VersionAocrEsperada { get; set; }
        public string RutaPdfFirmado { get; set; }
        public string HashPdfFirmado { get; set; }
        public long TamanioPdfFirmado { get; set; }
        public string NombreFirmante { get; set; }
        public string CargoFirmante { get; set; }
        public string SujetoCertificado { get; set; }
        public string BaseUrl { get; set; }
    }

    public sealed class AocrWorkflowResult
    {
        public bool Exito { get; set; }
        public bool Idempotente { get; set; }
        public int HttpStatusCode { get; set; }
        public string Codigo { get; set; }
        public string Mensaje { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public long VersionAnterior { get; set; }
        public long VersionNueva { get; set; }
        public int? DocumentoId { get; set; }
        public string CorrelationId { get; set; }

        public static AocrWorkflowResult Error(int status, string codigo, string mensaje)
        {
            return new AocrWorkflowResult
            {
                Exito = false,
                HttpStatusCode = status,
                Codigo = codigo,
                Mensaje = mensaje
            };
        }
    }

    public sealed class BandejaAocrDirdacItemViewModel
    {
        public int SolicitudId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public int DocumentoId { get; set; }
        public string Documento { get; set; }
        public int VersionDocumento { get; set; }
        public long VersionExpediente { get; set; }
        public DateTime FechaRemision { get; set; }
        public string UsuarioRemitente { get; set; }
        public string Estado { get; set; }
        public int MinutosPendiente { get; set; }
        public bool PuedeDevolver { get; set; }
        public bool PuedeFirmar { get; set; }
        public string HashDocumento { get; set; }
    }

    public sealed class BandejaAocrDirdacViewModel
    {
        public IList<BandejaAocrDirdacItemViewModel> Expedientes { get; set; } = new List<BandejaAocrDirdacItemViewModel>();
        public int TotalPendientes { get { return Expedientes == null ? 0 : Expedientes.Count; } }
    }

    public sealed class DetalleAocrDirdacViewModel
    {
        public BandejaAocrDirdacItemViewModel Expediente { get; set; }
        public string EstadoCondicionesLimitaciones { get; set; }
        public int VersionCondicionesLimitaciones { get; set; }
        public bool CondicionesFirmadasDircav { get; set; }
        public bool AocrFirmadaDirdac { get; set; }
    }

    public sealed class AocrWorkflowResponse
    {
        public bool Ok { get; set; }
        public string Codigo { get; set; }
        public string Mensaje { get; set; }
        public string Estado { get; set; }
        public long Version { get; set; }
        public string CorrelationId { get; set; }
    }
}
