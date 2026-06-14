using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class InspectorBandejaService
    {
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly RevisionDocumentalBandejaService _revisionDocumentalBandejaService = new RevisionDocumentalBandejaService();

        public int ContarRevisionDocumentalPendiente(AocrBandejaRoleContext context)
        {
            return _revisionDocumentalBandejaService.ContarBandejaInspector(context);
        }

        public IList<Inspeccion> ObtenerInspeccionesAsignadas(AocrBandejaRoleContext context)
        {
            var inspectorIds = ResolverInspectorFilterIds(context);
            var inspectorTextIds = ResolverInspectorFilterTextIds(context, inspectorIds);
            var inspeccionesBase = _inspeccionDao.ListarTodas() ?? new List<Inspeccion>();

            return inspectorIds
                .SelectMany(id => _inspeccionDao.ListarPorInspector(id) ?? new List<Inspeccion>())
                .Concat(inspeccionesBase.Where(ins => InspectorMatchesTextIdentifiers(ins, inspectorTextIds)))
                .Where(ins => ins != null && ins.CodigoInspeccion > 0)
                .GroupBy(ins => ins.CodigoInspeccion)
                .Select(group => group.OrderByDescending(ins => ins.UpdatedAt ?? DateTime.MinValue).First())
                .ToList();
        }

        public InspectorBandejaContadores ObtenerContadores(AocrBandejaRoleContext context)
        {
            var inspecciones = ObtenerInspeccionesAsignadas(context);
            return new InspectorBandejaContadores
            {
                Total = inspecciones.Count,
                Asignadas = inspecciones.Count(ins => EsEstadoAsignada(ins.Estado)),
                EnFase = inspecciones.Count(ins => EsEstadoEnFase(ins.Estado)),
                Observadas = inspecciones.Count(ins => EsEstadoObservada(ins.Estado)),
                Finalizadas = inspecciones.Count(ins => EsEstadoFinalizada(ins.Estado)),
                RevisionDocumental = ContarRevisionDocumentalPendiente(context)
            };
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

            ids.Add(value.Trim());
        }

        private static bool InspectorMatchesTextIdentifiers(Inspeccion inspeccion, HashSet<string> inspectorTextIds)
        {
            if (inspeccion == null || inspectorTextIds == null || inspectorTextIds.Count == 0)
            {
                return false;
            }

            return TextIdentifierMatches(inspeccion.CodigoInspector.HasValue ? inspeccion.CodigoInspector.Value.ToString() : null, inspectorTextIds)
                || TextIdentifierMatches(inspeccion.InspectorPrincipalCedula, inspectorTextIds)
                || TextIdentifierMatches(inspeccion.InspectorPrincipalNombre, inspectorTextIds);
        }

        private static bool TextIdentifierMatches(string value, HashSet<string> identifiers)
        {
            if (string.IsNullOrWhiteSpace(value) || identifiers == null || identifiers.Count == 0)
            {
                return false;
            }

            var normalized = value.Trim();
            return identifiers.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }

        private static bool EsEstadoAsignada(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.VERIFICACION_SOLICITUD, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.PAGO_VALIDADO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.VIATICOS_REQUERIDOS, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoEnFase(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoObservada(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, EstadosInspeccion.OBSERVADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoFinalizada(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, EstadosInspeccion.RESULTADO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class InspectorBandejaContadores
    {
        public int Total { get; set; }
        public int Asignadas { get; set; }
        public int EnFase { get; set; }
        public int Observadas { get; set; }
        public int Finalizadas { get; set; }
        public int RevisionDocumental { get; set; }
    }
}
