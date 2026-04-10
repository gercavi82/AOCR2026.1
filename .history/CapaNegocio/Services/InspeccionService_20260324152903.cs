using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class InspeccionService
    {
        // 1. Definimos la instancia del DAO
        private readonly InspeccionDAO _inspeccionDAO;
        private readonly InspeccionBL _inspeccionBL;
        private readonly HallazgoBL _hallazgoBL;
        private readonly SolicitudAOCRDAO _solicitudDAO;
        private readonly AuditoriaService _auditoriaService;
        private readonly ValidacionDocumentalService _validacionDocumentalService;
        private readonly IntegracionInspeccionOrService _integracionInspeccionOrService;
        private readonly InspeccionWorkflowService _workflowService;

        public InspeccionService()
        {
            // Inicializamos el DAO (esto soluciona los errores CS0120)
            _inspeccionDAO = new InspeccionDAO();
            _inspeccionBL = new InspeccionBL();
            _hallazgoBL = new HallazgoBL();
            _solicitudDAO = new SolicitudAOCRDAO();
            _auditoriaService = new AuditoriaService();
            _validacionDocumentalService = new ValidacionDocumentalService();
            _integracionInspeccionOrService = new IntegracionInspeccionOrService();
            _workflowService = new InspeccionWorkflowService();
        }

        // ✅ Crear inspección
        public ResultadoOperacion CrearInspeccion(Inspeccion inspeccion, int usuarioId)
        {
            try
            {
                if (inspeccion.FechaProgramada.HasValue && inspeccion.FechaProgramada.Value < DateTime.Today)
                    return ResultadoOperacion.Error("La fecha programada no puede ser en el pasado");

                inspeccion.CreatedBy = usuarioId;
                inspeccion.UpdatedBy = usuarioId;

                // Llamada por instancia. Crear devuelve un int (ID)
                int nuevoId = _inspeccionDAO.Crear(inspeccion);

                if (nuevoId > 0)
                {
                    return ResultadoOperacion.Ok(nuevoId, "Inspección creada con éxito");
                }

                return ResultadoOperacion.Error("No se pudo insertar la inspección en la base de datos.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al crear inspección: " + ex.Message);
            }
        }

        // ✅ Programar inspección
        public ResultadoOperacion ProgramarInspeccion(int inspeccionId, DateTime fecha, TimeSpan hora, string lugar, int usuarioId)
        {
            try
            {
                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                    return ResultadoOperacion.Error("Inspección no encontrada");

                if (fecha < DateTime.Today)
                    return ResultadoOperacion.Error("La fecha no puede ser en el pasado");

                inspeccion.FechaProgramada = fecha;
                inspeccion.HoraProgramada = hora;
                inspeccion.Lugar = lugar;
                inspeccion.Estado = "PROGRAMADA";
                inspeccion.UpdatedBy = usuarioId;

                bool exito = _inspeccionDAO.Actualizar(inspeccion);
                return exito ? ResultadoOperacion.Ok(null, "Programación actualizada") : ResultadoOperacion.Error("Error al actualizar");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error(ex.Message);
            }
        }

        // ✅ Asignar inspector
        public ResultadoOperacion AsignarInspector(int inspeccionId, int inspectorId, int usuarioId)
        {
            try
            {
                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                    return ResultadoOperacion.Error("Inspección no encontrada");

                // Nota: ListarPorInspector debe existir en tu DAO
                var inspeccionesInspector = _inspeccionDAO.ListarPorInspector(inspectorId);

                if (inspeccion.FechaProgramada.HasValue)
                {
                    foreach (var i in inspeccionesInspector)
                    {
                        if (i.FechaProgramada.HasValue &&
                            i.FechaProgramada.Value.Date == inspeccion.FechaProgramada.Value.Date &&
                            i.CodigoInspeccion != inspeccionId)
                        {
                            return ResultadoOperacion.Error("El inspector ya tiene una asignación para esa fecha.");
                        }
                    }
                }

                inspeccion.CodigoInspector = inspectorId;
                inspeccion.UpdatedBy = usuarioId;

                bool exito = _inspeccionDAO.Actualizar(inspeccion);

                if (exito)
                {
                    _auditoriaService.RegistrarAccionInspeccion(
                        inspeccionId,
                        "ASIGNACION_INSPECTOR",
                        usuarioId,
                        usuarioId.ToString(),
                        "Inspector asignado: " + inspectorId,
                        null,
                        "Asignación de inspector sobre solicitud " + inspeccion.CodigoSolicitud);
                }

                return exito ? ResultadoOperacion.Ok(null, "Inspector asignado") : ResultadoOperacion.Error("Error al asignar");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error(ex.Message);
            }
        }

        // ✅ Finalizar inspección
        public ResultadoOperacion FinalizarInspeccion(int inspeccionId, string resultado, string comentarios, int usuarioId)
        {
            try
            {
                var inspeccion = _inspeccionDAO.ObtenerPorId(inspeccionId);
                if (inspeccion == null)
                    return ResultadoOperacion.Error("Inspección no encontrada");

                var validacionDocs = _validacionDocumentalService.PuedeAvanzarEtapa(inspeccion.CodigoSolicitud, "CIERRE_INSPECCION");
                if (!validacionDocs.EsValido)
                {
                    return ResultadoOperacion.Error("No se puede finalizar la inspección. " + ConstruirMensajeFaltantes(validacionDocs));
                }

                var estadoAnterior = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);

                inspeccion.Estado = "FINALIZADA";
                inspeccion.Resultado = resultado;
                inspeccion.Comentarios = comentarios;
                inspeccion.UpdatedBy = usuarioId;

                bool exito = _inspeccionDAO.Actualizar(inspeccion);

                if (exito)
                {
                    _auditoriaService.RegistrarCambioEstadoInspeccion(
                        inspeccionId,
                        estadoAnterior,
                        EstadosInspeccion.INFORME_ELABORADO,
                        usuarioId,
                        usuarioId.ToString(),
                        comentarios,
                        null,
                        "Finalización manual de inspección.");
                }

                return exito ? ResultadoOperacion.Ok(null, "Inspección finalizada") : ResultadoOperacion.Error("Error al cerrar");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error(ex.Message);
            }
        }

        // ✅ Obtener inspecciones por rango de fechas
        public List<Inspeccion> ObtenerInspeccionesPorFecha(DateTime fechaInicio, DateTime fechaFin)
        {
            // El DAO debe tener ListarTodas() como método de instancia
            var todas = _inspeccionDAO.ListarPorInspector(0); // O un método ListarTodas()
            var filtradas = new List<Inspeccion>();

            foreach (var i in todas)
            {
                if (i.FechaProgramada.HasValue &&
                    i.FechaProgramada.Value.Date >= fechaInicio.Date &&
                    i.FechaProgramada.Value.Date <= fechaFin.Date)
                {
                    filtradas.Add(i);
                }
            }
            return filtradas;
        }

        public ResultadoOperacion EvaluarInspeccion(int inspeccionId, string resultado, string observacion, int usuarioId, string usuarioNombre)
        {
            return _workflowService.EvaluarInspeccion(inspeccionId, resultado, observacion, usuarioId, usuarioNombre);
        }

        public ResultadoOperacion SubsanarInspeccion(int inspeccionId, string observacion, int usuarioId, string usuarioNombre)
        {
            return _workflowService.SubsanarInspeccion(inspeccionId, observacion, usuarioId, usuarioNombre);
        }

        public ResultadoOperacion RevalidarInspeccion(int inspeccionId, bool aprobada, string observacion, int usuarioId, string usuarioNombre)
        {
            return _workflowService.RevalidarInspeccion(inspeccionId, aprobada, observacion, usuarioId, usuarioNombre);
        }

        public ResultadoOperacion SolicitarNuevaInspeccion(int inspeccionId, string observacion, int usuarioId, string usuarioNombre)
        {
            return _workflowService.SolicitarNuevaInspeccion(inspeccionId, observacion, usuarioId, usuarioNombre);
        }

        public ResultadoOperacion RegistrarNoConformidad(int inspeccionId, string descripcion, string criticidad, int usuarioId, string usuarioNombre)
        {
            return _workflowService.RegistrarNoConformidad(inspeccionId, descripcion, criticidad, usuarioId, usuarioNombre);
        }

        public ResultadoOperacion RegistrarResultadoInspeccion(int inspeccionId, string resultado, string observacion, int usuarioId, string usuarioNombre)
        {
            return _workflowService.RegistrarResultadoInspeccion(inspeccionId, resultado, observacion, usuarioId, usuarioNombre);
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

                NotificarSolicitanteCambioEstado(inspeccion, estadoDestinoNormalizado);

                _auditoriaService.RegistrarCambioEstadoInspeccion(
                    inspeccionId,
                    estadoAnterior,
                    estadoDestinoNormalizado,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    null,
                    "Origen: " + (origen ?? "N/A"));

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

        private void NotificarSolicitanteCambioEstado(Inspeccion inspeccion, string estadoDestino)
        {
            try
            {
                if (inspeccion == null)
                {
                    return;
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                if (solicitud == null || solicitud.CodigoUsuario <= 0)
                {
                    return;
                }

                NotificacionBL.EnviarNotificacion(
                    solicitud.CodigoUsuario,
                    "Actualización de Inspección",
                    "La inspección #" + inspeccion.CodigoInspeccion + " cambió al estado: " + estadoDestino,
                    "INFO",
                    "/Inspeccion/Detalle/" + inspeccion.CodigoInspeccion,
                    "Inspeccion",
                    inspeccion.CodigoInspeccion,
                    "aocr_tbinspeccion");
            }
            catch
            {
                // Notificación no bloqueante para el flujo principal.
            }
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
                return "Faltan documentos: " + string.Join(", ", faltantes.Distinct(StringComparer.OrdinalIgnoreCase)) + ".";
            }

            var errores = validacion.Errores ?? new List<string>();
            if (errores.Count > 0)
            {
                return string.Join(" ", errores.Take(3));
            }

            return "Existen observaciones documentales pendientes.";
        }
    }
}