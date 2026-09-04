using System;
using System.Collections.Generic;

namespace CapaModelo.DTOs
{
    /// <summary>
    /// AC-10: ViewModels tipados para el ciclo de vida de Condiciones y Limitaciones.
    /// </summary>
    public class CondicionesLimitacionesViewModel
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int? InspeccionId { get; set; }
        public int? InformeId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NumeroAocr { get; set; }
        public int Version { get; set; } = 1;
        public string Estado { get; set; } = AocrEstadoCl.ClNoGenerada;
        public string EstadoEtiqueta => AocrEstadoCl.ObtenerEtiqueta(Estado);
        public string EstadoBadgeCss => AocrEstadoCl.ObtenerBadgeCss(Estado);
        public bool Vigente { get; set; } = true;

        // Datos del Operador y Expediente
        public string Compania { get; set; }
        public string OperadorExtranjero { get; set; }
        public string RepresentanteTecnico { get; set; }
        public string PaisOperador { get; set; }
        public string NumeroAoc { get; set; }
        public string TipoOperacion { get; set; }
        public string RutasAutorizadas { get; set; }
        public string AlcanceAutorizado { get; set; }

        // Estaciones autorizadas y fechas independientes (AC-02)
        public List<SolicitudEstacionInspeccion> Estaciones { get; set; } = new List<SolicitudEstacionInspeccion>();

        // Aeronaves/Equipos
        public List<AeronaveSolicitud> Aeronaves { get; set; } = new List<AeronaveSolicitud>();

        // Contenido técnico editable/revisable
        public string CondicionesAprobadas { get; set; }
        public string Limitaciones { get; set; }
        public string Observaciones { get; set; }

        // Datos del Inspector
        public int? InspectorUsuarioId { get; set; }
        public string InspectorNombre { get; set; }
        public DateTime? FechaInformeTecnico { get; set; }
        public DateTime FechaGeneracion { get; set; } = DateTime.Now;

        // Datos del Coordinador
        public int? CoordinadorUsuarioId { get; set; }
        public string CoordinadorNombre { get; set; }
        public string ObservacionCoordinador { get; set; }
        public DateTime? FechaRevisionCoordinador { get; set; }

        // Datos de DIRCAV
        public int? DircavUsuarioId { get; set; }
        public string DircavNombre { get; set; }
        public string ObservacionDircav { get; set; }
        public DateTime? FechaFirmaDircav { get; set; }

        // Integridad y metadatos
        public string HashPdf { get; set; }
        public string HashPdfFirmado { get; set; }
        public string CodigoVerificacion { get; set; }
        public string RutaPdf { get; set; }
        public long? TamanioPdf { get; set; }
        public bool TienePdfFirmado => !string.IsNullOrWhiteSpace(RutaPdfFirmado) || !string.IsNullOrWhiteSpace(HashPdfFirmado);
        public string RutaPdfFirmado { get; set; }

        // Permisos de UI por rol y estado
        public bool PuedeEditarInspector { get; set; }
        public bool PuedeRemitirCoordinador { get; set; }
        public bool PuedeRevisarCoordinador { get; set; }
        public bool PuedeDevolverInspector { get; set; }
        public bool PuedeRemitirDircav { get; set; }
        public bool PuedeRevisarDircav { get; set; }
        public bool PuedeDevolverCoordinador { get; set; }
        public bool PuedeFirmarDircav { get; set; }
        public bool PuedeDescargar { get; set; }
        public bool PuedeVerVistaPrevia { get; set; }

        // Estado del AOCR (para verificar cierre dual)
        public string EstadoAocr { get; set; }
        public bool AocrFirmadoDirdac { get; set; }
        public bool ClFirmadaDircav => string.Equals(Estado, AocrEstadoCl.ClFirmadaDircav, StringComparison.OrdinalIgnoreCase);
        public bool ExpedienteListoParaCierre => ClFirmadaDircav && AocrFirmadoDirdac;
    }

    public class CondicionesLimitacionesSaveRequest
    {
        public int SolicitudId { get; set; }
        public int? InspeccionId { get; set; }
        public string CondicionesAprobadas { get; set; }
        public string Limitaciones { get; set; }
        public string Observaciones { get; set; }
        public string RutasAutorizadas { get; set; }
        public string AlcanceAutorizado { get; set; }
    }

    public class CondicionesLimitacionesTransicionRequest
    {
        public int SolicitudId { get; set; }
        public string Observacion { get; set; }
    }

    public class CondicionesLimitacionesFirmaRequest
    {
        public int SolicitudId { get; set; }
        public string PasswordCertificado { get; set; }
        public int DircavUsuarioId { get; set; }
        public string DircavUsuarioNombre { get; set; }
        public string RolSolicitante { get; set; }
        public byte[] CertificadoBytes { get; set; }
    }

    public class CondicionesLimitacionesResultado
    {
        public bool Exitoso { get; set; }
        public int HttpStatusCode { get; set; } = 200;
        public string Mensaje { get; set; }
        public int DocumentoId { get; set; }
        public int Version { get; set; }
        public string Estado { get; set; }
        public string HashPdf { get; set; }
        public string RutaPdf { get; set; }
        public bool Idempotente { get; set; }
        public bool ExpedienteFinalizado { get; set; }
    }
}
