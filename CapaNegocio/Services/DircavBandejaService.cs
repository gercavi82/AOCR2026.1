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
    /// Servicio exclusivo para la gestión y conteo de bandejas del rol DIRCAV.
    /// Funciones: Aceptación documental, designación de inspectores, revisión de informes,
    /// firma de Condiciones y Limitaciones, y remisión a DIRDAC.
    /// No comparte datos ni contadores con DIRDAC.
    /// </summary>
    public class DircavBandejaService
    {
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly AocrBandejaDAO _bandejaDao;

        public DircavBandejaService()
        {
            _solicitudDao = new SolicitudAOCRDAO();
            _inspeccionDao = new InspeccionDAO();
            _bandejaDao = new AocrBandejaDAO();
        }

        public DircavBandejaService(SolicitudAOCRDAO solicitudDao, InspeccionDAO inspeccionDao, AocrBandejaDAO bandejaDao)
        {
            _solicitudDao = solicitudDao;
            _inspeccionDao = inspeccionDao;
            _bandejaDao = bandejaDao;
        }

        // 1. Documentación pendiente de aceptación por DIRCAV (AC-04: Remitida obligatoriamente por Coordinador)
        public List<SolicitudAOCR> ObtenerDocumentacionPendienteAceptacion()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                (string.Equals(s.Estado, AocrEstadosProceso.PendienteDircav, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(s.Estado, AocrEstadosProceso.PendienteAceptacionDircav, StringComparison.OrdinalIgnoreCase))
                && !string.Equals(s.Estado, AocrEstadosProceso.PendienteCoordinador, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(s.Estado, AocrEstadosProceso.DevueltoInspector, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        public int ContarDocumentacionPendienteAceptacion()
        {
            return ObtenerDocumentacionPendienteAceptacion().Count;
        }

        // 2. Designaciones pendientes de firma por DIRCAV
        public List<SolicitudAOCR> ObtenerDesignacionesPendientes()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.PendienteDesignacionDircav, StringComparison.OrdinalIgnoreCase)
                || (string.Equals(s.Estado, EstadoSolicitud.RequiereInspeccion, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(s.TecnicoResponsableCedula))
            ).ToList();
        }

        public int ContarDesignacionesPendientes()
        {
            return ObtenerDesignacionesPendientes().Count;
        }

        // 3. Designaciones firmadas por DIRCAV
        public List<SolicitudAOCR> ObtenerDesignacionesFirmadas()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.DesignacionFirmadaDircav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // 4. Informes técnicos pendientes de revisión por DIRCAV
        public List<SolicitudAOCR> ObtenerInformesPendientesRevision()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.PendienteRevisionFinalDircav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, AocrEstadosProceso.PendienteRevisionInformeDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        public int ContarInformesPendientesRevision()
        {
            return ObtenerInformesPendientesRevision().Count;
        }

        // 5. Condiciones y Limitaciones pendientes de firma exclusiva por DIRCAV
        public List<AocrBandejaDocumentoRow> ObtenerCondicionesPendientesFirma()
        {
            var filas = _bandejaDao.ListarGeneradasFirmadas() ?? new List<AocrBandejaDocumentoRow>();
            return filas.Where(f => AocrFirmaPendientePolicy.EsCondicionesPendienteFirma(f)).ToList();
        }

        public int ContarCondicionesPendientesFirma()
        {
            return ObtenerCondicionesPendientesFirma().Count;
        }

        // 6. Expedientes pendientes de remisión a DIRDAC
        public List<SolicitudAOCR> ObtenerExpedientesPendientesRemisionDirdac()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.ClFirmadaDircav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, AocrEstadosProceso.CondicionesFirmadasDcav, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        public int ContarExpedientesPendientesRemisionDirdac()
        {
            return ObtenerExpedientesPendientesRemisionDirdac().Count;
        }

        // 7. Expedientes devueltos (por DIRDAC a DIRCAV o en subsanación)
        public List<SolicitudAOCR> ObtenerExpedientesDevueltos()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.Where(s =>
                string.Equals(s.Estado, AocrEstadosProceso.DevueltoDircavPorDirdac, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, AocrEstadosProceso.DevueltoCoordinadorFinalDircav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Estado, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // 8. Historial de trámites gestionados por DIRCAV
        public List<SolicitudAOCR> ObtenerHistorialGestionados()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            return solicitudes.OrderByDescending(s => s.CodigoSolicitud).ToList();
        }
    }
}
