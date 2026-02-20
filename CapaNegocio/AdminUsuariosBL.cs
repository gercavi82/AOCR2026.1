using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo.Seguridad;
using CapaNegocio.Helpers;

namespace CapaNegocio
{
    public static class AdminUsuariosBL
    {
        private static readonly AdminUsuariosDAO _dao = new AdminUsuariosDAO();

        public static List<SeguridadUsuarioDTO> BuscarUsuarios(string filtro, bool? activo)
        {
            return _dao.BuscarUsuarios(filtro, activo);
        }

        public static SeguridadUsuarioDTO ObtenerUsuarioPorId(int idUsuario)
        {
            if (idUsuario <= 0)
            {
                return null;
            }

            return _dao.ObtenerUsuarioPorId(idUsuario);
        }

        public static List<SeguridadRolDTO> ObtenerRolesActivos()
        {
            return _dao.ObtenerRolesActivos();
        }

        public static List<SeguridadPermisoDTO> ObtenerPermisos(bool soloActivos)
        {
            return _dao.ObtenerPermisos(soloActivos);
        }

        public static List<int> ObtenerPermisosPorRol(int codigoRol)
        {
            if (codigoRol <= 0)
            {
                return new List<int>();
            }

            return _dao.ObtenerPermisosPorRol(codigoRol);
        }

