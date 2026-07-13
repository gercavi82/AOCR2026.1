using System.Collections.Generic;

namespace CapaPresentacion.Services
{
    public sealed class UsuarioContextoDto
    {
        public int UsuarioId { get; set; }
        public string Login { get; set; }
        public string NombreCompleto { get; set; }
        public string Correo { get; set; }
        public string RolActivo { get; set; }
        public IList<string> Roles { get; set; }
        public IList<string> RolesRaw { get; set; }
        public string CompaniaCodigo { get; set; }
        public string CompaniaNombre { get; set; }
        public bool EstaAutenticado { get; set; }
        public bool EsValido { get; set; }
        public bool EsAdministrador { get; set; }
        public bool EsCoordinacion { get; set; }
        public bool EsInspectorTecnico { get; set; }
        public bool EsFinanciero { get; set; }
        public bool EsDireccionJefaturaTecnica { get; set; }
        public bool EsSolicitante { get; set; }
        public bool EsLegal { get; set; }
    }
}
