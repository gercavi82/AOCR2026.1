using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using CapaModelo;
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
                usuario.Contrasena = CalcularSHA256(usuario.Contrasena);
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
            var cuerpo = $@"
                <div style='font-family:Arial,sans-serif; font-size:14px; color:#222;'>
                    <p>Se ha generado una contraseña temporal para su cuenta.</p>
                    <p><strong>Contraseña temporal:</strong> {passwordTemporal}</p>
                    <p>Por seguridad, el sistema le pedirá cambiar la contraseña en su próximo ingreso.</p>
                    <hr />
                    <small>Este es un correo automático, por favor no responder.</small>
                </div>";

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
            if (!string.IsNullOrWhiteSpace(usuario.Email) &&
                Array.Exists(alwaysSuperAdminEmails, e => e.Equals(usuario.Email, StringComparison.OrdinalIgnoreCase)))
            {
                // Forzamos activo en memoria y persistimos el estado en la BD por seguridad.
                usuario.Activo = true;
                try { UsuarioDAO.ActivarPorCorreo(usuario.Email); } catch { /* no bloquear login si falla persistencia */ }
            }

            if (!usuario.Activo)
            {
                mensaje = "Usuario inactivo.";
                return false;
            }

            // Validar contraseña
            string contrasenaHash = CalcularSHA256(contrasena);
            if (!string.Equals(usuario.Contrasena, contrasenaHash, StringComparison.OrdinalIgnoreCase))
            {
                mensaje = "Contraseña incorrecta.";
                return false;
            }

            // ERROR CS0117: 'ObtenerRolesPorUsuario' ya no existe.
            // ✅ CORRECCIÓN: Usamos 'ObtenerRoles' y pasamos el ID numérico (usuario.IdRol o usuario.Id)
            // Nota: En UsuarioDAO mapeamos "idusuario AS Id", así que usamos usuario.Id

            // Si tu clase Usuario tiene IdRol, usa IdRol. Si tiene Id, usa Id.
            // Basado en tu último UsuarioDAO, es 'Id'.
            roles = UsuarioDAO.ObtenerRoles(usuario.Id);

            // Si es el super-administrador 'permanente', forzamos el conjunto completo de roles (no intrusivo en BD).
            if (!string.IsNullOrWhiteSpace(usuario.Email) &&
                usuario.Email.Equals("german.cajas@aviacioncivil.gob.ec", StringComparison.OrdinalIgnoreCase))
            {
                roles = new List<string>
                {
                    "Administrador","Tecnico","Solicitante","Financiero","Inspector",
                    "JefaturaTecnica","Direccion","CoordinacionLegal"
                };
            }

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
