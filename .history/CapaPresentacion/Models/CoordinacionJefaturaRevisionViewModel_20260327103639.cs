using System;
using System.Collections.Generic;
using CapaModelo;

namespace CapaPresentacion.Models
{
    public class CoordinacionJefaturaRevisionViewModel
    {
        public List<SolicitudAOCR> SolicitudesControlDocumental { get; set; } = new List<SolicitudAOCR>();
        public List<SolicitudAOCR> SolicitudesAocrRevision { get; set; } = new List<SolicitudAOCR>();
        public List<Inspeccion> InspeccionesSeguimiento { get; set; } = new List<Inspeccion>();
    }

    public class ValidarAocrViewModel
    {
        public List<ValidarAocrSolicitudItemViewModel> Items { get; set; } = new List<ValidarAocrSolicitudItemViewModel>();
    }

    public class ValidarAocrSolicitudItemViewModel
    {
        public SolicitudAOCR Solicitud { get; set; }
        public Inspeccion Inspeccion { get; set; }
        public InspeccionInformeTecnico Informe { get; set; }
        public Certificado Certificado { get; set; }
        public List<AeronaveSolicitud> Aeronaves { get; set; } = new List<AeronaveSolicitud>();
        public List<ValidarAocrDocumentoItemViewModel> Documentos { get; set; } = new List<ValidarAocrDocumentoItemViewModel>();
        public bool FirmaCompleta { get; set; }
        public bool PuedeContinuar { get; set; }
        public bool ListoParaEnvioRt { get; set; }
        public string MensajeEstado { get; set; }
        public string MensajeAdvertencia { get; set; }
        public string Firmantes { get; set; }
        public string NumeroAocr { get; set; }
        public DateTime? FechaFirmaFinal { get; set; }
        public DateTime? FechaDisponibilidad { get; set; }
    }

    public class ValidarAocrDocumentoItemViewModel
    {
        public string TipoDocumento { get; set; }
        public string NombreVisible { get; set; }
        public string Estado { get; set; }
        public string Observacion { get; set; }
        public string UrlEditar { get; set; }
        public string UrlVer { get; set; }
        public string UrlDescargar { get; set; }
        public DateTime? FechaDocumento { get; set; }
        public bool Disponible { get; set; }
    }

    public class AocrDocumentoEdicionViewModel
    {
        public int SolicitudId { get; set; }
        public int? InspeccionId { get; set; }
        public string TipoDocumento { get; set; }
        public string NumeroAocr { get; set; }
        public string NombreDocumento { get; set; }

        public string AocOriginalNumero { get; set; }
        public string EstadoOtorgante { get; set; }
        public string NombreExplotador { get; set; }
        public string EstadoExplotador { get; set; }
        public string RazonSocial { get; set; }
        public string DireccionExplotador { get; set; }
        public string TelefonoExplotador { get; set; }
        public string CorreoExplotador { get; set; }

        public string PuntoContactoEcuador { get; set; }
        public string ContactoDireccion { get; set; }
        public string ContactoTelefono { get; set; }
        public string ContactoCorreo { get; set; }
        public string PuntosContactoOperacionales { get; set; }

        public string BaseLegalReferencia { get; set; }
        public string ObservacionesReconocimiento { get; set; }
        public string RepresentanteTecnico { get; set; }
        public string CondicionBaseOperacion { get; set; }
        public string RestriccionesCondiciones { get; set; }
        public string CondicionesAdicionales { get; set; }
        public string ObservacionesValidacionFinal { get; set; }

        public DateTime FechaEmisionDocumento { get; set; }
        public DateTime? FechaExpedicion { get; set; }
        public DateTime? FechaRenovacion { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        public string ElaboradoPor { get; set; }
        public string RevisadoPor { get; set; }
        public string FirmanteNombre { get; set; }
        public string FirmanteCargo { get; set; }

        public List<AocrCondicionAeronaveFilaViewModel> AeronavesCondiciones { get; set; } = new List<AocrCondicionAeronaveFilaViewModel>();
    }

    public class AocrCondicionAeronaveFilaViewModel
    {
        public string ModeloTipo { get; set; }
        public string Matricula { get; set; }
        public string Serie { get; set; }
        public string Uio { get; set; }
        public string Gye { get; set; }
        public string Mec { get; set; }
        public string Ltx { get; set; }
    }

    public class AocrDocumentoPdfViewModel
    {
        public SolicitudAOCR Solicitud { get; set; }
        public Inspeccion Inspeccion { get; set; }
        public InspeccionInformeTecnico Informe { get; set; }
        public Certificado Certificado { get; set; }
        public List<AeronaveSolicitud> Aeronaves { get; set; } = new List<AeronaveSolicitud>();
        public string NumeroAocr { get; set; }
        public string FirmanteFinal { get; set; }
        public string CargoFirmante { get; set; }
        public DateTime FechaEmisionDocumento { get; set; }
        public DateTime? FechaExpedicion { get; set; }
        public DateTime? FechaRenovacion { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public string AocOriginalNumero { get; set; }
        public string EstadoOtorgante { get; set; }
        public string NombreExplotador { get; set; }
        public string EstadoExplotador { get; set; }
        public string RazonSocial { get; set; }
        public string DireccionExplotador { get; set; }
        public string TelefonoExplotador { get; set; }
        public string CorreoExplotador { get; set; }
        public string PuntoContactoEcuador { get; set; }
        public string ContactoDireccion { get; set; }
        public string ContactoTelefono { get; set; }
        public string ContactoCorreo { get; set; }
        public string PuntosContactoOperacionales { get; set; }
        public string BaseLegalReferencia { get; set; }
        public string ObservacionesReconocimiento { get; set; }
        public string RepresentanteTecnico { get; set; }
        public string CondicionBaseOperacion { get; set; }
        public string RestriccionesCondiciones { get; set; }
        public string CondicionesAdicionales { get; set; }
        public string ObservacionesValidacionFinal { get; set; }
        public string ElaboradoPor { get; set; }
        public string RevisadoPor { get; set; }
        public List<AocrCondicionAeronaveFilaViewModel> AeronavesCondiciones { get; set; } = new List<AocrCondicionAeronaveFilaViewModel>();
    }
}