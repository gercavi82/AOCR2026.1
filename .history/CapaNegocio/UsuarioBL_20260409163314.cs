using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using CapaModelo;
using CapaModelo.Common;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaNegocio.Helpers;

namespace CapaNegocio
{
    public static class UsuarioBL
    {
        // ================================
        // 1. Crear Usuario
        // ================================
        public static int RegistrarUsuario(Usuario usuario, out string mensaje)
        {
            try
            {
                usuario.Contrasena = PasswordHelper.HashPassword(usuario.Contrasena);
                int id = UsuarioDAO.Crear(usuario);
                mensaje = "Usuario registrado exitosamente.";
                return id;
            }
            catch (Exception ex)
            {
                mensaje = $"Error al registrar: {ex.Message}";
                return 0;
            }
        }

        // ================================
        // 2. Restablecer Contraseña (Solución Error CS7036)
        // ================================
        public static bool RestablecerContrasenaPorEmail(string email, out string mensaje)
        {
            // Generar contraseña temporal y guardarla (hash)
            string passwordTemporal = PasswordHelper.GenerarPasswordAleatoria(10);
            string passwordHash = PasswordHelper.HashPassword(passwordTemporal);

            bool ok = UsuarioDAO.RestablecerContrasena(email, passwordHash, out mensaje);
            if (!ok)
            {
                return false;
            }

            // Enviar correo con la contraseña temporal
            var asunto = "Recuperación de contraseña - Sistema AOCR";
            var model = new EmailTemplateModel
            {
                Titulo = "Recuperacion de contrasena",
                MensajePrincipal = "Se ha generado una contrasena temporal para su cuenta.",
                ContenidoHtmlExtra = "<div style='margin:16px 0; padding:12px 14px; background:#f8fbff; border:1px solid #d7e7ff; border-radius:6px; font-size:14px;'><strong>Contrasena temporal:</strong> "
                    + System.Net.WebUtility.HtmlEncode(passwordTemporal) + "</div>",
                TextoCierre = "Por seguridad, el sistema le pedira cambiar la contrasena en su proximo ingreso.",
                Footer = "Este es un correo automatico, por favor no responder."
            };
            var cuerpo = EmailTemplateRenderer.Render(model);

            bool correoEnviado = false;
            try
            {
                var servicioCorreo = new EnviarCorreo();
                correoEnviado = servicioCorreo.enviaMensajeCorreo(email, asunto, cuerpo);
            }
            catch
            {
                correoEnviado = false;
            }

            if (!correoEnviado)
            {
                mensaje = "Contraseña actualizada, pero no se pudo enviar el correo. Verifique configuración SMTP.";
            }
            else
            {
                mensaje = "Se envió una contraseña temporal a su correo.";
            }

            return true;
        }

        // ================================
        // 2. Aceptación de usuario con clave temporal
        // ================================
        public static bool NotificarAceptacionConClaveTemporal(string email, string nombreCompleto, string codigoUsuario, string nombreCompania, out string mensaje)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                mensaje = "Correo del usuario vacío.";
                return false;
            }

            string passwordTemporal = PasswordHelper.GenerarPasswordAleatoria(10);
            string passwordHash = PasswordHelper.HashPassword(passwordTemporal);

            bool ok = UsuarioDAO.RestablecerContrasena(email, passwordHash, out mensaje);
            if (!ok)
            {
                return false;
            }

            var asunto = "Designación RT aprobada - Sus credenciales de acceso - Sistema AOCR";
            var companiaTexto = string.IsNullOrWhiteSpace(nombreCompania)
                ? "SU COMPAÑÍA"
                : nombreCompania.Trim().ToUpperInvariant();

            var textoOficialAprobacion =
                "NOS COMPLACE INFORMARLE QUE SU DESIGNACIÓN COMO RESPONSABLE TÉCNICO (RT) DE LA COMPAÑÍA " +
                companiaTexto +
                " HA SIDO APROBADA POR LA DGAC.";
            var textoOficialContinuidad =
                "EN TAL VIRTUD CON SU USUARIO PODRÁ CONTINUAR CON LOS TRAMITES EN EL SISTEMA SIMPLIFICADO AOCR";

            var extraHtml = "<p style='margin:0 0 12px 0; font-size:14px; color:#3a4f5e;'><strong>"
                + HttpUtility.HtmlEncode(textoOficialAprobacion) + "</strong></p>"
                + "<p style='margin:0 0 12px 0; font-size:14px; color:#3a4f5e;'><strong>"
                + HttpUtility.HtmlEncode(textoOficialContinuidad) + "</strong></p>"
                + "<p style='margin:0 0 8px 0; font-size:14px; color:#3a4f5e;'>A continuacion sus credenciales de acceso al <strong>Sistema AOCR</strong>:</p>"
                + "<div style='margin:16px 0; padding:12px 14px; background:#f8fbff; border:1px solid #d7e7ff; border-radius:6px; font-size:14px;'>"
                + "<strong>Usuario:</strong> " + System.Net.WebUtility.HtmlEncode(codigoUsuario) + "<br/>"
                + "<strong>Contrasena temporal:</strong> " + System.Net.WebUtility.HtmlEncode(passwordTemporal)
                + "</div>";

