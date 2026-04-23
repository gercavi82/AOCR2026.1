using System;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class CertificacionService
    {
        private readonly CertificadoDAO _certificadoDAO;

        public CertificacionService()
        {
            _certificadoDAO = new CertificadoDAO();
        }

        // Ojo: tu método recibe "id" pero llama ObtenerPorSolicitud.
        // Lo dejo compatible: si "id" es solicitud, está bien.
        public Certificado ObtenerCertificado(int codigoSolicitud)
        {
            return _certificadoDAO.ObtenerPorSolicitud(codigoSolicitud);
        }

        // ✅ Crear devuelve el Certificado con el ID asignado
        public Certificado CrearCertificado(Certificado cert)
        {
            if (cert == null) throw new ArgumentNullException(nameof(cert));
            if (cert.CodigoSolicitud <= 0) throw new ArgumentException("Código de solicitud inválido.");

            // Defaults seguros
            if (string.IsNullOrWhiteSpace(cert.Estado))
                cert.Estado = "Vigente";

            if (!cert.FechaEmision.HasValue)
                cert.FechaEmision = DateTime.Now;

            // Si no viene vencimiento, por defecto 1 año
            if (!cert.FechaVencimiento.HasValue)
                cert.FechaVencimiento = cert.FechaEmision.Value.AddYears(1);

            // Auditoría (si no te pasan)
            if (!cert.CreatedAt.HasValue) cert.CreatedAt = DateTime.Now;
            if (!cert.UpdatedAt.HasValue) cert.UpdatedAt = DateTime.Now;

            // ✅ Crear ahora retorna ID (int)
            int id = _certificadoDAO.Crear(cert);

            if (id > 0)
            {
                cert.CodigoCertificado = id; // importantísimo
                return cert;
            }

            return null;
        }

        public bool ActualizarCertificado(Certificado cert)
        {
            if (cert == null) throw new ArgumentNullException(nameof(cert));
            if (cert.CodigoCertificado <= 0) throw new ArgumentException("Código de certificado inválido.");

            cert.UpdatedAt = DateTime.Now;
            return _certificadoDAO.Actualizar(cert);
        }
    }
}
