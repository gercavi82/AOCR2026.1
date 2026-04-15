using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CapaModelo.Seguridad
{
    public class SeguridadUsuarioDTO
    {
        private static readonly HashSet<string> RolesInternos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Administrador", "Direccion", "JefaturaTecnica", "Financiero",
            "CoordinadorFinanciero", "CoordinacionLegal", "CoordinadorLegal",
            "Inspector", "Tecnico", "EvaluadorTecnico", "CoordinadorInspecciones",
            "DirectorFinanciero", "DirectorGeneral", "Recepcion"
        };

        private static readonly HashSet<string> RolesExternos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Solicitante", "Operador", "RepresentanteTecnico",
            "Representante Técnico", "RepresentanteLegal", "RT"
        };

        public int IdUsuario { get; set; }

        [Required]
        [StringLength(64)]
        public string CodigoUsuario { get; set; }

        [Required]
        [StringLength(120)]
        public string NombreUsuario { get; set; }

        [StringLength(120)]
        public string ApellidoUsuario { get; set; }

        [Required]
        [StringLength(160)]
        [EmailAddress]
        public string Correo { get; set; }

        public bool Activo { get; set; }

        public bool MustChangePassword { get; set; }

        public DateTime? UltimoLogin { get; set; }

        public string RolFallback { get; set; }

        public string RolesTexto { get; set; }

        public IList<int> RolesAsignados { get; set; } = new List<int>();

        /// <summary>
        /// Tipo derivado de los roles asignados: "Interno", "Externo" o "Sin rol".
        /// </summary>
        public string TipoUsuario
        {
            get
            {
                var roles = ParsearRoles();
                if (roles.Count == 0)
                    return "Sin rol";

                bool tieneInterno = roles.Any(r => RolesInternos.Contains(r));
                bool tieneExterno = roles.Any(r => RolesExternos.Contains(r));

                if (tieneInterno)
                    return "Interno";
                if (tieneExterno)
                    return "Externo";

                return "Externo";
            }
        }

        /// <summary>
        /// Indica si el usuario puede ser eliminado físicamente (solo externos sin bloqueos).
        /// </summary>
        public bool EsEliminableFisicamente
        {
            get { return TipoUsuario != "Interno"; }
        }

        private List<string> ParsearRoles()
        {
            if (string.IsNullOrWhiteSpace(RolesTexto))
                return new List<string>();

            return RolesTexto
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => r.Length > 0)
                .ToList();
        }
    }
}
