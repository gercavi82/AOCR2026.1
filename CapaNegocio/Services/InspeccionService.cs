using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class InspeccionService
    {
        // 1. Definimos la instancia del DAO
        private readonly InspeccionDAO _inspeccionDAO;

        public InspeccionService()
        {
            // Inicializamos el DAO (esto soluciona los errores CS0120)
            _inspeccionDAO = new InspeccionDAO();
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

                inspeccion.Estado = "FINALIZADA";
                inspeccion.Resultado = resultado;
                inspeccion.Comentarios = comentarios;
                inspeccion.UpdatedBy = usuarioId;

                bool exito = _inspeccionDAO.Actualizar(inspeccion);
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
    }
}