        public static bool CrearUsuario(
            SeguridadUsuarioDTO usuario,
            IEnumerable<int> roles,
            string passwordInicial,
            bool generarPasswordTemporal,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out int nuevoId,
            out string passwordTemporal,
            out string mensaje)
        {
            nuevoId = 0;
            passwordTemporal = null;
            mensaje = string.Empty;

            if (usuario == null)
            {
                mensaje = "Datos de usuario inválidos.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(usuario.CodigoUsuario))
            {
                mensaje = "El usuario es obligatorio.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(usuario.NombreUsuario))
            {
                mensaje = "El nombre es obligatorio.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(usuario.Correo))
            {
                mensaje = "El correo es obligatorio.";
                return false;
            }

            var rolesNormalizados = (roles ?? Enumerable.Empty<int>())
                .Where(r => r > 0)
                .Distinct()
                .ToList();

            if (!rolesNormalizados.Any())
            {
                mensaje = "Debe asignar al menos un rol.";
                return false;
            }

            if (_dao.ExisteCodigoUsuario(usuario.CodigoUsuario))
            {
                mensaje = "El código de usuario ya existe.";
                return false;
            }

            if (_dao.ExisteCorreo(usuario.Correo))
            {
                mensaje = "El correo ya existe.";
                return false;
            }

            var passwordPlano = (passwordInicial ?? string.Empty).Trim();
            if (generarPasswordTemporal || string.IsNullOrWhiteSpace(passwordPlano))
            {
                passwordPlano = PasswordHelper.GenerarPasswordAleatoria(12);
                passwordTemporal = passwordPlano;
            }

            var validacion = PasswordHelper.ValidarFortaleza(passwordPlano);
            if (!validacion.esValida)
            {
                mensaje = validacion.mensaje;
                return false;
            }

            var hash = PasswordHelper.HashPassword(passwordPlano);
            nuevoId = _dao.CrearUsuarioConRoles(
                usuario,
                hash,
                rolesNormalizados,
                actorUsuarioId,
                actorCodigoUsuario,
                ip);

            if (nuevoId <= 0)
            {
                mensaje = "No se pudo crear el usuario.";
                return false;
            }

            mensaje = "Usuario creado correctamente.";
            return true;
        }

        public static bool ActualizarUsuario(
            SeguridadUsuarioDTO usuario,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out string mensaje)
        {
            mensaje = string.Empty;

            if (usuario == null || usuario.IdUsuario <= 0)
            {
                mensaje = "Usuario inválido.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(usuario.NombreUsuario))
            {
                mensaje = "El nombre es obligatorio.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(usuario.Correo))
            {
                mensaje = "El correo es obligatorio.";
                return false;
            }

            if (_dao.ExisteCorreo(usuario.Correo, usuario.IdUsuario))
            {
                mensaje = "El correo ya está asignado a otro usuario.";
                return false;
            }

            var ok = _dao.ActualizarUsuario(usuario, actorUsuarioId, actorCodigoUsuario, ip);
            mensaje = ok ? "Usuario actualizado correctamente." : "No se pudo actualizar el usuario.";
            return ok;
        }

        public static bool CambiarEstadoUsuario(
            int idUsuario,
            bool activo,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out string mensaje)
        {
            mensaje = string.Empty;
            if (idUsuario <= 0)
            {
                mensaje = "Usuario inválido.";
                return false;
            }

            var ok = _dao.CambiarEstadoUsuario(idUsuario, activo, actorUsuarioId, actorCodigoUsuario, ip);
            mensaje = ok
                ? (activo ? "Usuario activado correctamente." : "Usuario desactivado correctamente.")
                : "No se pudo actualizar el estado del usuario.";
            return ok;
        }

        public static bool ReemplazarRolesUsuario(
            int idUsuario,
            IEnumerable<int> roles,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out string mensaje)
        {
            mensaje = string.Empty;

            if (idUsuario <= 0)
            {
                mensaje = "Usuario inválido.";
                return false;
            }

            var rolesNormalizados = (roles ?? Enumerable.Empty<int>())
                .Where(r => r > 0)
                .Distinct()
                .ToList();

            if (!rolesNormalizados.Any())
            {
                mensaje = "Debe seleccionar al menos un rol.";
                return false;
            }

            var ok = _dao.ReemplazarRolesUsuario(idUsuario, rolesNormalizados, actorUsuarioId, actorCodigoUsuario, ip);
            mensaje = ok ? "Roles actualizados correctamente." : "No se pudieron actualizar los roles.";
            return ok;
        }

        public static bool ReemplazarPermisosRol(
            int codigoRol,
            IEnumerable<int> permisos,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out string mensaje)
        {
            mensaje = string.Empty;

            if (codigoRol <= 0)
            {
                mensaje = "Rol inválido.";
                return false;
            }

            var permisosNormalizados = (permisos ?? Enumerable.Empty<int>())
                .Where(p => p > 0)
                .Distinct()
                .ToList();

            var ok = _dao.ReemplazarPermisosRol(codigoRol, permisosNormalizados, actorUsuarioId, actorCodigoUsuario, ip);
            mensaje = ok ? "Permisos del rol actualizados correctamente." : "No se pudieron actualizar los permisos del rol.";
            return ok;
        }

        public static bool ResetPassword(
            int idUsuario,
            bool generarTemporal,
            string passwordNueva,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out string passwordTemporal,
            out string mensaje)
        {
            passwordTemporal = null;
            mensaje = string.Empty;

            if (idUsuario <= 0)
            {
                mensaje = "Usuario invalido.";
                return false;
            }

            var passwordPlano = (passwordNueva ?? string.Empty).Trim();
            if (generarTemporal || string.IsNullOrWhiteSpace(passwordPlano))
            {
                passwordPlano = PasswordHelper.GenerarPasswordAleatoria(12);
                passwordTemporal = passwordPlano;
            }

            var usuario = _dao.ObtenerUsuarioPorId(idUsuario);
            if (usuario == null)
            {
                mensaje = "Usuario invalido.";
                return false;
            }

            var validacion = PasswordHelper.ValidarFortaleza(passwordPlano);
            if (!validacion.esValida)
            {
                mensaje = validacion.mensaje;
                return false;
            }

            var hash = PasswordHelper.HashPassword(passwordPlano);
            var ok = _dao.ResetPassword(idUsuario, hash, actorUsuarioId, actorCodigoUsuario, ip);
            if (!ok)
            {
                mensaje = "No se pudo restablecer la contrasena.";
                return false;
            }

            string mensajeCorreo;
            var correoOk = NotificarResetPassword(usuario, passwordPlano, actorCodigoUsuario, out mensajeCorreo);
            mensaje = correoOk
                ? "Contrasena restablecida correctamente. " + mensajeCorreo
                : "Contrasena restablecida, pero " + mensajeCorreo;

            return true;
        }

        private static bool NotificarResetPassword(
            SeguridadUsuarioDTO usuario,
            string passwordTemporal,
            string actorCodigoUsuario,
            out string mensaje)
        {
            mensaje = string.Empty;

            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Correo))
            {
                mensaje = "el usuario no tiene correo registrado.";
                return false;
            }

            var nombre = string.Format(
                "{0} {1}",
                (usuario.NombreUsuario ?? string.Empty).Trim(),
                (usuario.ApellidoUsuario ?? string.Empty).Trim()).Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                nombre = (usuario.CodigoUsuario ?? "Usuario").Trim();
            }

            var asunto = "Cambio de contrasena - Sistema AOCR";
            var cuerpo = ConstruirPlantillaResetPassword(nombre, passwordTemporal, actorCodigoUsuario);

            try
            {
                var queueService = new EmailQueueService();
                var configService = new SecureConfigurationService();
                var servicioCorreo = new EnviarCorreo(configService, queueService);

                if (servicioCorreo.EnviarEncolado(usuario.Correo, asunto, cuerpo, null, "RESET_PASSWORD"))
                {
                    mensaje = "se envio un correo con la nueva contrasena temporal.";
                    return true;
                }

                if (servicioCorreo.enviaMensajeCorreo(usuario.Correo, asunto, cuerpo))
                {
                    mensaje = "la cola de correos fallo, pero se envio el correo directamente.";
                    return true;
                }

                mensaje = "no se pudo enviar el correo de notificacion.";
                return false;
            }
            catch
            {
                try
                {
                    var servicioCorreo = new EnviarCorreo();
                    if (servicioCorreo.enviaMensajeCorreo(usuario.Correo, asunto, cuerpo))
                    {
                        mensaje = "la cola de correos fallo, pero se envio el correo directamente.";
                        return true;
                    }
                }
                catch
                {
                    // Ignorar para devolver mensaje uniforme.
                }

                mensaje = "no se pudo enviar el correo de notificacion.";
                return false;
            }
        }

        private static string ConstruirPlantillaResetPassword(
            string nombreUsuario,
            string passwordTemporal,
            string actorCodigoUsuario)
        {
            var nombreSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(nombreUsuario) ? "Usuario" : nombreUsuario);
            var passwordSegura = WebUtility.HtmlEncode(passwordTemporal ?? string.Empty);
            var actorSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(actorCodigoUsuario) ? "Administrador" : actorCodigoUsuario);
            var fecha = WebUtility.HtmlEncode(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

            return string.Format(@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; margin:0; padding:20px; background:#f4f6f8;'>
  <div style='max-width:620px; margin:0 auto; background:#ffffff; border:1px solid #d9dee5; border-radius:8px; padding:24px;'>
    <h2 style='margin:0 0 16px 0; color:#1f3a5f;'>Contrasena restablecida</h2>
    <p style='margin:0 0 12px 0;'>Estimado/a <strong>{0}</strong>,</p>
    <p style='margin:0 0 12px 0;'>Se registro un cambio de contrasena para su cuenta en AOCR.</p>
    <div style='margin:16px 0; padding:12px; background:#f8fbff; border:1px solid #d7e7ff; border-radius:6px;'>
      <strong>Nueva contrasena temporal:</strong> {1}
    </div>
    <p style='margin:0 0 8px 0;'>Usuario que realizo el cambio: <strong>{2}</strong></p>
    <p style='margin:0 0 8px 0;'>Fecha: {3}</p>
    <p style='margin:0 0 12px 0;'>Por seguridad, en el proximo inicio de sesion se le pedira cambiar su contrasena.</p>
    <hr style='margin:20px 0; border:none; border-top:1px solid #e8ecf1;' />
    <p style='margin:0; font-size:12px; color:#6b7785;'>Mensaje automatico del sistema AOCR.</p>
  </div>
</body>
</html>",
                nombreSeguro,
                passwordSegura,
                actorSeguro,
                fecha);
        }
    }
}
