using System;
using System.Linq;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class AocrContextoResolucion
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public int? SolicitudId { get; set; }
        public int? AocrId { get; set; }
        public int? InformeTecnicoId { get; set; }
        public int? InspeccionId { get; set; }
        public string CodigoSolicitud { get; set; }
        public string EstadoSolicitud { get; set; }
        public string EstadoAocr { get; set; }
        public string Operadora { get; set; }
        public bool ExisteSolicitud { get; set; }
        public bool ExisteAocr { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public Certificado Aocr { get; set; }
        public Inspeccion Inspeccion { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }

        public static AocrContextoResolucion Error(string mensaje)
        {
            return new AocrContextoResolucion
            {
                Ok = false,
                Mensaje = string.IsNullOrWhiteSpace(mensaje)
                    ? "No se pudo resolver el contexto AOCR."
                    : mensaje
            };
        }
    }

    public sealed class AocrContextResolverService
    {
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly CertificadoDAO _certificadoDao;
        private readonly DocumentoDAO _documentoDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly InspeccionInformeDAO _informeDao;

        public AocrContextResolverService()
            : this(new SolicitudAOCRDAO(), new CertificadoDAO(), new DocumentoDAO(), new InspeccionDAO(), new InspeccionInformeDAO())
        {
        }

        public AocrContextResolverService(
            SolicitudAOCRDAO solicitudDao,
            CertificadoDAO certificadoDao,
            DocumentoDAO documentoDao,
            InspeccionDAO inspeccionDao,
            InspeccionInformeDAO informeDao)
        {
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _certificadoDao = certificadoDao ?? new CertificadoDAO();
            _documentoDao = documentoDao ?? new DocumentoDAO();
            _inspeccionDao = inspeccionDao ?? new InspeccionDAO();
            _informeDao = informeDao ?? new InspeccionInformeDAO();
        }

        public AocrContextoResolucion ResolverContextoAocr(int? solicitudId, int? aocrId)
        {
            if (aocrId.HasValue && aocrId.Value > 0)
            {
                return ResolverDesdeAocrId(aocrId.Value);
            }

            if (solicitudId.HasValue && solicitudId.Value > 0)
            {
                return ResolverDesdeSolicitudId(solicitudId.Value);
            }

            return AocrContextoResolucion.Error("No se recibio el identificador de la solicitud o del AOCR.");
        }

        public AocrContextoResolucion ResolverDesdeSolicitudId(int solicitudId)
        {
            if (solicitudId <= 0)
            {
                return AocrContextoResolucion.Error("No se recibio un identificador de solicitud valido.");
            }

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null || solicitud.CodigoSolicitud <= 0)
            {
                return AocrContextoResolucion.Error("No se encontro la solicitud AOCR.");
            }

            var aocr = Safe(() => _certificadoDao.ObtenerPorSolicitud(solicitudId));
            var inspeccion = Safe(() => (_inspeccionDao.ListarPorSolicitud(solicitudId) ?? new System.Collections.Generic.List<Inspeccion>())
                .OrderByDescending(i => i.CodigoInspeccion)
                .FirstOrDefault());
            InspeccionInformeTecnico informe = null;
            if (inspeccion != null && inspeccion.CodigoInspeccion > 0)
            {
                informe = Safe(() => _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion));
            }

            return ConstruirContexto(solicitud, aocr, inspeccion, informe);
        }

        public AocrContextoResolucion ResolverDesdeAocrId(int aocrId)
        {
            if (aocrId <= 0)
            {
                return AocrContextoResolucion.Error("No se recibio un identificador AOCR valido.");
            }

            var aocr = Safe(() => _certificadoDao.ObtenerPorId(aocrId));
            if (aocr == null || aocr.CodigoSolicitud <= 0)
            {
                return AocrContextoResolucion.Error("No se encontro el AOCR asociado a la solicitud.");
            }

            var contexto = ResolverDesdeSolicitudId(aocr.CodigoSolicitud);
            contexto.Aocr = aocr;
            contexto.AocrId = aocr.CodigoCertificado;
            contexto.EstadoAocr = aocr.Estado;
            contexto.ExisteAocr = true;
            return contexto;
        }

        public AocrContextoResolucion ResolverDesdeDocumentoId(int documentoId)
        {
            if (documentoId <= 0)
            {
                return AocrContextoResolucion.Error("No se recibio un identificador de documento valido.");
            }

            var documento = Safe(() => _documentoDao.ObtenerPorId(documentoId));
            if (documento == null || documento.CodigoSolicitud <= 0)
            {
                return AocrContextoResolucion.Error("No se encontro el documento solicitado.");
            }

            return ResolverDesdeSolicitudId(documento.CodigoSolicitud);
        }

        public AocrContextoResolucion ResolverDesdeInformeTecnicoId(int informeTecnicoId)
        {
            if (informeTecnicoId <= 0)
            {
                return AocrContextoResolucion.Error("No se recibio un identificador de informe tecnico valido.");
            }

            var informe = Safe(() => _informeDao.ObtenerPorId(informeTecnicoId));
            if (informe == null || informe.CodigoInspeccion <= 0)
            {
                return AocrContextoResolucion.Error("No se encontro el informe tecnico solicitado.");
            }

            var inspeccion = Safe(() => _inspeccionDao.ObtenerPorId(informe.CodigoInspeccion));
            if (inspeccion == null || inspeccion.CodigoSolicitud <= 0)
            {
                return AocrContextoResolucion.Error("No se encontro la inspeccion asociada al informe tecnico.");
            }

            var contexto = ResolverDesdeSolicitudId(inspeccion.CodigoSolicitud);
            contexto.InformeTecnico = informe;
            contexto.InformeTecnicoId = informe.CodigoInforme;
            contexto.Inspeccion = inspeccion;
            contexto.InspeccionId = inspeccion.CodigoInspeccion;
            return contexto;
        }

        private static AocrContextoResolucion ConstruirContexto(SolicitudAOCR solicitud, Certificado aocr, Inspeccion inspeccion, InspeccionInformeTecnico informe)
        {
            return new AocrContextoResolucion
            {
                Ok = true,
                Mensaje = "Contexto AOCR resuelto.",
                SolicitudId = solicitud.CodigoSolicitud,
                AocrId = aocr != null && aocr.CodigoCertificado > 0 ? (int?)aocr.CodigoCertificado : null,
                InformeTecnicoId = informe != null && informe.CodigoInforme > 0 ? (int?)informe.CodigoInforme : null,
                InspeccionId = inspeccion != null && inspeccion.CodigoInspeccion > 0 ? (int?)inspeccion.CodigoInspeccion : null,
                CodigoSolicitud = string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud) ? solicitud.CodigoSolicitud.ToString() : solicitud.NumeroSolicitud,
                EstadoSolicitud = solicitud.Estado,
                EstadoAocr = aocr != null ? aocr.Estado : null,
                Operadora = !string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? solicitud.RazonSocial : solicitud.NombreOperador,
                ExisteSolicitud = true,
                ExisteAocr = aocr != null && aocr.CodigoCertificado > 0,
                Solicitud = solicitud,
                Aocr = aocr,
                Inspeccion = inspeccion,
                InformeTecnico = informe
            };
        }

        private static T Safe<T>(Func<T> action) where T : class
        {
            try
            {
                return action != null ? action() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
