using System;
using System.Collections.Generic;

namespace CapaNegocio.DTOs
{
    public class UsuarioContexto
    {
        public int UsuarioId { get; set; }
        public string LoginNormalizado { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public List<string> Roles { get; set; }
        public string RolSeleccionado { get; set; }
        public int? CompaniaActivaId { get; set; }
        public string IdentificadorInstitucional { get; set; }
        public string CodigoInstitucional { get; set; }
        public bool EstaAutenticado { get; set; }
        
        public UsuarioContexto()
        {
            Roles = new List<string>();
        }
    }
}
