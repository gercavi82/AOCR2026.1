using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo.RT;
using CapaModelo.Common;
using CapaNegocio.Helpers;

namespace CapaNegocio.Services
{
    /// <summary>
    /// AC-01: Servicio orquestador para el flujo de designación de Representantes Técnicos (RT).
    /// Garantiza la devolución transaccional, liberación de correos de postulaciones devueltas,
    /// protección estricta de cuentas de usuarios activos y notificaciones idempotentes.
    /// </summary>
    public class RtDesignacionFlujoService
    {
        private readonly AocrFlujoService _flujoService;

        public RtDesignacionFlujoService()
            : this(new AocrFlujoService())
        {
        }

        public RtDesignacionFlujoService(AocrFlujoService flujoService)
        {
            _flujoService = flujoService ?? new AocrFlujoService();
        }

        /// <summary>
        /// Ejecuta la devolución de la designación provisional de RT.
        /// </summary>
        /// <param name="usuarioId">ID del postulante provisional en la tabla usuario.</param>
        /// <param name="coordinadorUsuarioId">ID del usuario autenticado que solicita la devolución.</param>
        /// <param name="rolSesion">Rol activo del usuario en la sesión.</param>
        /// <param name="observacion">Motivo/observación de la devolución para subsanación.</param>
        public ResultadoDevolucionRT DevolverDesignacion(
            int usuarioId,
            int coordinadorUsuarioId,
            string rolSesion,
            string observacion)
        {
            var resultado = new ResultadoDevolucionRT
            {
                UsuarioId = usuarioId
            };

            // 1. REGLA 7: El Administrador no puede devolver designaciones operativas
            if (string.Equals(rolSesion, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                resultado.Exitoso = false;
                resultado.Mensaje = "El rol Administrador no tiene autorización para devolver designaciones operativas de RT.";
                LogBL.RegistrarAdvertencia(
                    "[AC-01] Intento no autorizado del Administrador para devolver designación RT. UsuarioId=" + usuarioId,
                    "RtDesignacionFlujoService",
                    coordinadorUsuarioId);
                return resultado;
            }

            // Validar que el rol sea de Coordinación o Dirección institucional
            var rolNormalizado = AocrFlujoService.NormalizarRolFlujo(rolSesion);
            var esRolAutorizado = string.Equals(rolNormalizado, "Coordinacion", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(rolNormalizado, "DireccionJefaturaTecnica", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(rolNormalizado, "DIRDAC", StringComparison.OrdinalIgnoreCase);

            if (!esRolAutorizado)
            {
                resultado.Exitoso = false;
                resultado.Mensaje = "No tiene permisos suficientes para devolver designaciones RT.";
                return resultado;
            }

            var observacionNormalizada = (observacion ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(observacionNormalizada))
            {
                resultado.Exitoso = false;
                resultado.Mensaje = "Debe ingresar una observación para devolver la designación RT.";
                return resultado;
            }

            // 2. Ejecución transaccional en DAO
            resultado = UsuarioDAO.DevolverDesignacionRTTransaccional(
                usuarioId,
                coordinadorUsuarioId,
                observacionNormalizada);

            if (!resultado.Exitoso)
            {
                LogBL.RegistrarAdvertencia(
                    string.Format("[AC-01] Devolución fallida para UsuarioId={0}: {1}", usuarioId, resultado.Mensaje),
                    "RtDesignacionFlujoService",
                    coordinadorUsuarioId);
                return resultado;
            }

            // 3. Notificación al postulante si no estaba devuelto previamente (evita doble correo en doble clic)
            if (!resultado.YaEstabaDevuelto && !string.IsNullOrWhiteSpace(resultado.CorreoOriginal))
            {
                try
                {
                    EnviarNotificacionDevolucion(resultado, coordinadorUsuarioId, observacionNormalizada);
                }
                catch (Exception exNotif)
                {
                    // La falla de notificación no debe revertir la transacción confirmada
                    LogBL.RegistrarError(
                        string.Format("[AC-01] Error al enviar notificación de devolución RT a {0}.", resultado.CorreoOriginal),
                        exNotif.ToString(),
                        "RtDesignacionFlujoService");
                }
            }

            return resultado;
        }

        private void EnviarNotificacionDevolucion(
            ResultadoDevolucionRT resultado,
            int coordinadorUsuarioId,
            string observacion)
        {
            var asunto = RtCorreoTextoHelper.GetAsuntoDevolucionRt();
            var textoDevolucion = RtCorreoTextoHelper.GetTextoDevolucionRt(new Dictionary<string, string>
            {
                { "NOMBRE", resultado.NombreCompleto ?? string.Empty },
                { "USUARIO", resultado.CodigoUsuario ?? string.Empty },
                { "MOTIVO", observacion }
            });

            var cuerpo = EmailTemplateRenderer.Render(new EmailTemplateModel
            {
                Titulo = "Designación RT devuelta",
                NombreDestinatario = resultado.NombreCompleto,
                MensajePrincipal = textoDevolucion,
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Usuario", resultado.CodigoUsuario ?? string.Empty),
                    new EmailFieldItem("Estado", "Devuelta para corrección"),
                    new EmailFieldItem("Observación", observacion),
                    new EmailFieldItem("Correo liberado", resultado.CorreoOriginal)
                },
                Observaciones = observacion,
                TextoCierre = "Su correo ha quedado disponible. Puede corregir sus documentos y postularse nuevamente en el sistema.",
                Footer = "Este es un correo automático, por favor no responder."
            });

            var servicioCorreo = new EnviarCorreo();
            servicioCorreo.enviaMensajeCorreo(resultado.CorreoOriginal, asunto, cuerpo);

            LogBL.RegistrarInfo(
                string.Format("[AC-01] Notificación de devolución RT enviada a {0}. PostulanteId={1}",
                    resultado.CorreoOriginal, resultado.UsuarioId),
                "RtDesignacionFlujoService",
                coordinadorUsuarioId);
        }
    }
}
