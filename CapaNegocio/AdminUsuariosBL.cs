using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo.Common;
using CapaModelo.Seguridad;
using CapaNegocio.Helpers;

namespace CapaNegocio
{
    public static class AdminUsuariosBL
    {
        private static readonly AdminUsuariosDAO _dao = new AdminUsuariosDAO();
        private static readonly ILoggingService _logger = LoggingServiceFactory.Create();

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

        public static SeguridadUsuarioDTO ObtenerUsuarioPorCodigoUsuario(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return null;
            }

            return _dao.ObtenerUsuarioPorCodigoUsuario(codigoUsuario);
        }

        public static List<SeguridadRolDTO> ObtenerRolesActivos()
        {
            return _dao.ObtenerRolesActivos();
        }

        public static List<SeguridadRolDTO> ObtenerRolesFuncionalesAocr()
        {
            return _dao.ObtenerRolesFuncionalesAocr();
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

        public static DateTime? ObtenerFechaUltimaActualizacionPermisosRol(int codigoRol)
        {
            if (codigoRol <= 0)
            {
                return null;
            }

            return _dao.ObtenerFechaUltimaActualizacionPermisosRol(codigoRol);
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
            bool correoEnviado;
            string detalleCorreo;
            return CrearUsuario(
                usuario,
                roles,
                passwordInicial,
                generarPasswordTemporal,
                actorUsuarioId,
                actorCodigoUsuario,
                ip,
                out nuevoId,
                out passwordTemporal,
                out correoEnviado,
                out detalleCorreo,
                out mensaje);
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
            out bool correoEnviado,
            out string detalleCorreo,
            out string mensaje)
        {
            nuevoId = 0;
            passwordTemporal = null;
            correoEnviado = false;
            detalleCorreo = string.Empty;
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

            string mensajeCorreo;
            correoEnviado = NotificarCredencialesCreacion(
                usuario,
                passwordPlano,
                actorCodigoUsuario,
                out mensajeCorreo,
                out detalleCorreo);
            mensaje = correoEnviado
                ? "Usuario creado correctamente. " + mensajeCorreo
                : "Usuario creado correctamente, pero " + mensajeCorreo;

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

        public static bool ActualizarPermisosRolDiferencial(
            int codigoRol,
            IEnumerable<int> agregados,
            IEnumerable<int> retirados,
            DateTime? versionEsperada,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out bool conflictoVersion,
            out DateTime? versionActual,
            out string mensaje)
        {
            mensaje = string.Empty;
            conflictoVersion = false;
            versionActual = null;

            if (codigoRol <= 0)
            {
                mensaje = "Rol inválido.";
                return false;
            }

            var ok = _dao.ActualizarPermisosRolDiferencial(
                codigoRol,
                agregados,
                retirados,
                versionEsperada,
                actorUsuarioId,
                actorCodigoUsuario,
                ip,
                out conflictoVersion,
                out versionActual,
                out mensaje);
            if (conflictoVersion)
            {
                return false;
            }

            mensaje = ok
                ? "Permisos del rol actualizados correctamente."
                : (string.IsNullOrWhiteSpace(mensaje) ? "No se pudieron actualizar los permisos del rol." : mensaje);
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
            bool correoEnviado;
            string detalleCorreo;
            return ResetPassword(
                idUsuario,
                generarTemporal,
                passwordNueva,
                actorUsuarioId,
                actorCodigoUsuario,
                ip,
                null,
                out passwordTemporal,
                out correoEnviado,
                out detalleCorreo,
                out mensaje);
        }

        public static bool ResetPassword(
            int idUsuario,
            bool generarTemporal,
            string passwordNueva,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            string correoDestinoOverride,
            out string passwordTemporal,
            out bool correoEnviado,
            out string detalleCorreo,
            out string mensaje)
        {
            passwordTemporal = null;
            correoEnviado = false;
            detalleCorreo = string.Empty;
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

            if (!string.IsNullOrWhiteSpace(correoDestinoOverride))
            {
                usuario.Correo = correoDestinoOverride.Trim();
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
                mensaje = "No se pudo restablecer la contraseña.";
                return false;
            }

            string mensajeCorreo;
            correoEnviado = NotificarResetPassword(
                usuario,
                passwordPlano,
                actorCodigoUsuario,
                out mensajeCorreo,
                out detalleCorreo);
            mensaje = correoEnviado
                ? "Contrasena restablecida correctamente. " + mensajeCorreo
                : "Contrasena restablecida, pero " + mensajeCorreo;

            return true;
        }

        public static bool ResetPassword(
            int idUsuario,
            bool generarTemporal,
            string passwordNueva,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out string passwordTemporal,
            out bool correoEnviado,
            out string detalleCorreo,
            out string mensaje)
        {
            return ResetPassword(
                idUsuario,
                generarTemporal,
                passwordNueva,
                actorUsuarioId,
                actorCodigoUsuario,
                ip,
                null,
                out passwordTemporal,
                out correoEnviado,
                out detalleCorreo,
                out mensaje);
        }

        public static bool ResetPassword(
            int idUsuario,
            bool generarTemporal,
            string passwordNueva,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            string correoDestinoOverride,
            out string passwordTemporal,
            out string mensaje)
        {
            bool correoEnviado;
            string detalleCorreo;
            return ResetPassword(
                idUsuario,
                generarTemporal,
                passwordNueva,
                actorUsuarioId,
                actorCodigoUsuario,
                ip,
                correoDestinoOverride,
                out passwordTemporal,
                out correoEnviado,
                out detalleCorreo,
                out mensaje);
        }

        public static List<SeguridadUsuarioDTO> ObtenerUsuariosActivosParaTransferencia(int excluirIdUsuario)
        {
            if (excluirIdUsuario <= 0)
            {
                return BuscarUsuarios(null, true);
            }

            return _dao.ObtenerUsuariosActivosParaTransferencia(excluirIdUsuario);
        }

        public static UsuarioTransferenciaPreviewDTO ObtenerImpactoTransferencia(int idUsuarioOrigen)
        {
            if (idUsuarioOrigen <= 0)
            {
                return new UsuarioTransferenciaPreviewDTO();
            }

            return _dao.ObtenerImpactoTransferencia(idUsuarioOrigen);
        }

        public static bool TransferirYDesactivarUsuario(
            int idUsuarioOrigen,
            int idUsuarioDestino,
            string motivo,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out UsuarioTransferenciaResultadoDTO resultado,
            out string mensaje)
        {
            mensaje = string.Empty;
            resultado = new UsuarioTransferenciaResultadoDTO
            {
                Ok = false,
                Mensaje = "No se pudo completar la transferencia."
            };

            if (idUsuarioOrigen <= 0 || idUsuarioDestino <= 0)
            {
                mensaje = "Debe seleccionar usuario origen y usuario destino.";
                resultado.Mensaje = mensaje;
                return false;
            }

            if (idUsuarioOrigen == idUsuarioDestino)
            {
                mensaje = "El usuario destino no puede ser igual al origen.";
                resultado.Mensaje = mensaje;
                return false;
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                mensaje = "El motivo de transferencia es obligatorio.";
                resultado.Mensaje = mensaje;
                return false;
            }

            if (motivo.Trim().Length < 8)
            {
                mensaje = "El motivo de transferencia debe tener al menos 8 caracteres.";
                resultado.Mensaje = mensaje;
                return false;
            }

            var ok = _dao.TransferirYDesactivarUsuario(
                idUsuarioOrigen,
                idUsuarioDestino,
                motivo,
                actorUsuarioId,
                actorCodigoUsuario,
                ip,
                out resultado);

            mensaje = ok
                ? "Transferencia ejecutada correctamente."
                : (resultado != null && !string.IsNullOrWhiteSpace(resultado.Mensaje)
                    ? resultado.Mensaje
                    : "No se pudo completar la transferencia.");

            if (resultado != null && string.IsNullOrWhiteSpace(resultado.Mensaje))
            {
                resultado.Mensaje = mensaje;
            }

            return ok;
        }

        public static bool EliminarUsuarioPermanente(
            int idUsuario,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out string mensaje)
        {
            mensaje = string.Empty;

            if (idUsuario <= 0)
            {
                mensaje = "ID de usuario no válido.";
                return false;
            }

            var usuario = _dao.ObtenerUsuarioPorId(idUsuario);
            if (usuario == null)
            {
                mensaje = "Usuario no encontrado.";
                return false;
            }

            bool ok = UsuarioDAO.EliminarUsuarioRT(idUsuario, out mensaje, true);

            return ok;
        }

        private static bool NotificarResetPassword(
            SeguridadUsuarioDTO usuario,
            string passwordTemporal,
            string actorCodigoUsuario,
            out string mensaje)
        {
            string detalleError;
            return NotificarResetPassword(
                usuario,
                passwordTemporal,
                actorCodigoUsuario,
                out mensaje,
                out detalleError);
        }

        private static bool NotificarResetPassword(
            SeguridadUsuarioDTO usuario,
            string passwordTemporal,
            string actorCodigoUsuario,
            out string mensaje,
            out string detalleError)
        {
            mensaje = string.Empty;
            detalleError = string.Empty;

            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Correo))
            {
                mensaje = "El usuario no tiene correo registrado.";
                detalleError = "No hay correo institucional configurado para el usuario.";
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

            var asunto = "Cambio de contraseña - Sistema AOCR";
            var cuerpo = ConstruirPlantillaResetPassword(nombre, usuario.CodigoUsuario, passwordTemporal, actorCodigoUsuario);

            return EnviarCorreoCredenciales(usuario.Correo, asunto, cuerpo, out mensaje, out detalleError);
        }

        private static bool NotificarCredencialesCreacion(
            SeguridadUsuarioDTO usuario,
            string passwordTemporal,
            string actorCodigoUsuario,
            out string mensaje)
        {
            string detalleError;
            return NotificarCredencialesCreacion(
                usuario,
                passwordTemporal,
                actorCodigoUsuario,
                out mensaje,
                out detalleError);
        }

        private static bool NotificarCredencialesCreacion(
            SeguridadUsuarioDTO usuario,
            string passwordTemporal,
            string actorCodigoUsuario,
            out string mensaje,
            out string detalleError)
        {
            mensaje = string.Empty;
            detalleError = string.Empty;

            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Correo))
            {
                mensaje = "El usuario no tiene correo registrado.";
                detalleError = "No hay correo institucional configurado para el usuario.";
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

            var asunto = "Cuenta creada - Sistema AOCR";
            var cuerpo = ConstruirPlantillaCreacionUsuario(nombre, usuario.CodigoUsuario, passwordTemporal, actorCodigoUsuario);

            return EnviarCorreoCredenciales(usuario.Correo, asunto, cuerpo, out mensaje, out detalleError);
        }

