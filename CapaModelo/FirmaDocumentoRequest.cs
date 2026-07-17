using System;
using System.Web;

namespace CapaModelo
{
    public sealed class FirmaDocumentoRequest
    {
        public int SolicitudId { get; set; }
        public string TipoDocumento { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public string RolSolicitado { get; set; }
        public HttpPostedFileBase CertificadoDigital { get; set; }
        public string PasswordCertificado { get; set; }
    }
}
