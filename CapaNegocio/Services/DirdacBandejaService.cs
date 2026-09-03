using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio exclusivo para la gestión y conteo de bandejas del rol DIRDAC.
    /// Funciones: Revisión del AOCR, confirmación de firmas DIRCAV previas, firma y legalización
    /// del AOCR, devolución a DIRCAV con observaciones y consulta de trámites concluidos.
    /// No comparte datos ni contadores con DIRCAV.
    /// </summary>
    public class DirdacBandejaService
    {
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly AocrBandejaDAO _bandejaDao;

        public DirdacBandejaService()
        {
            _solicitudDao = new SolicitudAOCRDAO();
            _bandejaDao = new AocrBandejaDAO();
        }

        public DirdacBandejaService(SolicitudAOCRDAO solicitudDao, AocrBandejaDAO bandejaDao)
        {
            _solicitudDao = solicitudDao;
            _bandejaDao = bandejaDao;
        }

        // 1. AOCR pendientes de revisión por DIRDAC
        public List<SolicitudAOCR> ObtenerAocrPendientesRevision()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.AocrPendienteDirdac, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        public int ContarAocrPendientesRevision()
        {
            return ObtenerAocrPendientesRevision().Count;
        }

        // 2. AOCR pendientes de firma exclusiva por DIRDAC
        public List<AocrBandejaDocumentoRow> ObtenerAocrPendientesFirma()
        {
            var filas = _bandejaDao.ListarGeneradasFirmadas() ?? new List<AocrBandejaDocumentoRow>();
            return filas.Where(f => AocrFirmaPendientePolicy.EsAocrPendienteFirma(f)).ToList();
        }

        public int ContarAocrPendientesFirma()
        {
            return ObtenerAocrPendientesFirma().Count;
        }

        // 3. Expedientes devueltos a DIRCAV
        public List<SolicitudAOCR> ObtenerExpedientesDevueltosDircav()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.DevueltoDircavPorDirdac, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        public int ContarExpedientesDevueltosDircav()
        {
            return ObtenerExpedientesDevueltosDircav().Count;
        }

        // 4. AOCR firmados por DIRDAC
        public List<AocrBandejaDocumentoRow> ObtenerAocrFirmados()
        {
            var filas = _bandejaDao.ListarGeneradasFirmadas() ?? new List<AocrBandejaDocumentoRow>();
            return filas.Where(f => f.FirmaReconocimientoId.GetValueOrDefault() > 0
                || !string.IsNullOrWhiteSpace(f.RutaReconocimientoFirmado)).ToList();
        }

        // 5. Procesos concluidos institucionalmente
        public List<SolicitudAOCR> ObtenerProcesosConcluidos()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.Finalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, AocrEstadosProceso.Cerrado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // 6. Historial de trámites gestionados por DIRDAC
        public List<SolicitudAOCR> ObtenerHistorialGestionados()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.OrderByDescending(s => s.CodigoSolicitud).ToList();
        }
    }
}
