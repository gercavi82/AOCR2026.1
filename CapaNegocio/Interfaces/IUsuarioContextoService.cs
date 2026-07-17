using System;
using CapaNegocio.DTOs;

namespace CapaNegocio.Interfaces
{
    public interface IUsuarioContextoService
    {
        UsuarioContexto ObtenerContextoActual();
        void ValidarAutenticacion();
        void ValidarRol(params string[] rolesPermitidos);
    }
}
