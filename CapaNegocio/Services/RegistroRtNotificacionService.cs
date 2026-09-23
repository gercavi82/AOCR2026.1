using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class RegistroRtNotificacionService
    {
        private static readonly string[] RolesCoordinacion =
        {
            "Coordinacion", "Coordinador", "CoordinadorInspecciones",
            "CoordinacionLegal", "CoordinadorLegal", "JefaturaTecnica"
        };
        private readonly Func<string, List<Usuario>> _usuariosPorRol;
        private readonly Func<IEnumerable<string>> _correosInstitucionales;
        private readonly Func<Notificacion, bool> _notificar;
        private readonly Action<EmailQueueItem> _encolar;
        private readonly ILoggingService _logger;

        public RegistroRtNotificacionService() : this(
            UsuarioDAO.ListarPorRol,
            () => new CorreoInstitucionalService()
                .ObtenerDestinatariosPorArea(CorreoInstitucionalService.CoordinadorAocr)?.ObtenerTodosLosCorreos(),
            NotificacionDAO.Insertar,
            item => new EmailQueueService().EncolarAsync(item).GetAwaiter().GetResult(),
            LoggingServiceFactory.Create())
        {
        }

        public RegistroRtNotificacionService(Func<string, List<Usuario>> usuariosPorRol,
            Func<IEnumerable<string>> correosInstitucionales, Func<Notificacion, bool> notificar,
            Action<EmailQueueItem> encolar, ILoggingService logger)
        {
            _usuariosPorRol = usuariosPorRol;
            _correosInstitucionales = correosInstitucionales;
            _notificar = notificar;
            _encolar = encolar;
            _logger = logger;
        }

        // Ejecutar solo despues de persistir la designacion pendiente.
        // Cada canal se procesa por separado: SMTP no condiciona el aviso en la campana.
        public void NotificarRegistroPendiente(int usuarioRtId, string nombreRt, string urlRevision)
        {
            if (usuarioRtId <= 0) throw new ArgumentOutOfRangeException(nameof(usuarioRtId));
            var contexto = new LogContext { ErrorCode = "RT_NOTIFICACION_COORDINADOR" };
            var coordinadores = new Dictionary<int, Usuario>();
            foreach (var rol in RolesCoordinacion)
            {
                try
                {
                    foreach (var usuario in _usuariosPorRol(rol) ?? new List<Usuario>())
                    {
                        if (usuario != null && usuario.Id > 0 && usuario.Id != usuarioRtId)
                            coordinadores[usuario.Id] = usuario;
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, contexto); }
            }

            const string titulo = "Nuevo RT pendiente de aprobación";
            var mensaje = "El RT " + (nombreRt ?? string.Empty).Trim()
                + " ha presentado su designación y está pendiente de aceptación y aprobación por Coordinación.";
            var internas = 0;
            foreach (var coordinador in coordinadores.Values)
            {
                try
                {
                    if (_notificar(new Notificacion
                    {
                        CodigoUsuario = coordinador.Id, Titulo = titulo, Mensaje = mensaje,
                        Tipo = "INFO", Url = urlRevision, Modulo = "RT",
                        EntidadId = usuarioRtId, TipoEntidad = "USUARIO_RT",
                        FechaCreacion = DateTime.Now, Leida = false
                    })) internas++;
                    else _logger.LogWarning("No se pudo guardar el aviso RT para Coordinación.", contexto);
                }
                catch (Exception ex) { _logger.LogError(ex, contexto); }
            }

            IEnumerable<string> institucionales = null;
            try { institucionales = _correosInstitucionales()?.ToList(); }
            catch (Exception ex) { _logger.LogError(ex, contexto); }
            var correos = (institucionales ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c) && !c.Trim().Equals("coordinador.aocr@aviacioncivil.gob.ec", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (correos.Count == 0)
            {
                _logger.LogWarning("Sin correo institucional COORDINADOR_AOCR válido; se usaran los correos de los coordinadores activos.", contexto);
                correos = coordinadores.Values.Select(u => u.Email)
                    .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            var evento = Guid.NewGuid().ToString("N");
            var encolados = 0;
            foreach (var correo in correos)
            {
                try
                {
                    _encolar(new EmailQueueItem
                    {
                        Para = correo, ParaNombre = "Coordinador/a AOCR", Asunto = titulo + " - Sistema AOCR",
                        Cuerpo = "<p>Estimado/a Coordinador/a:</p><p>" + HttpUtility.HtmlEncode(mensaje)
                            + "</p><p>Ingrese al Sistema AOCR, sección Gestión de Usuarios RT / Revisar designaciones, para revisar la solicitud y continuar el proceso de aprobación.</p>",
                        EsHtml = true, MaxIntentos = 3, TipoNotificacion = "RT_REGISTRO_PENDIENTE",
                        EventKey = "RT_REGISTRO:" + usuarioRtId + ":" + evento + ":" + correo.ToLowerInvariant(),
                        Remitente = AocrEmailService.CorreoNoReply, AliasRemitente = "DGAC - Sistema AOCR"
                    });
                    encolados++;
                }
                catch (Exception ex) { _logger.LogError(ex, contexto); }
            }
            _logger.LogInfo("[RT_REGISTRO_NOTIFICADO] UsuarioRtId=" + usuarioRtId
                + "; AvisosInternos=" + internas + "; CorreosEncolados=" + encolados, contexto);
            if (coordinadores.Count == 0 || correos.Count == 0)
                _logger.LogWarning("Faltan destinatarios para uno o ambos canales del registro RT.", contexto);
        }
    }
}
