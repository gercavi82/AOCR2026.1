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
        public List<CoordinacionJefaturaInspeccionSeguimientoItemViewModel> InspeccionesSeguimientoItems { get; set; } = new List<CoordinacionJefaturaInspeccionSeguimientoItemViewModel>();
    }

    public class CoordinacionJefaturaInspeccionSeguimientoItemViewModel
    {
        public Inspeccion Inspeccion { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public string NumeroInspeccion { get; set; }
        public string NumeroSolicitud { get; set; }
        public string OperadorNombre { get; set; }
        public string EstadoNormalizado { get; set; }
        public string EtapaActual { get; set; }
        public string InspectorAsignado { get; set; }
        public string MensajeOperativo { get; set; }
        public string ResumenAcciones { get; set; }
        public string MensajeSinAcciones { get; set; }
        public bool PuedeAceptarSolicitud { get; set; }
        public bool PuedeObservar { get; set; }
        public bool PuedeCerrar { get; set; }
        public bool PuedeAsignarInspector { get; set; }
    }

    public class ValidarAocrViewModel
    {
        public List<ValidarAocrSolicitudItemViewModel> Items { get; set; } = new List<ValidarAocrSolicitudItemViewModel>();
        public string MensajeInformativo { get; set; }
        public string MensajeError { get; set; }
        public int SolicitudId { get; set; }
        public string CodigoSolicitud { get; set; }
        public string Operadora { get; set; }
        public string EstadoSolicitud { get; set; }
        public string CodigoAocr { get; set; }
        public DateTime? FechaGeneracionAocr { get; set; }
        public bool InformeAprobadoDireccion { get; set; }
        public bool AocrExiste { get; set; }
        public bool AocrFirmada { get; set; }
        public bool CondicionesExisten { get; set; }
        public bool CondicionesFirmadas { get; set; }
        public bool DocumentoUnificado { get; set; }
        public bool PuedeGenerarAocr { get; set; }
        public bool PuedeVerAocr { get; set; }
        public bool PuedeDescargarAocr { get; set; }
        public bool PuedeFirmarAocr { get; set; }
        public bool PuedeFirmarCondiciones { get; set; }
        public bool PuedeFinalizar { get; set; }
        public bool FirmaDigitalCargada { get; set; }
        public string UsuarioFirma { get; set; }
        public string RolFirma { get; set; }
        public IList<DocumentoFirmaAocrViewModel> DocumentosFirma { get; set; } = new List<DocumentoFirmaAocrViewModel>();
    }

    public class DocumentoFirmaAocrViewModel
    {
        public string Tipo { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Estado { get; set; }
        public string EstadoVisible { get; set; }
        public DateTime? Fecha { get; set; }
        public bool RutaDisponible { get; set; }
        public bool PuedeGenerar { get; set; }
        public bool PuedeVer { get; set; }
        public bool PuedeDescargar { get; set; }
        public bool PuedeFirmar { get; set; }
        public bool EstaFirmado { get; set; }
        public bool EsUnificado { get; set; }
        public string UrlGenerar { get; set; }
        public string UrlVer { get; set; }
        public string UrlDescargar { get; set; }
        public string UrlFirmar { get; set; }
        public string ErrorDocumento { get; set; }
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
        public string EstadoSolicitud { get; set; }
        public bool PuedeEnviarADirdac { get; set; }
        public bool PuedeAprobarFinal { get; set; }
        public bool PuedeSolicitarModificacion { get; set; }
        public string CamposFaltantes { get; set; }
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
        public string UrlFirmar { get; set; }
        public DateTime? FechaDocumento { get; set; }
        public bool Disponible { get; set; }
        public bool Firmado { get; set; }
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
        public bool UsaPosicionFirmaPersonalizada { get; set; }
        public int NumeroPaginaFirma { get; set; } = 1;
        public string PosicionFirmaX { get; set; }
        public string PosicionFirmaY { get; set; }
        public string AnchoFirma { get; set; }
        public string AltoFirma { get; set; }

        public List<AocrCondicionAeronaveFilaViewModel> AeronavesCondiciones { get; set; } = new List<AocrCondicionAeronaveFilaViewModel>();
    }

    public class AocrDocumentoValidacionNoDisponibleViewModel
    {
        public int SolicitudId { get; set; }
        public string TipoDocumento { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NumeroAocr { get; set; }
        public string NombreExplotador { get; set; }
        public string EstadoSolicitud { get; set; }
        public string Motivo { get; set; }
        public string Referencia { get; set; }
        public bool PuedeVolverBandeja { get; set; } = true;
        public bool PuedeAbrirExpediente { get; set; }
    }

    public class AocrFirmaPosicionEdicionViewModel
    {
        public int SolicitudId { get; set; }
        public int? InspeccionId { get; set; }
        public string TipoDocumento { get; set; }
        public string RolFirmante { get; set; }
        public int NumeroPaginaFirma { get; set; }
        public string PosicionFirmaX { get; set; }
        public string PosicionFirmaY { get; set; }
        public string AnchoFirma { get; set; }
        public string AltoFirma { get; set; }
    }

    public class FirmarAocrRequest : AocrDocumentoEdicionViewModel
    {
        public int? AocrId { get; set; }
        public string ModoFirma { get; set; }
        public System.Web.HttpPostedFileBase CertificadoDigital { get; set; }
        public string PasswordCertificado { get; set; }
        public decimal? PosicionX { get; set; }
        public decimal? PosicionY { get; set; }
        public decimal? AnchoFirmaDecimal { get; set; }
        public decimal? AltoFirmaDecimal { get; set; }
        public int? PaginaFirma { get; set; }
        public string NombreFirmante { get; set; }
        public string CargoFirmante { get; set; }
        public string RutaPdfOrigen { get; set; }
        public int? DocumentoId { get; set; }
    }

    public class FirmaAocrResult
    {
        public bool Ok { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public int SolicitudId { get; set; }
        public int AocrId { get; set; }
        public string RutaOrigen { get; set; }
        public string RutaFirmada { get; set; }
        public string HashPdfFirmado { get; set; }
        public long TamanioPdfFirmado { get; set; }
        public string EstadoNuevo { get; set; }
        public string UrlDescarga { get; set; }
        public string RedirectUrl { get; set; }
    }

    public class FirmaAocrInstitucionalViewModel
    {
        public int SolicitudId { get; set; }
        public int AocrId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Operadora { get; set; }
        public string CodigoAocr { get; set; }
        public string EstadoSolicitud { get; set; }
        public string EstadoAocr { get; set; }
        public string InformeTecnicoEstado { get; set; }
        public string ResultadoTecnico { get; set; }
        public string ResponsableFirma { get; set; }
        public string UsuarioActual { get; set; }
        public string RolActual { get; set; }
        public string CargoFirmante { get; set; }
        public DateTime? FechaGeneracion { get; set; }
        public DateTime? FechaFirma { get; set; }
        public string NombreArchivoPdf { get; set; }
        public string NombreArchivoFirmado { get; set; }
        public bool PdfExiste { get; set; }
        public bool PdfFirmadoExiste { get; set; }
        public long TamanioPdf { get; set; }
        public long TamanioPdfFirmado { get; set; }
        public string HashPdfFirmado { get; set; }
        public string RutaPdf { get; set; }
        public string RutaPdfFirmado { get; set; }
        public bool PuedeGenerar { get; set; }
        public bool PuedeRegenerar { get; set; }
        public bool PuedeFirmar { get; set; }
        public bool InformeAprobado { get; set; }
        public bool DocumentoCompleto { get; set; }
        public string MotivoBloqueo { get; set; }
        public List<string> CamposFaltantes { get; set; } = new List<string>();
        public string EstadoExplotador { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public DateTime? FechaEmisionDocumento { get; set; }
        public string AocOriginalNumero { get; set; }
        public string PermisoOperacionCnac { get; set; }
        public string CondicionBaseOperacion { get; set; }
        public bool PuedeGuardarDatos { get; set; }
        public bool PuedeEditarDocumentos { get; set; }
        public bool PuedeEnviarRevisionDcav { get; set; }
        public string EstadoProcesoCentral { get; set; }
        public string UrlGuardarDatos { get; set; }
        public string UrlGenerar { get; set; }
        public string UrlVerPdf { get; set; }
        public string UrlDescargarPdf { get; set; }
        public string UrlVerPdfFirmado { get; set; }
        public string UrlDescargarFirmado { get; set; }
        public string UrlFirmar { get; set; }
        public string UrlVolverBandeja { get; set; }
        public string UrlCompletarDatos { get; set; }
        public List<FirmaAocrDocumentoItemViewModel> Documentos { get; set; } = new List<FirmaAocrDocumentoItemViewModel>();
        public bool AmbosDocumentosFirmados { get; set; }
    }

    public class FirmaAocrDocumentoItemViewModel
    {
        public string TipoDocumento { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Estado { get; set; }
        public string NombreArchivoPdf { get; set; }
        public string NombreArchivoFirmado { get; set; }
        public bool PdfExiste { get; set; }
        public bool Firmado { get; set; }
        public bool PuedeGenerar { get; set; }
        public bool PuedeFirmar { get; set; }
        public int Paginas { get; set; }
        public long TamanioPdf { get; set; }
        public long TamanioPdfFirmado { get; set; }
        public string HashPdfFirmado { get; set; }
        public DateTime? FechaGeneracion { get; set; }
        public DateTime? FechaFirma { get; set; }
        public string UrlGenerar { get; set; }
        public string UrlVerPdf { get; set; }
        public string UrlDescargarPdf { get; set; }
        public string UrlVerPdfFirmado { get; set; }
        public string UrlDescargarFirmado { get; set; }
        public string UrlFirmar { get; set; }
    }

    public class FirmaAocrPendienteRowViewModel
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Operadora { get; set; }
        public string InspectorResponsable { get; set; }
        public string EstadoProceso { get; set; }
        public string Etapa { get; set; }
        public string SiguienteAccion { get; set; }
        public DateTime FechaEstado { get; set; }
        public bool PdfReconocimientoGenerado { get; set; }
        public bool PdfCondicionesGenerado { get; set; }
        public bool ReconocimientoFirmado { get; set; }
        public bool CondicionesFirmadas { get; set; }
        public string UrlGestionar { get; set; }
    }

    public class FirmaAocrPendientesViewModel
    {
        public IList<FirmaAocrPendienteRowViewModel> Items { get; set; } = new List<FirmaAocrPendienteRowViewModel>();
        public int Total { get; set; }
        public int PendientesFirma { get; set; }
        public int Parciales { get; set; }
        public int Completos { get; set; }
        public int Observados { get; set; }
        public int Enviados { get; set; }
        public bool EsBandejaInspector { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
    }

    public class FirmarAocrInstitucionalRequest
    {
        public int SolicitudId { get; set; }
        public int? AocrId { get; set; }
        public string TipoDocumento { get; set; }
        public System.Web.HttpPostedFileBase CertificadoDigital { get; set; }
        public string PasswordCertificado { get; set; }
        public int PaginaFirma { get; set; }
        public string PosicionFirma { get; set; }
    }

    public class GuardarDatosFirmaAocrRequest
    {
        public int SolicitudId { get; set; }
        public string EstadoExplotador { get; set; }
        public DateTime? FechaVencimiento { get; set; }
    }

    public class FirmarAocrInstitucionalResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public int SolicitudId { get; set; }
        public int AocrId { get; set; }
        public string RutaPdfOrigen { get; set; }
        public string RutaPdfFirmado { get; set; }
        public string HashPdfFirmado { get; set; }
        public long TamanioPdfFirmado { get; set; }
        public string EstadoAocrNuevo { get; set; }
        public string EstadoSolicitudNuevo { get; set; }
        public string UrlDescarga { get; set; }
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
