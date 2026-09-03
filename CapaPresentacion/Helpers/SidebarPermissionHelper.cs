using System.Collections.Generic;

namespace CapaPresentacion.Helpers
{
    public sealed class SidebarPermissionSnapshot
    {
        public SidebarPermissionSnapshot()
        {
            RolesRaw = new List<string>();
            RolesDisponibles = new List<string>();
        }

        public string RolActual { get; set; }
        public string RolDisplay { get; set; }
        public IList<string> RolesRaw { get; private set; }
        public IList<string> RolesDisponibles { get; private set; }
        public bool SinRolesRaw { get; set; }
        public bool EsAdministrador { get; set; }
        public bool EsSolicitanteRol { get; set; }
        public bool EsRepresentanteRtRol { get; set; }
        public bool EsSolicitanteORT { get; set; }
        public bool EsInspectorRol { get; set; }
        public bool EsCoordinadorRol { get; set; }
        public bool EsFinancieroRol { get; set; }
        public bool EsLegalRol { get; set; }
        public bool EsDirectorGeneralRol { get; set; }
        public bool EsDirdacRol { get; set; }
        public bool EsDcavRol { get; set; }
        public bool EsDircavRol { get; set; }
        public bool PuedeAdministracion { get; set; }
        public bool PuedeAprobarUsuarios { get; set; }
        public bool TieneNavegacionRol { get; set; }
    }

    /// <summary>
    /// Resolución de permisos de Sidebar con segregación estricta entre DIRCAV y DIRDAC.
    /// No permite que un rol directivo genérico active simultáneamente o de forma ambigua a DIRCAV y DIRDAC.
    /// </summary>
    public static class SidebarPermissionHelper
    {
        public static SidebarPermissionSnapshot Resolve(string selectedRole, object rawRolesObject)
        {
            var snapshot = new SidebarPermissionSnapshot();
            snapshot.RolActual = RoleGroupingHelper.NormalizeSelectedRole(selectedRole ?? string.Empty);
            foreach (var rawRole in RoleGroupingHelper.ExtractRoles(rawRolesObject, selectedRole))
            {
                snapshot.RolesRaw.Add(rawRole);
            }

            foreach (var role in RoleGroupingHelper.BuildUnifiedRoles(snapshot.RolesRaw))
            {
                snapshot.RolesDisponibles.Add(role);
            }

            snapshot.RolDisplay = RoleGroupingHelper.ToDisplayName(snapshot.RolActual);
            snapshot.SinRolesRaw = snapshot.RolesRaw.Count == 0;
            snapshot.EsAdministrador = RoleGroupingHelper.IsAdministrador(snapshot.RolActual);
            snapshot.EsSolicitanteRol = RoleGroupingHelper.IsSolicitante(snapshot.RolActual)
                && (snapshot.SinRolesRaw || RoleGroupingHelper.HasAnyRawRole(snapshot.RolesRaw, "Operador", "Solicitante", "RT"));
            snapshot.EsRepresentanteRtRol = RoleGroupingHelper.IsSolicitante(snapshot.RolActual)
                && RoleGroupingHelper.HasAnyRawRole(snapshot.RolesRaw, "RepresentanteTecnico", "Representante Técnico", "RepresentanteLegal", "RT");
            snapshot.EsSolicitanteORT = snapshot.EsSolicitanteRol || snapshot.EsRepresentanteRtRol;
            snapshot.EsInspectorRol = RoleGroupingHelper.IsInspectorTecnico(snapshot.RolActual)
                && (snapshot.SinRolesRaw || RoleGroupingHelper.HasAnyRawRole(snapshot.RolesRaw, "Inspector", "Tecnico", "EvaluadorTecnico"));
            snapshot.EsCoordinadorRol = RoleGroupingHelper.IsCoordinacion(snapshot.RolActual)
                && (snapshot.SinRolesRaw || RoleGroupingHelper.HasAnyRawRole(snapshot.RolesRaw, "Coordinacion", "Coordinador", "CoordinadorInspecciones", "Coordinador de Inspecciones", "COORDINADOR"));
            snapshot.EsFinancieroRol = RoleGroupingHelper.IsFinanciero(snapshot.RolActual)
                && (snapshot.SinRolesRaw || RoleGroupingHelper.HasAnyRawRole(snapshot.RolesRaw, "Financiero", "CoordinadorFinanciero", "CoordinacionFinanciera", "Coordinación Financiera", "DirectorFinanciero", "FINANCIERO"));
            snapshot.EsLegalRol = RoleGroupingHelper.IsCoordinacion(snapshot.RolActual)
                && (snapshot.SinRolesRaw || RoleGroupingHelper.HasAnyRawRole(snapshot.RolesRaw, "CoordinacionLegal", "CoordinadorLegal", "Coordinación Legal"));

            // SEGREGACIÓN ESTRICTA DIRCAV vs DIRDAC:
            // 1. DIRCAV activo únicamente si el rol normalizado es DIRCAV
            snapshot.EsDircavRol = RoleGroupingHelper.IsDircav(snapshot.RolActual);
            snapshot.EsDcavRol = snapshot.EsDircavRol;

            // 2. DIRDAC activo únicamente si el rol normalizado es DIRDAC (nunca DIRCAV)
            snapshot.EsDirdacRol = RoleGroupingHelper.IsDirdac(snapshot.RolActual) && !snapshot.EsDircavRol;
            snapshot.EsDirectorGeneralRol = snapshot.EsDirdacRol;

            snapshot.PuedeAdministracion = snapshot.EsAdministrador;
            snapshot.PuedeAprobarUsuarios = snapshot.EsAdministrador || snapshot.EsLegalRol || snapshot.EsCoordinadorRol || snapshot.EsDirdacRol || snapshot.EsDircavRol;
            snapshot.TieneNavegacionRol = snapshot.EsSolicitanteRol
                || snapshot.EsRepresentanteRtRol
                || snapshot.EsInspectorRol
                || snapshot.EsCoordinadorRol
                || snapshot.EsFinancieroRol
                || snapshot.EsDirdacRol
                || snapshot.EsDircavRol
                || snapshot.EsLegalRol
                || snapshot.EsAdministrador
                || snapshot.PuedeAprobarUsuarios;

            return snapshot;
        }
    }
}
