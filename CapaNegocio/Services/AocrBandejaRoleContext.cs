using System.Collections.Generic;

namespace CapaNegocio.Services
{
    public sealed class AocrBandejaRoleContext
    {
        public int UserId { get; set; }
        public string CodigoUsuario { get; set; }
        public string UserName { get; set; }
        public string RolActivo { get; set; }
        public IList<string> RolesUnificados { get; set; }
        public bool EsAdministrador { get; set; }
        public bool EsCoordinacion { get; set; }
        public bool EsInspectorTecnico { get; set; }
        public bool EsFinanciero { get; set; }
        public bool EsDireccionJefaturaTecnica { get; set; }
        public bool EsSolicitante { get; set; }
        public bool EsLegal { get; set; }
        public string CodigoCompaniaActiva { get; set; }
    }
}