            var model = new EmailTemplateModel
            {
                Titulo = "Designacion RT aprobada",
                NombreDestinatario = nombreCompleto,
                ContenidoHtmlExtra = extraHtml,
                TextoCierre = "Por seguridad, el sistema le pedira cambiar la contrasena en su primer ingreso. Si usted no solicito este registro, por favor comuniquese con la DGAC de inmediato.",
                Footer = "Este es un correo automatico, por favor no responder."
            };
            var cuerpo = EmailTemplateRenderer.Render(model);

            bool correoEnviado;
            try
            {
                var servicioCorreo = new EnviarCorreo();
                correoEnviado = servicioCorreo.enviaMensajeCorreo(email, asunto, cuerpo);
            }
            catch
            {
                correoEnviado = false;
            }

            if (!correoEnviado)
            {
                mensaje = "Usuario aceptado y clave generada, pero no se pudo enviar el correo.";
                return false;
            }

            mensaje = "Usuario aceptado y correo enviado con clave temporal.";
            return true;
        }

        // ================================
        // 3. Autenticación (Solución Error CS0117)
        // ================================
        public static bool Autenticar(
            string nombreUsuario,
            string contrasena,
            out Usuario usuario,
            out List<string> roles,
            out string mensaje,
            bool actualizarUltimaConexion = true)
        {
            usuario = UsuarioDAO.ObtenerPorNombreUsuario(nombreUsuario);
            roles = new List<string>();
            mensaje = "";

            if (usuario == null)
            {
                mensaje = "Usuario no encontrado.";
                return false;
            }

            // == Excepciones/usuarios especiales ==
            // Este usuario debe estar siempre activo y actuar como 'superadministrador'.
            var alwaysSuperAdminEmails = new[] { "german.cajas@aviacioncivil.gob.ec" };
            var usuarioEmail = usuario?.Email; // copiar a local para evitar capturar el parametro out en lambdas
            if (!string.IsNullOrWhiteSpace(usuarioEmail) &&
                Array.Exists(alwaysSuperAdminEmails, e => e.Equals(usuarioEmail, StringComparison.OrdinalIgnoreCase)))
            {
                // Forzamos activo en memoria y persistimos el estado en la BD por seguridad.
                usuario.Activo = true;
                try { UsuarioDAO.ActivarPorCorreo(usuarioEmail); } catch { /* no bloquear login si falla persistencia */ }
            }

            if (!usuario.Activo)
            {
                mensaje = "Usuario inactivo.";
                return false;
            }

            // Validar contraseña (soporta hash PBKDF2 y legacy SHA256).
            if (!PasswordHelper.VerifyPassword(contrasena, usuario.Contrasena))
            {
                mensaje = "Contraseña incorrecta.";
                return false;
            }

            // Migracion transparente: si el hash legacy aun existe, se actualiza al formato nuevo.
            if (PasswordHelper.NeedsRehash(usuario.Contrasena))
            {
                try
                {
                    string msgInterno;
                    UsuarioDAO.ActualizarContrasena(usuario.Id, PasswordHelper.HashPassword(contrasena), out msgInterno);
                }
                catch
                {
                    // No bloquear login si falla la migracion de hash.
                }
            }

            // ERROR CS0117: 'ObtenerRolesPorUsuario' ya no existe.
            // ✅ CORRECCIÓN: Usamos 'ObtenerRoles' y pasamos el ID numérico (usuario.IdRol o usuario.Id)
            // Nota: En UsuarioDAO mapeamos "idusuario AS Id", así que usamos usuario.Id

            // Si tu clase Usuario tiene IdRol, usa IdRol. Si tiene Id, usa Id.
            // Basado en tu último UsuarioDAO, es 'Id'.
            roles = UsuarioDAO.ObtenerRoles(usuario.Id);

            // Importante: no forzar roles en memoria. El menú y la autorización deben reflejar solo roles en BD.

            if (roles == null || roles.Count == 0)
            {
                mensaje = "El usuario no tiene roles asignados.";
                // return false; // Descomentar si es obligatorio tener rol
            }

            // Actualizar última conexión (opcional)
            if (actualizarUltimaConexion)
            {
                UsuarioDAO.ActualizarUltimaConexion(usuario.Id);
            }

            mensaje = "Inicio de sesión exitoso.";
            return true;
        }

        // ================================
        // 4. Listar Técnicos
        // ================================
        public static List<Usuario> ListarTecnicos()
        {
            try
            {
                return UsuarioDAO.ListarPorRol("TECNICO");
            }
            catch
            {
                return new List<Usuario>();
            }
        }

        // ================================
        // Utilidad: Hash SHA-256
        // ================================
        private static string CalcularSHA256(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(texto);
                byte[] hash = sha256.ComputeHash(bytes);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));

                return sb.ToString();
            }
        }
        // ================================
        // 5. Obtener Inspectores / Técnicos
        // ================================
        public static List<Usuario> ObtenerInspectores()
        {
            try
            {
                // Rol según tu base de datos
                return UsuarioDAO.ListarPorRol("TECNICO");
            }
            catch
            {
                return new List<Usuario>();
            }
        }


    }
}