        private static bool EnviarCorreoCredenciales(
            string correoDestino,
            string asunto,
            string cuerpo,
            out string mensaje)
        {
            string detalleError;
            return EnviarCorreoCredenciales(correoDestino, asunto, cuerpo, out mensaje, out detalleError);
        }

        private static bool EnviarCorreoCredenciales(
            string correoDestino,
            string asunto,
            string cuerpo,
            out string mensaje,
            out string detalleError)
        {
            mensaje = string.Empty;
            detalleError = string.Empty;

            if (string.IsNullOrWhiteSpace(correoDestino))
            {
                mensaje = "No se pudo enviar el correo de notificación.";
                detalleError = "Destino de correo vacío.";
                _logger.LogWarning("AdminUsuariosBL.EnviarCorreoCredenciales sin destinatario.");
                return false;
            }

            try
            {
                var queueService = new EmailQueueService();
                var configService = new SecureConfigurationService();
                var servicioCorreo = new EnviarCorreo(configService, queueService);

                if (servicioCorreo.EnviarEncolado(correoDestino, asunto, cuerpo, null, "USER_CREDENTIALS"))
                {
                    mensaje = "Se envió un correo con las credenciales temporales.";
                    return true;
                }

                if (servicioCorreo.enviaMensajeCorreo(correoDestino, asunto, cuerpo))
                {
                    mensaje = "La cola de correos falló, pero se envió el correo directamente.";
                    return true;
                }

                mensaje = "No se pudo enviar el correo de notificación.";
                detalleError = "La cola y el envío directo retornaron false.";
                _logger.LogWarning(
                    "AdminUsuariosBL.EnviarCorreoCredenciales fallo sin excepcion. Destino=" + correoDestino
                    + ", Asunto=" + (asunto ?? string.Empty));
                return false;
            }
            catch (Exception ex)
            {
                detalleError = ex.Message;
                _logger.LogError(
                    ex,
                    new LogContext
                    {
                        ErrorCode = "ADMIN_CREDENTIALS_EMAIL_ERROR",
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "Destino", correoDestino },
                            { "Asunto", asunto ?? string.Empty }
                        }
                    });

