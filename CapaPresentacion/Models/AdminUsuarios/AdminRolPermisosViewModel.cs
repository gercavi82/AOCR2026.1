using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaModelo.Seguridad;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminRolPermisosViewModel
    {
        public int CodigoRolSeleccionado { get; set; }
        public string NombreRolSeleccionado { get; set; }
        public bool InfraestructuraPermisosDisponible { get; set; }

        public IList<int> PermisosSeleccionados { get; set; } = new List<int>();
        public IList<SeguridadPermisoDTO> PermisosDisponibles { get; set; } = new List<SeguridadPermisoDTO>();
        public IEnumerable<SelectListItem> RolesDisponibles { get; set; } = new List<SelectListItem>();

        public DateTime? FechaUltimaActualizacion { get; set; }
        public string VersionEsperada => FechaUltimaActualizacion.HasValue
            ? FechaUltimaActualizacion.Value.ToUniversalTime().ToString("O")
            : string.Empty;

        public int PermisosAsignados => PermisosSeleccionados != null ? PermisosSeleccionados.Count : 0;
        public int TotalPermisos => PermisosDisponibles != null ? PermisosDisponibles.Count : 0;
        public int ModulosConAcceso => PermisosDisponibles != null && PermisosSeleccionados != null
            ? PermisosDisponibles
                .Where(p => PermisosSeleccionados.Contains(p.IdPermiso))
                .Where(p => !string.IsNullOrWhiteSpace(p.Modulo))
                .Select(p => p.Modulo)
                .Distinct()
                .Count()
            : 0;

        public string FechaActualizacionDisplay => FechaUltimaActualizacion.HasValue
            ? FechaUltimaActualizacion.Value.ToString("dd/MM/yyyy HH:mm")
            : "\u2014";

        public string FechaActualizacionSub => FechaUltimaActualizacion.HasValue
            ? "Ultimo cambio registrado"
            : "Sin registros";

        public IList<PermisoModuloGrupo> ModulosAgrupados
        {
            get
            {
                if (PermisosDisponibles == null || !PermisosDisponibles.Any())
                    return new List<PermisoModuloGrupo>();

                return PermisosDisponibles
                    .GroupBy(p => string.IsNullOrWhiteSpace(p.Modulo) ? "Sin modulo" : p.Modulo)
                    .OrderBy(g => g.Key)
                    .Select(g => new PermisoModuloGrupo
                    {
                        Nombre = g.Key,
                        Permisos = g.OrderBy(p => p.TipoAccion).ThenBy(p => p.Codigo).ToList(),
                        PermisosAsignados = g.Count(p => PermisosSeleccionados.Contains(p.IdPermiso))
                    })
                    .ToList();
            }
        }

        public string[] AccionesDisponibles => (PermisosDisponibles ?? new List<SeguridadPermisoDTO>())
            .Where(p => !string.IsNullOrWhiteSpace(p.TipoAccion))
            .Select(p => p.TipoAccion.Trim().ToUpperInvariant())
            .Distinct()
            .OrderBy(a => a)
            .ToArray();
    }

    public class PermisoModuloGrupo
    {
        public string Nombre { get; set; }
        public int PermisosAsignados { get; set; }
        public IList<SeguridadPermisoDTO> Permisos { get; set; } = new List<SeguridadPermisoDTO>();

        public int TotalPermisos => Permisos != null ? Permisos.Count : 0;
        public string ConteoDisplay => TotalPermisos == 1 ? "1 permiso" : TotalPermisos + " permisos";
    }
}
