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
        public string UrlVer { get; set; }
        public string UrlDescargar { get; set; }
        public DateTime? FechaDocumento { get; set; }
        public bool Disponible { get; set; }
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
    }
}