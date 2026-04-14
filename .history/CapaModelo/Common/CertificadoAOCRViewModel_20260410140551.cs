using System;

namespace CapaModelo.Common
{
    public class CertificadoAOCRViewModel
    {
        // Identificación del certificado
        public string NumeroAOCR { get; set; }
        public string NumeroAOCBase { get; set; }
        public string PermisoOperacionCNAC { get; set; }
        public int NumeroEnmienda { get; set; }

        // Fechas
        public DateTime FechaEmision { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public DateTime? FechaRenovacion { get; set; }

        // Solicitud completa
        public SolicitudAOCR Solicitud { get; set; }

        // Datos del explotador
        public string NombreExplotador { get; set; }
        public string EstadoExplotador { get; set; }
        public string RazonSocial { get; set; }
        public string RUC { get; set; }
        public string DireccionExplotador { get; set; }
        public string TelefonoExplotador { get; set; }
        public string CorreoExplotador { get; set; }

        // Punto de contacto Ecuador
        public string PuntoContactoEcuador { get; set; }
        public string DireccionContactoEcuador { get; set; }
        public string TelefonoContactoEcuador { get; set; }
        public string CorreoContactoEcuador { get; set; }

        // Puntos de contacto operacionales
        public string DireccionOperacional { get; set; }
        public string TelefonoOperacional { get; set; }
        public string CorreoOperacional { get; set; }

        // Gerencia de Seguridad Operacional
        public string GerenciaSeguridadOperacional { get; set; }
        public string DireccionGSO { get; set; }
        public string TelefonoGSO { get; set; }
        public string CorreoGSO { get; set; }

        // Representante Técnico
        public string RepresentanteTecnico { get; set; }
        public string DireccionRT { get; set; }
        public string TelefonoRT { get; set; }
        public string CorreoRT { get; set; }

        // Representante Legal
        public string RepresentanteLegal { get; set; }

        // Operación
        public string TipoOperacion { get; set; }
        public string AlcanceOperacion { get; set; }
        public string AeronavesDetalle { get; set; }

        // Firmante
        public string NombreFirmante { get; set; }
        public string CargoFirmante { get; set; }
        public string TituloFirmante { get; set; }

        // Texto legal
        public string TextoLegalEs { get; set; }
        public string TextoLegalEn { get; set; }

        // Observaciones
        public string Observaciones { get; set; }

        // Firma digital
        public string RutaFirmaDigital { get; set; }
        public string RutaSelloOficial { get; set; }
        public string HashDocumento { get; set; }

        // Recursos
        public string LogoBase64 { get; set; }
        public string EscudoBase64 { get; set; }

        // Alias compat
        public string NombreOperador { get { return NombreExplotador; } set { NombreExplotador = value; } }
        public string Direccion { get { return DireccionExplotador; } set { DireccionExplotador = value; } }
        public string AprobadoPor { get { return NombreFirmante; } set { NombreFirmante = value; } }
        public string CargoAprobador { get { return CargoFirmante; } set { CargoFirmante = value; } }
    }
}
