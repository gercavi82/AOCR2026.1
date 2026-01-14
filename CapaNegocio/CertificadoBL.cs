using System;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio
{
    public class CertificadoBL
    {
        private readonly CertificadoDAO _dao;

        public CertificadoBL()
        {
            _dao = new CertificadoDAO();
        }

        public Certificado ObtenerPorSolicitud(int solicitudId)
        {
            if (solicitudId <= 0)
                throw new ArgumentException("ID de solicitud inválido.");

            return _dao.ObtenerPorSolicitud(solicitudId);
        }

        public Certificado Obtener(int id)
        {
            if (id <= 0)
                throw new ArgumentException("ID del certificado inválido.");

            return _dao.ObtenerPorId(id);
        }

        public int GenerarCertificado(int solicitudId, string usuarioFirmante, int vigenciaAnios = 2, string condiciones = null)
        {
            if (solicitudId <= 0)
                throw new ArgumentException("El ID de la solicitud es obligatorio.");

            if (string.IsNullOrWhiteSpace(usuarioFirmante))
                throw new ArgumentException("El certificado debe ser firmado digitalmente por un usuario.");

            var ahora = DateTime.Now;

            var cert = new Certificado
            {
                CodigoSolicitud = solicitudId,
                NumeroCertificado = $"AOCR-{ahora.Year}-{solicitudId:D5}",
                FechaEmision = ahora,
                VigenciaAnios = vigenciaAnios,
                FechaVencimiento = ahora.AddYears(vigenciaAnios),
                Estado = "VIGENTE",
                CondicionesEspeciales = condiciones,
                FirmadoPor = usuarioFirmante,
                CodigoVerificacion = GenerarCodigoVerificacion(),
                RutaPdf = null
            };

            return _dao.Crear(cert);
        }

        public bool SubirPDF(int certificadoId, string rutaPDF)
        {
            if (certificadoId <= 0 || string.IsNullOrWhiteSpace(rutaPDF))
                return false;

            var cert = _dao.ObtenerPorId(certificadoId);
            if (cert == null)
                return false;

            cert.RutaPdf = rutaPDF;
            return _dao.Actualizar(cert);
        }

        // Utilidad: código de verificación estilo GUID corto
        private string GenerarCodigoVerificacion()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        }
    }
}
