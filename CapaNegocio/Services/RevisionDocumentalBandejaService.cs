using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class RevisionDocumentalBandejaItem
    {
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public bool MostrarAccionInspeccion { get; set; }
        public bool ExcluirPorDocumentacionCerrada { get; set; }
    }

    public sealed class RevisionDocumentalBandejaService
    {
        private readonly RevisionDocumentalDAO _revisionDocumentalDao = new RevisionDocumentalDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBl = new SolicitudAocrInfraBL();

        public IList<RevisionDocumentalBandejaItem> ObtenerItemsBandejaInspector(
            IEnumerable<int> inspectorIds,
            IEnumerable<string> identificadoresInspector,
            bool incluirTodasSiSinFiltro = false)
        {
            var ids = (inspectorIds ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            var identificadores = (identificadoresInspector ?? Enumerable.Empty<string>())
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Select(valor => valor.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var codigosSolicitud = new HashSet<int>(
                _revisionDocumentalDao.ObtenerPendientesRevisionInspector(ids, identificadores, incluirTodasSiSinFiltro)
                ?? new List<int>());

            if (!incluirTodasSiSinFiltro)
            {
                foreach (var inspectorId in ids)
                {
                    foreach (var inspeccion in _inspeccionDao.ListarPorInspector(inspectorId) ?? new List<Inspeccion>())
                    {
                        if (inspeccion != null && inspeccion.CodigoSolicitud > 0)
                        {
                            codigosSolicitud.Add(inspeccion.CodigoSolicitud);
                        }
                    }
                }
            }

            var items = new List<RevisionDocumentalBandejaItem>();
            foreach (var codigoSolicitud in codigosSolicitud.OrderByDescending(id => id))
            {
                var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
                if (solicitud == null)
                {
                    continue;
                }

                var inspecciones = _solicitudAocrInfraBl.ListarInspeccionesPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
                var estadoRevision = _solicitudAocrInfraBl.ObtenerEstadoRevisionDocumental(codigoSolicitud);
                if (!PuedeAccederRevisionDocumental(solicitud, estadoRevision, inspecciones, ids, identificadores))
                {
                    continue;
                }

                var inspeccionActiva = ResolverInspeccionActiva(inspecciones, ids, identificadores);
                var mostrarAccionInspeccion = DebeMostrarAccionInspeccion(solicitud, inspeccionActiva);
                var excluir = inspeccionActiva != null
                    && estadoRevision != null
                    && estadoRevision.DocumentacionAprobada
                    && RevisionDocumentalService.InspectorConfirmoCierreDocumental(inspeccionActiva);

                items.Add(new RevisionDocumentalBandejaItem
                {
                    CodigoSolicitud = codigoSolicitud,
                    CodigoInspeccion = inspeccionActiva != null ? inspeccionActiva.CodigoInspeccion : (int?)null,
                    MostrarAccionInspeccion = mostrarAccionInspeccion,
                    ExcluirPorDocumentacionCerrada = excluir
                });
            }

            return items.Where(item => item != null && !item.ExcluirPorDocumentacionCerrada).ToList();
        }

        public int ContarBandejaInspector(AocrBandejaRoleContext context)
        {
            if (context == null)
            {
                return 0;
            }

            var ids = ResolverInspectorFilterIds(context);
            var identificadores = ResolverInspectorFilterTextIds(context, ids);
            return ObtenerItemsBandejaInspector(ids, identificadores).Count;
        }

        public static bool PuedeAccederRevisionDocumental(
            SolicitudAOCR solicitud,
            EstadoRevisionDocumental estadoRevision,
            IEnumerable<Inspeccion> inspecciones,
            IEnumerable<int> inspectorIds,
            IEnumerable<string> identificadoresInspector)
        {
            if (solicitud == null)
            {
                return false;
            }

            if (SolicitudAocrInfraBL.EsRevisionDocumentalPreAsignacion(solicitud, inspecciones))
            {
                return true;
            }

            var ids = new HashSet<int>((inspectorIds ?? Enumerable.Empty<int>()).Where(id => id > 0));
            var identificadores = new HashSet<string>(
                (identificadoresInspector ?? Enumerable.Empty<string>())
                    .Where(valor => !string.IsNullOrWhiteSpace(valor))
                    .Select(valor => valor.Trim().ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase);

            if (InspectorAsignadoCoincide(solicitud, inspecciones, ids, identificadores))
            {
                if (estadoRevision == null)
                {
                    return true;
                }

                if (estadoRevision.VisibleEnBandejaInspector)
                {
                    return true;
                }

                return EsSolicitudEnFaseOperativaInspector(solicitud.Estado);
            }

            return false;
        }

        public static bool DebeMostrarAccionInspeccion(
            SolicitudAOCR solicitud,
            Inspeccion inspeccionActiva)
        {
            if (solicitud == null || inspeccionActiva == null || inspeccionActiva.CodigoInspeccion <= 0)
            {
                return false;
            }

            return EsSolicitudEnFaseOperativaInspector(solicitud.Estado)
                && RevisionDocumentalService.InspectorConfirmoCierreDocumental(inspeccionActiva);
        }

        private static Inspeccion ResolverInspeccionActiva(
            IEnumerable<Inspeccion> inspecciones,
            IEnumerable<int> inspectorIds,
            IEnumerable<string> identificadoresInspector)
        {
            return (inspecciones ?? Enumerable.Empty<Inspeccion>())
                .Where(ins => ins != null && ins.CodigoInspeccion > 0)
                .Where(ins => InspectorInspeccionCoincide(ins, inspectorIds, identificadoresInspector))
                .OrderByDescending(ins => ins.UpdatedAt ?? DateTime.MinValue)
                .ThenByDescending(ins => ins.CodigoInspeccion)
                .FirstOrDefault();
        }

        private static bool InspectorAsignadoCoincide(
            SolicitudAOCR solicitud,
            IEnumerable<Inspeccion> inspecciones,
            HashSet<int> inspectorIds,
            HashSet<string> identificadores)
        {
            if (solicitud.CodigoTecnico.HasValue && inspectorIds.Contains(solicitud.CodigoTecnico.Value))
            {
                return true;
            }

            if (CoincideIdentificadorInspector(solicitud.TecnicoResponsableCedula, identificadores)
                || CoincideIdentificadorInspector(solicitud.InspectorApoyoCedula, identificadores))
            {
                return true;
            }

            return (inspecciones ?? Enumerable.Empty<Inspeccion>())
                .Any(ins => InspectorInspeccionCoincide(ins, inspectorIds, identificadores));
        }

        private static bool InspectorInspeccionCoincide(
            Inspeccion inspeccion,
            IEnumerable<int> inspectorIds,
            IEnumerable<string> identificadoresInspector)
        {
            if (inspeccion == null)
            {
                return false;
            }

            var ids = new HashSet<int>((inspectorIds ?? Enumerable.Empty<int>()).Where(id => id > 0));
            var identificadores = new HashSet<string>(
                (identificadoresInspector ?? Enumerable.Empty<string>())
                    .Where(valor => !string.IsNullOrWhiteSpace(valor))
                    .Select(valor => valor.Trim().ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase);

            if (inspeccion.CodigoInspector.HasValue && ids.Contains(inspeccion.CodigoInspector.Value))
            {
                return true;
            }

            return CoincideIdentificadorInspector(inspeccion.InspectorPrincipalCedula, identificadores)
                || CoincideIdentificadorInspector(inspeccion.InspectorApoyoCedula, identificadores);
        }

        private static bool EsSolicitudEnFaseOperativaInspector(string estadoSolicitud)
        {
            var canonico = EstadoSolicitud.Normalizar(estadoSolicitud);
            if (string.Equals(canonico, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.DocumentacionPendiente, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var claveRaw = InspectorIdentityService.NormalizarCodigoInspector(estadoSolicitud);
            return string.Equals(claveRaw, "ENREVISIONINSPECTOR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(claveRaw, "ENREVISIONDOCUMENTAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(claveRaw, "SUBSANADART", StringComparison.OrdinalIgnoreCase)
                || string.Equals(claveRaw, "DOCUMENTACIONSUBSANADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(claveRaw, "PENDIENTEREVISIONINSPECTOR", StringComparison.OrdinalIgnoreCase);
        }

        private static bool CoincideIdentificadorInspector(string valor, HashSet<string> identificadores)
        {
            return !string.IsNullOrWhiteSpace(valor)
                && identificadores != null
                && identificadores.Contains(valor.Trim().ToUpperInvariant());
        }

        private static HashSet<int> ResolverInspectorFilterIds(AocrBandejaRoleContext context)
        {
            var ids = new HashSet<int>();
            if (context != null && context.UserId > 0)
            {
                ids.Add(context.UserId);
            }

            int codigoNumerico;
            if (context != null
                && int.TryParse(context.CodigoUsuario, out codigoNumerico)
                && codigoNumerico > 0)
            {
                ids.Add(codigoNumerico);
            }

            try
            {
                UsuarioInternoRTRegistro inspectorActual = null;
                if (context != null && context.UserId > 0)
                {
                    inspectorActual = new UsuarioInternoRTDAO().ObtenerInspectorActivoPorTecnicoIdOUsuarioId(context.UserId);
                }

                if (inspectorActual == null && context != null && !string.IsNullOrWhiteSpace(context.CodigoUsuario))
                {
                    inspectorActual = new UsuarioInternoRTDAO().ObtenerActivoPorCodigoUsuario(context.CodigoUsuario)
                        ?? new UsuarioInternoRTDAO().ObtenerInspectorAsignableActivo(context.CodigoUsuario);
                }

                if (inspectorActual != null)
                {
                    if (inspectorActual.UsuarioId.HasValue && inspectorActual.UsuarioId.Value > 0)
                    {
                        ids.Add(inspectorActual.UsuarioId.Value);
                    }

                    if (inspectorActual.TecnicoId.HasValue && inspectorActual.TecnicoId.Value > 0)
                    {
                        ids.Add(inspectorActual.TecnicoId.Value);
                    }
                }
            }
            catch
            {
            }

            return ids;
        }

        private static HashSet<string> ResolverInspectorFilterTextIds(AocrBandejaRoleContext context, IEnumerable<int> inspectorIds)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (context != null)
            {
                AddTextId(ids, context.CodigoUsuario);
                AddTextId(ids, context.UserName);
            }

            foreach (var id in inspectorIds ?? Enumerable.Empty<int>())
            {
                if (id > 0)
                {
                    AddTextId(ids, id.ToString());
                }
            }

            return ids;
        }

        private static void AddTextId(HashSet<string> ids, string value)
        {
            if (ids == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            ids.Add(value.Trim().ToUpperInvariant());
        }
    }
}
