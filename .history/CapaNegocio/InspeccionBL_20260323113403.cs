using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio
{
    /// <summary>
    /// Lógica de negocio para inspecciones AOCR.
    /// </summary>
    public class InspeccionBL
    {
        private readonly InspeccionDAO _dao;
        private readonly InspeccionHistorialDAO _historialDAO;
        private readonly HallazgoDAO _hallazgoDAO;

        public InspeccionBL()
        {
            _dao = new InspeccionDAO();
            _historialDAO = new InspeccionHistorialDAO();
            _hallazgoDAO = new HallazgoDAO();
        }

        public InspeccionBL(InspeccionDAO dao)
        {
            _dao = dao ?? throw new ArgumentNullException(nameof(dao));
            _historialDAO = new InspeccionHistorialDAO();
            _hallazgoDAO = new HallazgoDAO();
        }

        public Inspeccion ObtenerPorId(int id)
        {
            if (id <= 0) throw new ArgumentException("ID inválido.");
            return _dao.ObtenerPorId(id);
        }

        public List<Inspeccion> ListarTodas()
        {
            return _dao.ListarTodas() ?? new List<Inspeccion>();
        }

        public List<Inspeccion> ListarPorInspector(int codigoInspector)
        {
            if (codigoInspector <= 0) return new List<Inspeccion>();
            return _dao.ListarPorInspector(codigoInspector) ?? new List<Inspeccion>();
        }

        public int Crear(Inspeccion model, int codigoUsuario)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.CodigoSolicitud <= 0) throw new Exception("Código de solicitud inválido.");

            model.CreatedBy = codigoUsuario;
            model.UpdatedBy = codigoUsuario;

            if (string.IsNullOrWhiteSpace(model.Estado))
            {
                model.Estado = EstadosInspeccion.SOLICITUD_INSPECCION_CREADA;
            }
            else
            {
                model.Estado = EstadosInspeccion.NormalizarEstado(model.Estado);
            }

            int id = _dao.Crear(model);
            model.CodigoInspeccion = id;
            return id;
        }

        public bool Actualizar(Inspeccion model, int updatedBy)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.CodigoInspeccion <= 0) throw new Exception("Código de inspección inválido.");

            model.UpdatedBy = updatedBy;
            model.UpdatedAt = DateTime.Now;
            model.Estado = EstadosInspeccion.NormalizarEstado(model.Estado);

            return _dao.Actualizar(model);
        }

        public bool CambiarEstado(int id, string estado, int updatedBy, string observacion = null, string usuarioNombre = null, string origen = "FLUJO_BPMN")
        {
            if (id <= 0) throw new Exception("ID inválido.");
            if (string.IsNullOrWhiteSpace(estado)) throw new Exception("Estado requerido.");

            var inspeccion = _dao.ObtenerPorId(id);
            if (inspeccion == null) throw new Exception("Inspección no encontrada.");

            var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
            var estadoDestino = EstadosInspeccion.NormalizarEstado(estado);

            if (!EstadosInspeccion.EsEstadoValido(estadoDestino))
            {
                throw new Exception("Estado de inspección inválido: " + estadoDestino);
            }

            if (!EstadosInspeccion.EsTransicionValida(estadoActual, estadoDestino))
            {
                throw new Exception("Transición de inspección no permitida: " + estadoActual + " -> " + estadoDestino);
            }

            var ok = _dao.CambiarEstado(id, estadoDestino, updatedBy);
            if (ok)
            {
                _historialDAO.Registrar(id, estadoActual, estadoDestino, updatedBy, usuarioNombre, observacion, origen);
            }

            return ok;
        }

        public bool GuardarInforme(int id, string rutaInforme, int updatedBy)
        {
            if (id <= 0) throw new Exception("ID inválido.");
            if (string.IsNullOrWhiteSpace(rutaInforme)) throw new Exception("Ruta de informe requerida.");
            return _dao.GuardarInforme(id, rutaInforme, updatedBy);
        }

        public bool CerrarInspeccion(int id, string resultado, int updatedBy)
        {
            if (id <= 0) throw new Exception("ID inválido.");
            if (string.IsNullOrWhiteSpace(resultado)) throw new Exception("Resultado requerido.");

            var inspeccion = _dao.ObtenerPorId(id);
            if (inspeccion == null) throw new Exception("Inspección no encontrada.");

            var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
            if (!string.Equals(estadoActual, EstadosInspeccion.RESULTADO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(estadoActual, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(estadoActual, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(estadoActual, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Solo se puede cerrar una inspección con informe elaborado o resultado registrado.");
            }

            var hallazgos = _hallazgoDAO.ObtenerPorInspeccion(id) ?? new List<Hallazgo>();
            var hayHallazgosPendientes = hallazgos.Any(h => h != null && EsHallazgoPendienteCierre(h.Estado));
            if (hayHallazgosPendientes)
            {
                throw new Exception("No se puede cerrar la inspección: existen hallazgos pendientes de cierre.");
            }

            var ok = _dao.Cerrar(id, resultado, updatedBy);
            if (ok)
            {
                _historialDAO.Registrar(id, estadoActual, EstadosInspeccion.CERRADA, updatedBy, null, "Cierre formal de inspección. Resultado: " + resultado, "CIERRE_INSPECCION");
            }

            return ok;
        }

        private static bool EsHallazgoPendienteCierre(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return true;
            }

            var normalizado = estado.Trim().ToUpperInvariant();
            return normalizado != "CERRADO" && normalizado != "RESUELTO";
        }
    }
}
