using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CapaDatos.DAOs;
using CapaDatos.Models;

namespace CapaNegocio
{
    public static class UsuarioInternoRTBL
    {
        private static readonly UsuarioInternoRTDAO _dao = new UsuarioInternoRTDAO();

        public static List<UsuarioInternoRTRegistro> ListarUsuariosInternos(bool incluirInactivos)
        {
            return incluirInactivos ? _dao.ListarTodos() : _dao.ListarActivos();
        }

        public static UsuarioInternoRTRegistro ObtenerPorId(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            return _dao.ObtenerPorId(id);
        }

        public static List<TecnicoInternoDisponible> BuscarTecnicosDisponibles(string filtro, int? excluirUsuarioInternoId = null)
        {
            return _dao.BuscarTecnicosDisponibles(filtro, excluirUsuarioInternoId);
        }

        public static TecnicoInternoDisponible ObtenerTecnicoDisponiblePorId(int tecnicoId)
        {
            if (tecnicoId <= 0)
            {
                return null;
            }

            return _dao.ObtenerTecnicoDisponiblePorId(tecnicoId);
        }

        public static bool CrearUsuarioInterno(UsuarioInternoRTRegistro registro, string actor, out string mensaje)
        {
            if (!ValidarRegistro(registro, false, out mensaje))
            {
                return false;
            }

            return _dao.GuardarRegistro(registro, actor, out mensaje);
        }

        public static bool ActualizarUsuarioInterno(UsuarioInternoRTRegistro registro, string actor, out string mensaje)
        {
            if (!ValidarRegistro(registro, true, out mensaje))
            {
                return false;
            }

            return _dao.ActualizarRegistro(registro, actor, out mensaje);
        }

        public static bool CambiarEstado(int id, bool activo, string actor, out string mensaje)
        {
            return _dao.CambiarEstado(id, activo, actor, out mensaje);
        }

        public static UsuarioInternoRTRegistro ResolverDestinatarioAsignacion(string codigoUsuario)
        {
            return _dao.ResolverDestinatarioAsignacionPorCodigoUsuario(codigoUsuario);
        }

        public static List<UsuarioInternoRTRegistro> ListarInspectoresAsignables(string tipoInspector)
        {
            return _dao.ListarInspectoresAsignables(tipoInspector);
        }

        public static UsuarioInternoRTRegistro ObtenerInspectorAsignable(string codigoUsuario, string tipoInspector)
        {
            return _dao.ObtenerInspectorAsignableActivo(codigoUsuario, tipoInspector);
        }

        public static string ObtenerCorreoInstitucionalPorTecnicoId(int tecnicoId)
        {
            return _dao.ObtenerCorreoInstitucionalPorTecnicoId(tecnicoId);
        }

        public static string ObtenerCorreoInstitucionalPorCodigoUsuario(string codigoUsuario)
        {
            return _dao.ObtenerCorreoInstitucionalPorCodigoUsuario(codigoUsuario);
        }

        public static bool ExisteCorreoInstitucional(string correo, int? excluirId = null)
        {
            return _dao.ExisteCorreoInstitucional(correo, excluirId);
        }

        public static bool ExisteTecnicoActivo(int tecnicoId, int? excluirId = null)
        {
            return _dao.ExisteTecnicoActivo(tecnicoId, excluirId);
        }

        public static bool VincularCuentaAcceso(int idRegistro, int usuarioId, string actor, out string mensaje)
        {
            return _dao.VincularCuentaAcceso(idRegistro, usuarioId, actor, out mensaje);
        }

        private static bool ValidarRegistro(UsuarioInternoRTRegistro registro, bool esEdicion, out string mensaje)
        {
            mensaje = string.Empty;

            if (registro == null)
            {
                mensaje = "No se recibió información del usuario interno.";
                return false;
            }

            if (esEdicion && registro.Id <= 0)
            {
                mensaje = "El identificador del usuario interno es inválido.";
                return false;
            }

            if ((!registro.TecnicoId.HasValue || registro.TecnicoId.Value <= 0)
                && string.IsNullOrWhiteSpace(registro.CodigoUsuario)
                && string.IsNullOrWhiteSpace(registro.Identificacion))
            {
                mensaje = "Debe seleccionar un inspector de origen válido.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(registro.RolInterno))
            {
                mensaje = "Debe seleccionar un rol interno.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(registro.CorreoInstitucional))
            {
                var validator = new EmailAddressAttribute();
                if (!validator.IsValid(registro.CorreoInstitucional.Trim()))
                {
                    mensaje = "El correo institucional no tiene un formato válido.";
                    return false;
                }
            }

            if (registro.TecnicoId.HasValue
                && registro.TecnicoId.Value > 0
                && ExisteTecnicoActivo(registro.TecnicoId.Value, esEdicion ? (int?)registro.Id : null))
            {
                mensaje = "Ya existe un usuario interno activo para el inspector seleccionado.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(registro.CorreoInstitucional) && ExisteCorreoInstitucional(registro.CorreoInstitucional, esEdicion ? (int?)registro.Id : null))
            {
                mensaje = "El correo institucional ya está asignado a otro usuario interno activo.";
                return false;
            }

            return true;
        }
    }
}
