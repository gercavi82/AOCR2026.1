using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;
using CapaNegocio.Services;

namespace CapaPresentacion.Services
{
    public sealed class FirmaAocrInspectorQueueItem
    {
        public AocrProcesoEstadoRecord Estado { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public Inspeccion Inspeccion { get; set; }
    }

    public sealed class FirmaAocrInspectorQueueResult
    {
        public IList<FirmaAocrInspectorQueueItem> Editables { get; set; } = new List<FirmaAocrInspectorQueueItem>();
        public IList<FirmaAocrInspectorQueueItem> Observados { get; set; } = new List<FirmaAocrInspectorQueueItem>();
        public IList<FirmaAocrInspectorQueueItem> Enviados { get; set; } = new List<FirmaAocrInspectorQueueItem>();
        public int AocrEditables { get { return Editables.Count; } }
        public int CondicionesEditables { get { return Editables.Count; } }
        public int TotalPendientes { get { return Editables.Count + Observados.Count; } }
    }

    public sealed class FirmaAocrInspectorQueueService
    {
        private readonly AocrProcesoEstadoDAO _estadoDao = new AocrProcesoEstadoDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly AocrAuthorizationService _authorization = new AocrAuthorizationService();

        public FirmaAocrInspectorQueueResult Obtener(int usuarioId, bool administrador)
        {
            var sw = Stopwatch.StartNew();
            var estados = _estadoDao.ListarActivosPorEstado(
                AocrEstadosProceso.DocumentosHabilitadosInspector,
                AocrEstadosProceso.DocumentosEnRevisionInspector,
                AocrEstadosProceso.DocumentosObservadosDcav,
                AocrEstadosProceso.PendienteRevisionDocumentosDcav) ?? new List<AocrProcesoEstadoRecord>();

            var result = new FirmaAocrInspectorQueueResult();
            foreach (var estado in estados.Where(e => e != null && e.SolicitudId > 0))
            {
                var solicitud = _solicitudDao.ObtenerPorId(estado.SolicitudId);
                var inspeccion = ResolverInspeccion(estado);
                if (solicitud == null || inspeccion == null)
                {
                    continue;
                }

                if (!administrador && !_authorization.PuedeInspectorAbrirInspeccion(inspeccion.CodigoInspeccion, usuarioId))
                {
                    continue;
                }

                var item = new FirmaAocrInspectorQueueItem { Estado = estado, Solicitud = solicitud, Inspeccion = inspeccion };
                if (string.Equals(estado.EstadoActual, AocrEstadosProceso.DocumentosObservadosDcav, StringComparison.OrdinalIgnoreCase))
                {
                    result.Observados.Add(item);
                }
                else if (string.Equals(estado.EstadoActual, AocrEstadosProceso.PendienteRevisionDocumentosDcav, StringComparison.OrdinalIgnoreCase))
                {
                    result.Enviados.Add(item);
                }
                else
                {
                    result.Editables.Add(item);
                }
            }

            sw.Stop();
            Trace.TraceInformation("[INSPECTOR][BANDEJA_DOCUMENTOS] Usuario=" + usuarioId
                + "; Editables=" + result.Editables.Count
                + "; Observados=" + result.Observados.Count
                + "; Enviados=" + result.Enviados.Count
                + "; DuracionMs=" + sw.ElapsedMilliseconds + ";");
            return result;
        }

        private Inspeccion ResolverInspeccion(AocrProcesoEstadoRecord estado)
        {
            if (estado.InspeccionId.HasValue && estado.InspeccionId.Value > 0)
            {
                var directa = _inspeccionDao.ObtenerPorId(estado.InspeccionId.Value);
                if (directa != null)
                {
                    return directa;
                }
            }

            return (_inspeccionDao.ListarPorSolicitud(estado.SolicitudId) ?? new List<Inspeccion>())
                .OrderByDescending(i => i.CodigoInspeccion)
                .FirstOrDefault();
        }
    }
}
