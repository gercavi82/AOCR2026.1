using System;

namespace CapaModelo.Common
{
    public class AocrBandejaDocumentoRow
    {
        public int SolicitudId { get; set; }
        public string NumeroSolicitud { get; set; }
        public DateTime? FechaSolicitud { get; set; }
        public int? TipoSolicitud { get; set; }
        public string EstadoSolicitudRaw { get; set; }
        public string NombreExplotador { get; set; }
        public string NumeroAocBase { get; set; }
        public string CompaniasSeleccionadas { get; set; }
        public int CodigoUsuario { get; set; }
        public int? CodigoInspectorSolicitud { get; set; }
        public string InspectorNombreSolicitud { get; set; }
        public string InspectorApoyoNombreSolicitud { get; set; }
        public int? InspeccionId { get; set; }
        public string NumeroInspeccion { get; set; }
        public string EstadoInspeccionRaw { get; set; }
        public string ResultadoInspeccionRaw { get; set; }
        public int? CodigoInspectorInspeccion { get; set; }
        public string InspectorPrincipalNombreInspeccion { get; set; }
        public DateTime? FechaProgramadaInspeccion { get; set; }
        public int? InformeId { get; set; }
        public string EstadoInformeTecnicoRaw { get; set; }
        public string ResultadoTecnicoFinalRaw { get; set; }
        public bool? InformeFirmadoInspector { get; set; }
        public bool? InformeFirmadoDirdac { get; set; }
        public string RutaInformePdf { get; set; }
        public string RutaInformeFirmadoPdf { get; set; }
        public DateTime? FechaFirmaInformeInspector { get; set; }
        public DateTime? FechaFirmaInformeDireccion { get; set; }
        public DateTime? FechaEnvioInformeDirdac { get; set; }
        public int? CertificadoId { get; set; }
        public string NumeroAocrCertificado { get; set; }
        public string EstadoCertificadoRaw { get; set; }
        public string RutaCertificadoPdf { get; set; }
        public DateTime? FechaEmisionCertificado { get; set; }
        public DateTime? FechaActualizacionCertificado { get; set; }
        public string EmitidoPor { get; set; }
        public string AprobadoPor { get; set; }
        public int? FirmaReconocimientoId { get; set; }
        public string NumeroAocrReconocimiento { get; set; }
        public string RutaReconocimientoFirmado { get; set; }
        public string NombreFirmanteReconocimiento { get; set; }
        public string CargoFirmanteReconocimiento { get; set; }
        public DateTime? FechaFirmaReconocimiento { get; set; }
        public int? FirmaCondicionesId { get; set; }
        public string RutaCondicionesFirmado { get; set; }
        public string NombreFirmanteCondiciones { get; set; }
        public string CargoFirmanteCondiciones { get; set; }
        public DateTime? FechaFirmaCondiciones { get; set; }
        public string RutaAocrGenerada { get; set; }
        public DateTime? FechaAocrGenerada { get; set; }
        public string EstadoDocumentoAocr { get; set; }
        public string EstadoDocumentoCondiciones { get; set; }
    }
}
