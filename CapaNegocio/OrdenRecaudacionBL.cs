using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Services;
using CapaNegocio.Helpers;

namespace CapaNegocio
{
    /// <summary>
    /// Capa de negocio para Órdenes de Recaudación
    /// VERSIÓN CORREGIDA: Implementa patrón async/await correctamente
    /// </summary>
    public class OrdenRecaudacionBL
    {
        private readonly OrdenRecaudacionDAO _dao;

        public class GenerarOrdenResult
        {
            public bool Success { get; set; }
            public int OrdenId { get; set; }
            public string Error { get; set; }
        }

        // Constructor por defecto - usa configuración segura
        public OrdenRecaudacionBL()
        {
            var config = new SecureConfigurationService();
            var connectionString = config.GetConnectionString("PostgreSQL");
            _dao = new OrdenRecaudacionDAO(connectionString);
        }

        // Constructor con inyección de dependencias para testing
        public OrdenRecaudacionBL(string connectionString)
        {
            _dao = new OrdenRecaudacionDAO(connectionString);
        }

        /// <summary>
        /// Lista todas las órdenes de recaudación del usuario
        /// ✅ CORREGIDO: Ahora es async sin bloquear con .Result
        /// </summary>
        public async Task<List<OrdenRecaudacion>> ListarPorUsuarioAsync(string usuario)
        {
            var result = await _dao.ObtenerTodosAsync();
            return new List<OrdenRecaudacion>(result);
        }

        /// <summary>
        /// Obtiene una orden por su ID
        /// ✅ CORREGIDO: Ahora es async sin bloquear con .Result
        /// </summary>
        public async Task<OrdenRecaudacion> ObtenerPorIdAsync(int id)
        {
            return await _dao.ObtenerPorIdAsync(id);
        }

        /// <summary>
        /// Inserta una nueva orden de recaudación
        /// ✅ CORREGIDO: Ahora es async sin bloquear con .Result
        /// </summary>
        public async Task<int> InsertarAsync(OrdenRecaudacion orden)
        {
            return await _dao.CrearAsync(orden);
        }

        /// <summary>
        /// Actualiza una orden existente
        /// ✅ CORREGIDO: Ahora es async sin bloquear con .Result
        /// </summary>
        public async Task<bool> ActualizarAsync(OrdenRecaudacion orden)
        {
            return await _dao.ActualizarAsync(orden);
        }

        /// <summary>
        /// Cambia el estado de una orden
        /// ✅ CORREGIDO: Ahora es async sin bloquear con .Result
        /// </summary>
        public async Task<bool> CambiarEstadoAsync(int id, string nuevoEstado, string observacion = null)
        {
            return await _dao.ActualizarEstadoAsync(id, nuevoEstado, "SYSTEM");
        }

        public async Task<GenerarOrdenResult> GenerarOrdenEnUnPasoAsync(OrdenRecaudacion orden, string rolOrigen = "Solicitante")
        {
            if (orden == null)
            {
                return new GenerarOrdenResult { Success = false, Error = "La orden es nula." };
            }

            var emailDestino = string.IsNullOrWhiteSpace(orden.Correo) ? orden.EmailContribuyente : orden.Correo;
            var asunto = string.Format("Orden de Recaudación generada - {0}", orden.NumeroOrden ?? "N/A");
            var cuerpo = EmailTemplateBuilder.OrdenGenerada(
                orden.NombreContribuyente ?? orden.Compania,
                orden.NumeroOrden,
                orden.CodigoSolicitud.HasValue ? orden.CodigoSolicitud.Value.ToString() : "N/A",
                DateTime.Now,
                rolOrigen,
                orden.Total ?? 0m,
                null);
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);

            return await Task.Run(() =>
            {
                int ordenId;
                string err;
                var ok = _dao.CrearOrdenGeneradaConCorreoTransaccional(
                    orden,
                    emailDestino,
                    asunto,
                    cuerpo,
                    correlationId,
                    out ordenId,
                    out err);

                return new GenerarOrdenResult
                {
                    Success = ok,
                    OrdenId = ordenId,
                    Error = err
                };
            });
        }

        // ============================================================
        // MÉTODOS SÍNCRONOS LEGACY (Mantener para compatibilidad temporal)
        // ============================================================
        // NOTA: Estos métodos están DEPRECATED y se eliminarán en versión futura
        // Usar los métodos Async para nuevo código

        [Obsolete("Use ListarPorUsuarioAsync instead. Este método será eliminado en v2.0")]
        public List<OrdenRecaudacion> ListarPorUsuario(string usuario)
        {
            // Usar GetAwaiter().GetResult() en lugar de .Result para mejor stack trace
            return ListarPorUsuarioAsync(usuario).GetAwaiter().GetResult();
        }

        [Obsolete("Use ObtenerPorIdAsync instead. Este método será eliminado en v2.0")]
        public OrdenRecaudacion ObtenerPorId(int id)
        {
            return ObtenerPorIdAsync(id).GetAwaiter().GetResult();
        }

        [Obsolete("Use InsertarAsync instead. Este método será eliminado en v2.0")]
        public int Insertar(OrdenRecaudacion orden)
        {
            return InsertarAsync(orden).GetAwaiter().GetResult();
        }

        [Obsolete("Use ActualizarAsync instead. Este método será eliminado en v2.0")]
        public bool Actualizar(OrdenRecaudacion orden)
        {
            return ActualizarAsync(orden).GetAwaiter().GetResult();
        }

        [Obsolete("Use CambiarEstadoAsync instead. Este método será eliminado en v2.0")]
        public bool CambiarEstado(int id, string nuevoEstado, string observacion = null)
        {
            return CambiarEstadoAsync(id, nuevoEstado, observacion).GetAwaiter().GetResult();
        }
    }
}
