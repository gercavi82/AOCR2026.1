using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using CapaModelo;
using Rotativa;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio para generación de certificados AOCR en formato PDF
    /// Utiliza Rotativa para convertir vistas Razor HTML a PDF
    /// </summary>
    public class AOCRPdfService
    {
        private const string CARPETA_CERTIFICADOS = "~/Uploads/Certificados";
        
        /// <summary>
        /// Genera el número de certificado AOCR según formato estándar
        /// Formato: AOCR-YYYY-####
        /// </summary>
        public static string GenerarNumeroAOCR(int idSolicitud, DateTime? fecha = null)
        {
            fecha = fecha ?? DateTime.Now;
            return $"AOCR-{fecha.Value.Year}-{idSolicitud:D4}";
        }
        
        /// <summary>
        /// Genera ruta de archivo para el certificado PDF
        /// </summary>
        public static string GenerarRutaCertificado(string numeroAOCR)
        {
            string carpetaFisica = HttpContext.Current.Server.MapPath(CARPETA_CERTIFICADOS);
            
            // Crear carpeta si no existe
            if (!Directory.Exists(carpetaFisica))
            {
                Directory.CreateDirectory(carpetaFisica);
            }
            
            // Generar nombre de archivo
            string nombreArchivo = $"{numeroAOCR.Replace("/", "-")}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            return Path.Combine(carpetaFisica, nombreArchivo);
        }
        
        /// <summary>
        /// Genera PDF del certificado AOCR desde una vista Razor
        /// </summary>
        /// <param name="controller">Controlador que invoca la generación</param>
        /// <param name="solicitud">Datos de la solicitud AOCR</param>
        /// <param name="numeroAOCR">Número de certificado asignado</param>
        /// <returns>ActionResult con PDF o ruta del archivo guardado</returns>
        public static ActionResult GenerarPDFCertificado(Controller controller, SolicitudAOCR solicitud, string numeroAOCR, bool guardarArchivo = true)
        {
            try
            {
                // Preparar modelo con datos para la vista
                var modelo = new CertificadoAOCRViewModel
                {
                    NumeroAOCR = numeroAOCR,
                    FechaEmision = DateTime.Now,
                    Solicitud = solicitud,
                    NombreOperador = solicitud.NombreEmpresa,
                    RUC = solicitud.RUC,
                    RepresentanteLegal = solicitud.NombreRepresentante,
                    TipoOperacion = solicitud.TipoOperacion,
                    // Agregar más datos según sea necesari
o
                };
                
                // Generar PDF usando Rotativa
                var pdf = new ViewAsPdf("CertificadoAOCR", modelo)
                {
                    FileName = $"{numeroAOCR}.pdf",
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    PageSize = Rotativa.Options.Size.A4,
                    CustomSwitches = "--page-offset 0 --footer-center [page] --footer-font-size 10"
                };
                
                if (guardarArchivo)
                {
                    // Guardar PDF en servidor
                    string rutaArchivo = GenerarRutaCertificado(numeroAOCR);
                    byte[] pdfBytes = pdf.BuildPdf(controller.ControllerContext);
                    File.WriteAllBytes(rutaArchivo, pdfBytes);
                    
                    LogBL.RegistrarInfo($"Certificado AOCR {numeroAOCR} generado en {rutaArchivo}", "AOCRPdfService");
                }
                
                return pdf;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError($"Error al generar PDF certificado {numeroAOCR}", ex.ToString(), "AOCRPdfService");
                throw;
            }
        }
        
        /// <summary>
        /// Genera PDF y lo guarda en el servidor, retornando la ruta
        /// </summary>
        public static string GenerarYGuardarCertificado(Controller controller, SolicitudAOCR solicitud, string numeroAOCR, out string mensaje)
        {
            try
            {
                var modelo = new CertificadoAOCRViewModel
                {
                    NumeroAOCR = numeroAOCR,
                    FechaEmision = DateTime.Now,
                    Solicitud = solicitud,
                    NombreOperador = solicitud.NombreEmpresa,
                    RUC = solicitud.RUC,
                    RepresentanteLegal = solicitud.NombreRepresentante,
                    TipoOperacion = solicitud.TipoOperacion
                };
                
                var pdf = new ViewAsPdf("CertificadoAOCR", modelo)
                {
                    FileName = $"{numeroAOCR}.pdf",
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    PageSize = Rotativa.Options.Size.A4,
                    CustomSwitches = "--page-offset 0 --footer-center [page] --footer-font-size 10"
                };
                
                // Generar PDF
                string rutaArchivo = GenerarRutaCertificado(numeroAOCR);
                byte[] pdfBytes = pdf.BuildPdf(controller.ControllerContext);
                File.WriteAllBytes(rutaArchivo, pdfBytes);
                
                mensaje = "Certificado generado exitosamente.";
                LogBL.RegistrarInfo($"Certificado AOCR {numeroAOCR} guardado en {rutaArchivo}", "AOCRPdfService");
                
                return rutaArchivo;
            }
            catch (Exception ex)
            {
                mensaje = "Error al generar certificado: " + ex.Message;
                LogBL.RegistrarError($"Error al generar y guardar certificado {numeroAOCR}", ex.ToString(), "AOCRPdfService");
                return null;
            }
        }
        
        /// <summary>
        /// Verifica si existe el archivo PDF del certificado
        /// </summary>
        public static bool ExisteCertificado(string rutaArchivo)
        {
            return !string.IsNullOrEmpty(rutaArchivo) && File.Exists(rutaArchivo);
        }
        
        /// <summary>
        /// Obtiene el archivo PDF como FileResult para descargar
        /// </summary>
        public static FileResult ObtenerCertificadoParaDescarga(string rutaArchivo, string numeroAOCR)
        {
            if (!ExisteCertificado(rutaArchivo))
            {
                throw new FileNotFoundException($"Certificado {numeroAOCR} no encontrado.");
            }
            
            byte[] fileBytes = File.ReadAllBytes(rutaArchivo);
            string contentType = "application/pdf";
            string fileName = $"Certificado_{numeroAOCR}.pdf";
            
            return new FileContentResult(fileBytes, contentType)
            {
                FileDownloadName = fileName
            };
        }
    }
    
    // ==========================================
    // VIEW MODEL PARA CERTIFICADO AOCR
    // ==========================================

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
