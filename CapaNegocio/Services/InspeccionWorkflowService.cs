using System;
using System.Collections.Generic;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Orquestador central del workflow de inspeccion.
    /// Reutiliza BL/DAO existentes y opera como capa incremental sin romper rutas ni controladores actuales.
    /// </summary>
    public class InspeccionWorkflowService
    {
        private readonly InspeccionDAO _inspeccionDAO;
        private readonly InspeccionBL _inspeccionBL;
        private readonly HallazgoBL _hallazgoBL;
        private readonly InspeccionInformeDAO _informeDAO;
        private readonly SolicitudAOCRDAO _solicitudDAO;
        private readonly AuditoriaService _auditoriaService;
        private readonly ValidacionDocumentalService _validacionDocumentalService;
        private readonly IntegracionInspeccionOrService _integracionInspeccionOrService;
        private readonly InspeccionCorreoService _inspeccionCorreoService;

        public InspeccionWorkflowService()
        {
            _inspeccionDAO = new InspeccionDAO();
            _inspeccionBL = new InspeccionBL();
            _hallazgoBL = new HallazgoBL();
            _informeDAO = new InspeccionInformeDAO();
            _solicitudDAO = new SolicitudAOCRDAO();
            _auditoriaService = new AuditoriaService();
            _validacionDocumentalService = new ValidacionDocumentalService();
            _integracionInspeccionOrService = new IntegracionInspeccionOrService();
            _inspeccionCorreoService = new InspeccionCorreoService();
        }

        public ResultadoOperacion RegistrarResultadoInspeccion(int inspeccionId, string resultado, string observacion, int usuarioId, string usuarioNombre)
        {
            return EvaluarInspeccion(inspeccionId, resultado, observacion, usuarioId, usuarioNombre);
        }

        public ResultadoOperacion EvaluarInspeccion(int inspeccionId, string resultado, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                if (inspeccionId <= 0)
                {
                    return ResultadoOperacion.Error("Inspección inválida");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                {
                    return ResultadoOperacion.Error("Inspección no encontrada");
                }

                var informe = _informeDAO.ObtenerUltimoPorInspeccion(inspeccionId);
                if (informe == null)
                {
                    return ResultadoOperacion.Error("No se puede registrar el resultado sin informe técnico.");
                }

                if (!informe.Finalizado)
                {
                    return ResultadoOperacion.Error("No se puede registrar el resultado mientras el informe técnico no esté finalizado.");
                }

                if (!informe.FirmadoInspector)
                {
                    return ResultadoOperacion.Error("No se puede registrar el resultado hasta que el informe técnico esté firmado por el inspector.");
                }

                var resultadoInformeNormalizado = NormalizarResultadoInformeTecnico(informe.Resultado);
                if (resultadoInformeNormalizado != "SATISFACTORIO" && resultadoInformeNormalizado != "INSATISFACTORIO")
                {
                    return ResultadoOperacion.Error("No se puede registrar el resultado porque el Informe Técnico no tiene un resultado satisfactorio o insatisfactorio válido.");
                }

                var resultadoSolicitadoNormalizado = NormalizarResultadoInformeTecnico(resultado);
                if (!string.IsNullOrWhiteSpace(resultadoSolicitadoNormalizado)
                    && !string.Equals(resultadoSolicitadoNormalizado, resultadoInformeNormalizado, StringComparison.OrdinalIgnoreCase))
                {
                    return ResultadoOperacion.Error("El resultado de inspección debe coincidir con el resultado del Informe Técnico firmado.");
                }

                var esSatisfactorio = string.Equals(resultadoInformeNormalizado, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase);
                var tipoResultadoInsatisfactorio = NormalizarTipoResultadoInsatisfactorio(informe.TipoResultadoInsatisfactorio);

                if (!esSatisfactorio && string.IsNullOrWhiteSpace(tipoResultadoInsatisfactorio))
                {
                    return ResultadoOperacion.Error("El resultado insatisfactorio debe indicar si requiere nueva inspección o subsanación documental.");
                }

                if (!esSatisfactorio)
                {
                    // Si el informe pertenece a una reevaluación, materializa una NC de
                    // nuevo ciclo (idempotente) y conserva intacta la NC antecedente.
                    new ReevaluacionInspeccionService().CrearNcNuevoCicloInsatisfactorio(
                        inspeccionId, informe.CodigoInforme, usuarioId);

                    var resultadoNc = AsegurarNoConformidadDesdeInforme(inspeccionId, informe, usuarioId, usuarioNombre);
                    if (!resultadoNc.Exitoso)
                    {
                        return resultadoNc;
                    }

                    observacion = CombinarObservacionResultadoInsatisfactorio(observacion, tipoResultadoInsatisfactorio);
                }

                var estadoDestino = esSatisfactorio
                    ? EstadosInspeccion.RESULTADO_SATISFACTORIO
                    : EstadosInspeccion.RESULTADO_NO_SATISFACTORIO;

                if (esSatisfactorio)
                {
                    // En una reevaluación, el informe firmado y con hash es la evidencia que
                    // cierra formalmente la NC. El cierre ocurre antes de validar bloqueos.
                    new ReevaluacionInspeccionService().CerrarNcConInformeSatisfactorio(
                        inspeccionId,
                        informe.CodigoInforme,
                        usuarioId,
                        string.IsNullOrWhiteSpace(observacion)
                            ? "Subsanación verificada mediante Informe Técnico satisfactorio."
                            : observacion);

                    var noConformidadesAbiertas = ContarNoConformidadesAbiertas(inspeccionId);
                    if (noConformidadesAbiertas > 0)
                    {
                        return ResultadoOperacion.Error(
                            "No se puede registrar resultado satisfactorio mientras existan no conformidades abiertas (" +
                            noConformidadesAbiertas + ").");
                    }
                }

                inspeccion.ResultadoEvaluacion = esSatisfactorio
                    ? EstadosInspeccion.RESULTADO_SATISFACTORIO
                    : EstadosInspeccion.RESULTADO_NO_SATISFACTORIO;
                inspeccion.Resultado = esSatisfactorio ? "APROBADO" : "RECHAZADO";
                inspeccion.EstadoDocumental = esSatisfactorio ? "ACEPTADA" : "OBSERVACION_DOCUMENTAL";
                if (!string.IsNullOrWhiteSpace(observacion))
                {
                    inspeccion.ObservacionesGenerales = observacion;
                }

                if (!_inspeccionBL.Actualizar(inspeccion, usuarioId))
                {
                    return ResultadoOperacion.Error("No se pudo actualizar la evaluación de inspección");
                }

                var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                if (!string.Equals(estadoActual, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase) &&
                    EstadosInspeccion.EsTransicionValida(estadoActual, EstadosInspeccion.INFORME_ELABORADO))
                {
                    _inspeccionBL.CambiarEstado(
                        inspeccionId,
                        EstadosInspeccion.INFORME_ELABORADO,
                        usuarioId,
                        "Resultado registrado con informe asociado.",
                        usuarioNombre,
                        "RESULTADO_INSPECCION");

                    estadoActual = EstadosInspeccion.INFORME_ELABORADO;
                }

                var cambioEstado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    estadoDestino,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    esSatisfactorio ? "EVALUACION_APROBADA" : "EVALUACION_CON_NC");

                if (!cambioEstado.Exitoso)
                {
                    return cambioEstado;
                }

                EmitirNotificacionEvento(inspeccionId, esSatisfactorio ? "APROBACION_INSPECCION" : "NC_GENERADAS", observacion);

                return ResultadoOperacion.Ok(
                    null,
                    esSatisfactorio
                        ? "Resultado de inspección registrado correctamente"
                        : ConstruirMensajeResultadoInsatisfactorio(tipoResultadoInsatisfactorio));
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al evaluar inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion SubsanarInspeccion(int inspeccionId, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                if (inspeccionId <= 0)
                {
                    return ResultadoOperacion.Error("Inspección inválida");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                {
                    return ResultadoOperacion.Error("Inspección no encontrada");
                }

                inspeccion.EstadoDocumental = "SUBSANADA";
                if (!string.IsNullOrWhiteSpace(observacion))
                {
                    inspeccion.Comentarios = string.IsNullOrWhiteSpace(inspeccion.Comentarios)
                        ? observacion
                        : (inspeccion.Comentarios + " | " + observacion);
                }

                if (!_inspeccionBL.Actualizar(inspeccion, usuarioId))
                {
                    return ResultadoOperacion.Error("No se pudo actualizar la información de subsanación");
                }

                var resultado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    EstadosInspeccion.SUBSANADA,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    "SUBSANACION");

                if (resultado.Exitoso)
                {
                    EmitirNotificacionEvento(inspeccionId, "DOCUMENTOS_SUBSANADOS", observacion);
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al subsanar inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion RevalidarInspeccion(int inspeccionId, bool aprobada, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                var estadoDestino = aprobada
                    ? EstadosInspeccion.EN_INSPECCION
                    : EstadosInspeccion.OBSERVADA;

                var resultado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    estadoDestino,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    "REVALIDACION");

                if (resultado.Exitoso)
                {
                    EmitirNotificacionEvento(inspeccionId, aprobada ? "REVALIDACION_OK" : "REVALIDACION_RECHAZADA", observacion);
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al revalidar inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion SolicitarNuevaInspeccion(int inspeccionId, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                var resultado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    EstadosInspeccion.OBSERVADA,
                    usuarioId,
                    usuarioNombre,
                    string.IsNullOrWhiteSpace(observacion) ? "Se solicita una nueva inspección." : observacion,
                    "NUEVA_INSPECCION");

                if (resultado.Exitoso)
                {
                    EmitirNotificacionEvento(inspeccionId, "DEVOLUCION_INSPECCION", observacion);
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al solicitar nueva inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion AprobarNoConformidadParaNuevaInspeccion(int inspeccionId, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                Inspeccion inspeccion;
                InspeccionInformeTecnico informe;
                var validacion = ValidarAprobacionNoConformidad(inspeccionId, "CON_INSPECCION", out inspeccion, out informe);
                if (!validacion.Exitoso)
                {
                    return validacion;
                }

                var observacionFinal = ConstruirObservacionAprobacionNoConformidad(
                    observacion,
                    "Coordinación aprobó la NC y solicita nueva inspección.",
                    "CON_INSPECCION");

                var resultado = SolicitarNuevaInspeccion(inspeccionId, observacionFinal, usuarioId, usuarioNombre);
                if (!resultado.Exitoso)
                {
                    return resultado;
                }

                _auditoriaService.RegistrarAccionInspeccion(
                    inspeccionId,
                    "APROBACION_NC_NUEVA_INSPECCION",
                    usuarioId,
                    usuarioNombre,
                    observacionFinal,
                    null,
                    "TipoResultadoInsatisfactorio=CON_INSPECCION");

                return ResultadoOperacion.Ok(null, "No conformidad aprobada. Se habilitó formalmente la ruta de nueva inspección.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al aprobar la no conformidad para nueva inspección: " + ex.Message);
            }
        }

        public ResultadoOperacion AprobarNoConformidadParaSubsanacionDocumental(int inspeccionId, string observacion, int usuarioId, string usuarioNombre)
        {
            try
            {
                Inspeccion inspeccion;
                InspeccionInformeTecnico informe;
                var validacion = ValidarAprobacionNoConformidad(inspeccionId, "SIN_INSPECCION", out inspeccion, out informe);
                if (!validacion.Exitoso)
                {
                    return validacion;
                }

                var observacionFinal = ConstruirObservacionAprobacionNoConformidad(
                    observacion,
                    "Coordinación aprobó la NC y habilitó la subsanación documental del RT.",
                    "SIN_INSPECCION");

                inspeccion.EstadoDocumental = "OBSERVACION_DOCUMENTAL";
                if (!string.IsNullOrWhiteSpace(observacionFinal))
                {
                    inspeccion.Comentarios = string.IsNullOrWhiteSpace(inspeccion.Comentarios)
                        ? observacionFinal
                        : (inspeccion.Comentarios + " | " + observacionFinal);
                }

                if (!_inspeccionBL.Actualizar(inspeccion, usuarioId))
                {
                    return ResultadoOperacion.Error("No se pudo dejar la inspección lista para subsanación documental.");
                }

                var resultado = CambiarEstadoConNotificacion(
                    inspeccionId,
                    EstadosInspeccion.OBSERVADA,
                    usuarioId,
                    usuarioNombre,
                    observacionFinal,
                    "NC_SUBSANACION_DOCUMENTAL");

                if (!resultado.Exitoso)
                {
                    return resultado;
                }

                _auditoriaService.RegistrarAccionInspeccion(
                    inspeccionId,
                    "APROBACION_NC_SUBSANACION_DOCUMENTAL",
                    usuarioId,
                    usuarioNombre,
                    observacionFinal,
                    null,
                    "TipoResultadoInsatisfactorio=SIN_INSPECCION");

                EmitirNotificacionEvento(inspeccionId, "NC_GENERADAS", observacionFinal);

                TransicionarSolicitudObservadaParaSubsanacion(inspeccion.CodigoSolicitud, observacionFinal, usuarioId);
                NotificarRtSubsanacionDocumentalHabilitada(inspeccion, observacionFinal);

                return ResultadoOperacion.Ok(null, "No conformidad aprobada. El expediente quedó observado para subsanación documental del RT.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al aprobar la no conformidad para subsanación documental: " + ex.Message);
            }
        }

        public ResultadoOperacion RegistrarNoConformidad(int inspeccionId, string descripcion, string criticidad, int usuarioId, string usuarioNombre)
        {
            try
            {
                if (inspeccionId <= 0)
                {
                    return ResultadoOperacion.Error("Inspección inválida");
                }

                if (string.IsNullOrWhiteSpace(descripcion))
                {
                    return ResultadoOperacion.Error("La descripción de la no conformidad es obligatoria");
                }

                var hallazgo = new Hallazgo
                {
                    CodigoInspeccion = inspeccionId,
                    Descripcion = descripcion,
                    Criticidad = string.IsNullOrWhiteSpace(criticidad) ? "MEDIA" : criticidad,
                    Estado = "ABIERTO"
                };

                var hallazgoId = _hallazgoBL.Crear(hallazgo, usuarioNombre);
                if (hallazgoId <= 0)
                {
                    return ResultadoOperacion.Error("No se pudo registrar la no conformidad");
                }

                _auditoriaService.RegistrarAccionInspeccion(
                    inspeccionId,
                    "REGISTRO_NO_CONFORMIDAD",
                    usuarioId,
                    usuarioNombre,
                    descripcion,
                    null,
                    "Criticidad: " + hallazgo.Criticidad + ", HallazgoId: " + hallazgoId);

                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion != null)
                {
                    var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                    if (EstadosInspeccion.EsTransicionValida(estadoActual, EstadosInspeccion.OBSERVADA))
                    {
                        CambiarEstadoConNotificacion(
                            inspeccionId,
                            EstadosInspeccion.OBSERVADA,
                            usuarioId,
                            usuarioNombre,
                            "No conformidad registrada: " + descripcion,
                            "NC");
                    }
                }

                EmitirNotificacionEvento(inspeccionId, "NC_GENERADAS", descripcion);

                return ResultadoOperacion.Ok(hallazgoId, "No conformidad registrada correctamente");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al registrar no conformidad: " + ex.Message);
            }
        }

        private ResultadoOperacion CambiarEstadoConNotificacion(
            int inspeccionId,
            string estadoDestino,
            int usuarioId,
            string usuarioNombre,
            string observacion,
            string origen)
        {
            if (inspeccionId <= 0)
            {
                return ResultadoOperacion.Error("Inspección inválida");
            }

            try
            {
                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                {
                    return ResultadoOperacion.Error("Inspección no encontrada");
                }

                var estadoAnterior = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                var estadoDestinoNormalizado = EstadosInspeccion.NormalizarEstado(estadoDestino);

                if (string.Equals(estadoDestinoNormalizado, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase))
                {
                    var validacionDocs = _validacionDocumentalService.PuedeAvanzarEtapa(inspeccion.CodigoSolicitud, "APROBACION_INSPECCION");
                    if (!validacionDocs.EsValido)
                    {
                        return ResultadoOperacion.Error("No se puede cerrar la inspección. " + ConstruirMensajeFaltantes(validacionDocs));
                    }

                    var noConformidadesAbiertas = ContarNoConformidadesAbiertas(inspeccionId);
                    if (noConformidadesAbiertas > 0)
                    {
                        return ResultadoOperacion.Error(
                            "No se puede cerrar la inspección mientras existan no conformidades abiertas (" +
                            noConformidadesAbiertas + ").");
                    }
                }

                if (string.Equals(estadoDestinoNormalizado, EstadosInspeccion.RESULTADO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase))
                {
                    var noConformidadesAbiertas = ContarNoConformidadesAbiertas(inspeccionId);
                    if (noConformidadesAbiertas > 0)
                    {
                        return ResultadoOperacion.Error(
                            "No se puede aprobar la inspección mientras existan no conformidades abiertas (" +
                            noConformidadesAbiertas + ").");
                    }
                }

                var ok = _inspeccionBL.CambiarEstado(
                    inspeccionId,
                    estadoDestinoNormalizado,
                    usuarioId,
                    observacion,
                    usuarioNombre,
                    origen);

                if (!ok)
                {
                    return ResultadoOperacion.Error("No se pudo cambiar el estado de la inspección");
                }

                _auditoriaService.RegistrarCambioEstadoInspeccion(
                    inspeccionId,
                    estadoAnterior,
                    estadoDestinoNormalizado,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    null,
                    "Origen: " + (origen ?? "N/A") + ", Core: " + EstadosInspeccionCore.ObtenerEstadoCore(estadoDestinoNormalizado, inspeccion.Resultado));

                NotificarCambioEstado(inspeccion, estadoDestinoNormalizado, observacion);

                if (string.Equals(estadoDestinoNormalizado, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase))
                {
                    EvaluarGeneracionOrSiCorresponde(inspeccion.CodigoSolicitud, usuarioNombre, usuarioId);
                }

                return ResultadoOperacion.Ok(null, "Estado actualizado correctamente");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error(ex.Message);
            }
        }

        private void NotificarCambioEstado(Inspeccion inspeccion, string estadoDestino, string observacion)
        {
            try
            {
                if (inspeccion == null)
                {
                    return;
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                if (solicitud == null)
                {
                    return;
                }

                var titulo = "Actualización de inspección";
                var mensaje = "La inspección #" + inspeccion.CodigoInspeccion + " cambió a estado " + estadoDestino + ".";
                if (!string.IsNullOrWhiteSpace(observacion))
                {
                    mensaje += " Observación: " + observacion;
                }

                var url = "/Inspeccion/Detalle/" + inspeccion.CodigoInspeccion;
                var destinatarios = ConstruirDestinatarios(inspeccion, solicitud, estadoDestino);

                foreach (var codigoUsuario in destinatarios)
                {
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario,
                        titulo,
                        mensaje,
                        TiposNotificacion.INFO,
                        url,
                        TiposNotificacion.CATEGORIA_INSPECCION,
                        inspeccion.CodigoInspeccion,
                        "aocr_tbinspeccion");
                }
            }
            catch
            {
                // No bloquear el flujo principal por fallo de notificación.
            }
        }

        private void EmitirNotificacionEvento(int inspeccionId, string evento, string observacion)
        {
            try
            {
                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                {
                    return;
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                if (solicitud == null)
                {
                    return;
                }

                var titulo = "Evento de inspección";
                var mensaje = "Inspección #" + inspeccion.CodigoInspeccion + ": " + evento + ".";
                var tipo = TiposNotificacion.INFO;

                switch ((evento ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "NC_GENERADAS":
                        titulo = "No conformidades generadas";
                        mensaje = "Se registraron no conformidades en la inspección #" + inspeccion.CodigoInspeccion + ".";
                        tipo = TiposNotificacion.WARNING;
                        break;
                    case "DOCUMENTOS_SUBSANADOS":
                        titulo = "Documentación subsanada";
                        mensaje = "La documentación técnica de la inspección #" + inspeccion.CodigoInspeccion + " fue subsanada y requiere revisión.";
                        tipo = TiposNotificacion.INFO;
                        break;
                    case "DEVOLUCION_INSPECCION":
                        titulo = "Inspección devuelta";
                        mensaje = "La inspección #" + inspeccion.CodigoInspeccion + " fue devuelta para ajustes.";
                        tipo = TiposNotificacion.WARNING;
                        break;
                    case "APROBACION_INSPECCION":
                        titulo = "Inspección aprobada";
                        mensaje = "La inspección #" + inspeccion.CodigoInspeccion + " fue aprobada y continúa al siguiente paso.";
                        tipo = TiposNotificacion.SUCCESS;
                        break;
                    case "REVALIDACION_OK":
                        titulo = "Revalidación satisfactoria";
                        mensaje = "La revalidación de la inspección #" + inspeccion.CodigoInspeccion + " fue satisfactoria.";
                        tipo = TiposNotificacion.SUCCESS;
                        break;
                    case "REVALIDACION_RECHAZADA":
                        titulo = "Revalidación con observaciones";
                        mensaje = "La revalidación de la inspección #" + inspeccion.CodigoInspeccion + " mantiene observaciones pendientes.";
                        tipo = TiposNotificacion.WARNING;
                        break;
                }

                if (!string.IsNullOrWhiteSpace(observacion))
                {
                    mensaje += " Observación: " + observacion;
                }

                var url = "/Inspeccion/Detalle/" + inspeccion.CodigoInspeccion;
                foreach (var codigoUsuario in ConstruirDestinatarios(inspeccion, solicitud, evento))
                {
                    NotificacionBL.EnviarNotificacion(
                        codigoUsuario,
                        titulo,
                        mensaje,
                        tipo,
                        url,
                        TiposNotificacion.CATEGORIA_INSPECCION,
                        inspeccion.CodigoInspeccion,
                        "aocr_tbinspeccion");
                }

                _inspeccionCorreoService.NotificarEvento(inspeccion, solicitud, evento, observacion);
            }
            catch
            {
                // Notificación no bloqueante.
            }
        }

        private HashSet<int> ConstruirDestinatarios(Inspeccion inspeccion, SolicitudAOCR solicitud, string contexto)
        {
            var destinatarios = new HashSet<int>();
            if (inspeccion == null || solicitud == null)
            {
                return destinatarios;
            }

            var contextoNormalizado = (contexto ?? string.Empty).Trim().ToUpperInvariant();

            if (solicitud.CodigoUsuario > 0)
            {
                destinatarios.Add(solicitud.CodigoUsuario);
            }

            if (solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0)
            {
                destinatarios.Add(solicitud.CodigoTecnico.Value);
            }

            if (inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
            {
                destinatarios.Add(inspeccion.CodigoInspector.Value);
            }

            if (contextoNormalizado == "NC_GENERADAS" || contextoNormalizado == "DEVOLUCION_INSPECCION")
            {
                return destinatarios;
            }

            return destinatarios;
        }

        private void EvaluarGeneracionOrSiCorresponde(int codigoSolicitud, string usuarioNombre, int usuarioId)
        {
            if (codigoSolicitud <= 0)
            {
                return;
            }

            try
            {
                var resultadoOr = _integracionInspeccionOrService.GenerarORSiCorresponde(codigoSolicitud, usuarioNombre);
                _auditoriaService.RegistrarEvento(
                    modulo: "IntegracionInspeccionOR",
                    accion: resultadoOr.Exitoso ? "OR_EVALUADA_OK" : "OR_EVALUADA_BLOQUEADA",
                    entidad: "aocr_tbsolicitud",
                    entidadId: codigoSolicitud,
                    estadoAnterior: null,
                    estadoNuevo: resultadoOr.Exitoso ? "OR_HABILITADA" : "NO_HABILITADA",
                    usuarioId: usuarioId,
                    usuarioNombre: usuarioNombre,
                    observacion: resultadoOr.Mensaje,
                    ip: null,
                    datosResumen: "Evaluación de OR tras cierre de inspección.");
            }
            catch (Exception ex)
            {
                _auditoriaService.RegistrarEvento(
                    modulo: "IntegracionInspeccionOR",
                    accion: "OR_EVALUADA_ERROR",
                    entidad: "aocr_tbsolicitud",
                    entidadId: codigoSolicitud,
                    estadoAnterior: null,
                    estadoNuevo: "ERROR",
                    usuarioId: usuarioId,
                    usuarioNombre: usuarioNombre,
                    observacion: ex.Message,
                    ip: null,
                    datosResumen: "Error no bloqueante al evaluar OR.");
            }
        }

        private static string ConstruirMensajeFaltantes(ResultadoValidacionDocumental validacion)
        {
            if (validacion == null)
            {
                return "Validación documental inválida.";
            }

            var faltantes = validacion.DocumentosFaltantes ?? new List<string>();
            if (faltantes.Count > 0)
            {
                return "Faltan documentos: " + string.Join(", ", faltantes) + ".";
            }

            var errores = validacion.Errores ?? new List<string>();
            if (errores.Count > 0)
            {
                return string.Join(" ", errores.ToArray());
            }

            return "Existen observaciones documentales pendientes.";
        }

        private int ContarNoConformidadesAbiertas(int inspeccionId)
        {
            if (inspeccionId <= 0)
            {
                return 0;
            }

            var hallazgos = _hallazgoBL.ObtenerPorInspeccion(inspeccionId) ?? new List<Hallazgo>();
            var abiertas = 0;

            foreach (var hallazgo in hallazgos)
            {
                if (hallazgo == null)
                {
                    continue;
                }

                if (!string.Equals((hallazgo.Estado ?? string.Empty).Trim(), "CERRADO", StringComparison.OrdinalIgnoreCase))
                {
                    abiertas++;
                }
            }

            return abiertas;
        }

        private ResultadoOperacion ValidarAprobacionNoConformidad(
            int inspeccionId,
            string tipoResultadoEsperado,
            out Inspeccion inspeccion,
            out InspeccionInformeTecnico informe)
        {
            inspeccion = null;
            informe = null;

            if (inspeccionId <= 0)
            {
                return ResultadoOperacion.Error("Inspección inválida");
            }

            inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
            if (inspeccion == null)
            {
                return ResultadoOperacion.Error("Inspección no encontrada");
            }

            var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
            if (!string.Equals(estadoActual, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase))
            {
                return ResultadoOperacion.Error("La aprobación formal de NC solo puede ejecutarse cuando la inspección está en resultado no satisfactorio.");
            }

            informe = _informeDAO.ObtenerUltimoPorInspeccion(inspeccionId);
            if (informe == null)
            {
                return ResultadoOperacion.Error("No existe informe técnico para sustentar la aprobación de la NC.");
            }

            if (!informe.Finalizado || !informe.FirmadoInspector)
            {
                return ResultadoOperacion.Error("La aprobación de la NC requiere un Informe Técnico finalizado y firmado por el inspector.");
            }

            var resultadoInforme = NormalizarResultadoInformeTecnico(informe.Resultado);
            if (!string.Equals(resultadoInforme, "INSATISFACTORIO", StringComparison.OrdinalIgnoreCase))
            {
                return ResultadoOperacion.Error("La aprobación formal de NC solo aplica a informes técnicos con resultado insatisfactorio.");
            }

            var tipoResultado = NormalizarTipoResultadoInsatisfactorio(informe.TipoResultadoInsatisfactorio);
            if (string.IsNullOrWhiteSpace(tipoResultado))
            {
                return ResultadoOperacion.Error("El informe técnico debe indicar si la NC requiere nueva inspección o subsanación documental.");
            }

            if (!string.Equals(tipoResultado, tipoResultadoEsperado, StringComparison.OrdinalIgnoreCase))
            {
                var descripcionRuta = string.Equals(tipoResultado, "CON_INSPECCION", StringComparison.OrdinalIgnoreCase)
                    ? "nueva inspección"
                    : "subsanación documental";
                return ResultadoOperacion.Error("La NC registrada exige la ruta de " + descripcionRuta + " según el Informe Técnico.");
            }

            if (ContarNoConformidadesAbiertas(inspeccionId) <= 0)
            {
                return ResultadoOperacion.Error("No se puede aprobar la ruta de NC sin al menos una no conformidad abierta.");
            }

            if (!EstadosInspeccion.EsTransicionValida(estadoActual, EstadosInspeccion.OBSERVADA))
            {
                return ResultadoOperacion.Error("La inspección no admite pasar a observada para continuar la ruta de NC.");
            }

            return ResultadoOperacion.Ok(null, "Validación de NC aprobada.");
        }

        private void TransicionarSolicitudObservadaParaSubsanacion(int codigoSolicitud, string observacion, int usuarioId)
        {
            if (codigoSolicitud <= 0)
            {
                return;
            }

            var solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitud);
            if (solicitud == null)
            {
                return;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            if (string.Equals(estadoActual, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string mensajeCambio;
            new SolicitudEstadoTransitionBL().CambiarEstadoConReglasAocr(
                codigoSolicitud,
                EstadoSolicitud.Observada,
                string.IsNullOrWhiteSpace(observacion)
                    ? "Coordinación aprobó NC. RT habilitado para subsanación documental."
                    : observacion,
                usuarioId,
                _ => true,
                out mensajeCambio);
        }

        private void NotificarRtSubsanacionDocumentalHabilitada(Inspeccion inspeccion, string observacion)
        {
            if (inspeccion == null || inspeccion.CodigoSolicitud <= 0)
            {
                return;
            }

            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                if (solicitud == null)
                {
                    return;
                }

                new SolicitudAocrCorreoService().NotificarEvento(
                    solicitud,
                    "OBSERVADA",
                    observacion,
                    correlationId: "NC_SUBSANACION_" + inspeccion.CodigoInspeccion);
            }
            catch
            {
                // La subsanación no debe fallar si el correo no se encola.
            }
        }

        private ResultadoOperacion AsegurarNoConformidadDesdeInforme(int inspeccionId, InspeccionInformeTecnico informe, int usuarioId, string usuarioNombre)
        {
            if (ContarNoConformidadesAbiertas(inspeccionId) > 0)
            {
                return ResultadoOperacion.Ok(null, "La inspección ya cuenta con no conformidades abiertas.");
            }

            var descripcion = ConstruirDescripcionNoConformidadDesdeInforme(informe);
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                return ResultadoOperacion.Error("No se puede registrar un resultado insatisfactorio sin hallazgos u observaciones técnicas que respalden la no conformidad.");
            }

            var hallazgo = new Hallazgo
            {
                CodigoInspeccion = inspeccionId,
                Descripcion = descripcion,
                Criticidad = "ALTA",
                Estado = "ABIERTO"
            };

            var usuarioRegistro = string.IsNullOrWhiteSpace(usuarioNombre) ? "sistema" : usuarioNombre;
            var hallazgoId = _hallazgoBL.Crear(hallazgo, usuarioRegistro);
            if (hallazgoId <= 0)
            {
                return ResultadoOperacion.Error("No se pudo materializar la no conformidad a partir del Informe Técnico insatisfactorio.");
            }

            _auditoriaService.RegistrarAccionInspeccion(
                inspeccionId,
                "NC_AUTOGENERADA_DESDE_INFORME",
                usuarioId,
                usuarioRegistro,
                descripcion,
                null,
                "HallazgoId: " + hallazgoId + ", Origen=INFORME_TECNICO_INSATISFACTORIO");

            return ResultadoOperacion.Ok(hallazgoId, "No conformidad base registrada desde el Informe Técnico.");
        }

        private static string ConstruirDescripcionNoConformidadDesdeInforme(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return string.Empty;
            }

            var detalle = string.Empty;
            var candidatos = new[]
            {
                informe.NoConformidades,
                informe.Observaciones,
                informe.Conclusiones,
                informe.Recomendaciones
            };

            foreach (var candidato in candidatos)
            {
                if (!string.IsNullOrWhiteSpace(candidato))
                {
                    detalle = candidato.Trim();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(detalle))
            {
                detalle = "Resultado insatisfactorio registrado en el Informe Técnico.";
            }

            var tipo = NormalizarTipoResultadoInsatisfactorio(informe.TipoResultadoInsatisfactorio);
            if (string.Equals(tipo, "CON_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return "NC - Requiere nueva inspección. " + detalle;
            }

            if (string.Equals(tipo, "SIN_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return "NC - Requiere subsanación documental sin nueva inspección. " + detalle;
            }

            return detalle;
        }

        private static string CombinarObservacionResultadoInsatisfactorio(string observacion, string tipoResultadoInsatisfactorio)
        {
            var detalleTipo = string.Equals(tipoResultadoInsatisfactorio, "CON_INSPECCION", StringComparison.OrdinalIgnoreCase)
                ? "Tipo de resultado insatisfactorio: con nueva inspección."
                : (string.Equals(tipoResultadoInsatisfactorio, "SIN_INSPECCION", StringComparison.OrdinalIgnoreCase)
                    ? "Tipo de resultado insatisfactorio: subsanación documental sin nueva inspección."
                    : string.Empty);

            if (string.IsNullOrWhiteSpace(detalleTipo))
            {
                return observacion;
            }

            if (string.IsNullOrWhiteSpace(observacion))
            {
                return detalleTipo;
            }

            return observacion.Trim() + " " + detalleTipo;
        }

        private static string ConstruirObservacionAprobacionNoConformidad(string observacion, string observacionPredeterminada, string tipoResultadoInsatisfactorio)
        {
            var baseObservacion = string.IsNullOrWhiteSpace(observacion)
                ? observacionPredeterminada
                : observacion.Trim();

            return CombinarObservacionResultadoInsatisfactorio(baseObservacion, tipoResultadoInsatisfactorio);
        }

        private static string ConstruirMensajeResultadoInsatisfactorio(string tipoResultadoInsatisfactorio)
        {
            if (string.Equals(tipoResultadoInsatisfactorio, "CON_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return "Resultado insatisfactorio registrado. Se generó la no conformidad base y el expediente debe continuar por la ruta de nueva inspección.";
            }

            if (string.Equals(tipoResultadoInsatisfactorio, "SIN_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return "Resultado insatisfactorio registrado. Se generó la no conformidad base y el expediente debe continuar por la ruta de subsanación documental.";
            }

            return "Resultado insatisfactorio registrado. Se generó la no conformidad base del expediente.";
        }

        private static string NormalizarResultadoInformeTecnico(string resultado)
        {
            var normalizado = (resultado ?? string.Empty).Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');

            switch (normalizado)
            {
                case "APROBADO":
                case EstadosInspeccion.RESULTADO_SATISFACTORIO:
                    return "SATISFACTORIO";
                case "NO_SATISFACTORIO":
                case "RECHAZADO":
                case EstadosInspeccion.RESULTADO_NO_SATISFACTORIO:
                    return "INSATISFACTORIO";
                default:
                    return normalizado;
            }
        }

        private static string NormalizarTipoResultadoInsatisfactorio(string tipoResultado)
        {
            var normalizado = (tipoResultado ?? string.Empty).Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');

            switch (normalizado)
            {
                case "CON_INSPECCION":
                case "SIN_INSPECCION":
                    return normalizado;
                default:
                    return string.Empty;
            }
        }
    }
}