                try
                {
                    var servicioCorreo = new EnviarCorreo();
                    if (servicioCorreo.enviaMensajeCorreo(correoDestino, asunto, cuerpo))
                    {
                        mensaje = "La cola de correos falló, pero se envió el correo directamente.";
                        return true;
                    }
                }
                catch (Exception fallbackEx)
                {
                    if (string.IsNullOrWhiteSpace(detalleError))
                    {
                        detalleError = fallbackEx.Message;
                    }

                    _logger.LogError(
                        fallbackEx,
                        new LogContext
                        {
                            ErrorCode = "ADMIN_CREDENTIALS_EMAIL_FALLBACK_ERROR",
                            AdditionalData = new Dictionary<string, object>
                            {
                                { "Destino", correoDestino },
                                { "Asunto", asunto ?? string.Empty }
                            }
                        });
                }

                mensaje = "No se pudo enviar el correo de notificación.";
                return false;
            }
        }

        private static string ConstruirPlantillaResetPassword(
            string nombreUsuario,
            string codigoUsuario,
            string passwordTemporal,
            string actorCodigoUsuario)
        {
            var nombreSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(nombreUsuario) ? "Usuario" : nombreUsuario);
            var codigoSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(codigoUsuario) ? string.Empty : codigoUsuario);
            var passwordSegura = WebUtility.HtmlEncode(passwordTemporal ?? string.Empty);
            var actorSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(actorCodigoUsuario) ? "Administrador" : actorCodigoUsuario);
            var fecha = WebUtility.HtmlEncode(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

            var extraHtml = "<p style='margin:0 0 8px 0; font-size:14px; color:#3a4f5e;'><strong>Usuario:</strong> " + codigoSeguro + "</p>"
                + "<div style='margin:16px 0; padding:12px 14px; background:#f8fbff; border:1px solid #d7e7ff; border-radius:6px; font-size:14px;'>"
                + "<strong>Nueva contraseña temporal:</strong> " + passwordSegura + "</div>"
                + "<p style='margin:0 0 8px 0; font-size:13px; color:#3a4f5e;'>Usuario que realizó el cambio: <strong>" + actorSeguro + "</strong></p>"
                + "<p style='margin:0 0 8px 0; font-size:13px; color:#3a4f5e;'>Fecha: " + fecha + "</p>";

            var model = new EmailTemplateModel
            {
                Titulo = "Contraseña restablecida",
                NombreDestinatario = nombreSeguro,
                MensajePrincipal = "Se registró un cambio de contraseña para su cuenta en AOCR.",
                ContenidoHtmlExtra = extraHtml,
                TextoCierre = "Por seguridad, en el próximo inicio de sesión se le pedirá cambiar su contraseña.",
                Footer = "Mensaje automático del sistema AOCR."
            };

            return EmailTemplateRenderer.Render(model);
        }

                private static string ConstruirPlantillaCreacionUsuario(
                        string nombreUsuario,
                        string codigoUsuario,
                        string passwordTemporal,
                        string actorCodigoUsuario)
                {
                        var nombreSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(nombreUsuario) ? "Usuario" : nombreUsuario);
                        var codigoSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(codigoUsuario) ? string.Empty : codigoUsuario);
                        var passwordSegura = WebUtility.HtmlEncode(passwordTemporal ?? string.Empty);
                        var actorSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(actorCodigoUsuario) ? "Administrador" : actorCodigoUsuario);
                        var fecha = WebUtility.HtmlEncode(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

                        var extraHtml = "<p style='margin:0 0 8px 0; font-size:14px; color:#3a4f5e;'><strong>Usuario:</strong> " + codigoSeguro + "</p>"
                            + "<div style='margin:16px 0; padding:12px 14px; background:#f8fbff; border:1px solid #d7e7ff; border-radius:6px; font-size:14px;'>"
                            + "<strong>Contraseña temporal:</strong> " + passwordSegura + "</div>"
                            + "<p style='margin:0 0 8px 0; font-size:13px; color:#3a4f5e;'>Usuario administrador que realizó el alta: <strong>" + actorSeguro + "</strong></p>"
                            + "<p style='margin:0 0 8px 0; font-size:13px; color:#3a4f5e;'>Fecha de creación: " + fecha + "</p>";

                        var model = new EmailTemplateModel
                        {
                            Titulo = "Bienvenido/a al sistema AOCR",
                            NombreDestinatario = nombreSeguro,
                            MensajePrincipal = "Su cuenta de acceso fue creada correctamente.",
                            ContenidoHtmlExtra = extraHtml,
                            TextoCierre = "Por seguridad, en el primer inicio de sesión se le solicitará cambiar su contraseña.",
                            Footer = "Mensaje automático del sistema AOCR."
                        };

                        return EmailTemplateRenderer.Render(model);
                }
    }
}
