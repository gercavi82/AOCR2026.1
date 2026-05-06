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
            return _dao.ObtenerPorSolicitud(solicitudId);
        }

        public Certificado Obtener(int id)
        {
            return _dao.ObtenerPorId(id);
        }

        public Certificado ObtenerPorNumero(string numeroCertificado)
        {
            return _dao.ObtenerPorNumero(numeroCertificado);
        }

        public int GenerarCertificado(int solicitudId, string usuario)
        {
            var cert = new Certificado
            {
                CodigoSolicitud = solicitudId,
                NumeroCertificado = "AOCR-" + DateTime.Now.Ticks,
                FechaEmision = DateTime.Now,
                FechaVencimiento = DateTime.Now.AddYears(1),
                Estado = "GENERADO",
                EmitidoPor = usuario,
                RutaDocumento = null
            };

            return _dao.Crear(cert); // ✅ ahora Crear devuelve int
        }
    }
}
