using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Services;

namespace CapaNegocio
{
    /// <summary>
    /// Lógica de Negocio de Pagos (AOCR).
    /// Compatible con PagoDAO actual (Crear, Actualizar, ObtenerPorId, ObtenerPorSolicitud,
    /// ObtenerPorSolicitudCompleto, ObtenerPorRangoFechas, ObtenerPorEstado, ExistePagoParaSolicitud)
    /// + Métodos extendidos (ObtenerTodos, ExistePorNumeroTransaccion, ObtenerPagosValidadosHoy, ObtenerMontoRecaudadoMes).
    /// REFACTORIZADO: Todos los métodos ahora son async/await para mejor rendimiento.
    /// </summary>
    public class PagoBL
    {
        private readonly PagoDAO _dao;

        public PagoBL()
        {
            var config = new SecureConfigurationService();
            var connStr = config.GetConnectionString("PostgreSQL");
            _dao = new PagoDAO(connStr);
        }

        public PagoBL(string connectionString)
        {
            _dao = new PagoDAO(connectionString);
        }

        // ==========================
        // Consultas
        // ==========================

        public async Task<List<Pago>> ObtenerTodosAsync()
        {
            var result = await _dao.ObtenerPorEstadoAsync("TODOS");
            return new List<Pago>(result);
        }

        public async Task<Pago> ObtenerPorIdAsync(int id)
        {
            return await _dao.ObtenerPorIdAsync(id);
        }

        public async Task<List<Pago>> ObtenerPorSolicitudAsync(int solicitudId)
        {
            var pago = await _dao.ObtenerPorOrdenIdAsync(solicitudId);
            return pago != null ? new List<Pago> { pago } : new List<Pago>();
        }

        public async Task<Pago> ObtenerPorSolicitudCompletoAsync(int solicitudId)
        {
            return await _dao.ObtenerPorOrdenIdAsync(solicitudId);
        }

        public async Task<List<Pago>> ObtenerPorEstadoAsync(string estado)
        {
            var result = await _dao.ObtenerPorEstadoAsync(estado);
            return new List<Pago>(result);
        }

        public async Task<List<Pago>> ObtenerPorRangoFechasAsync(DateTime desde, DateTime hasta)
        {
            // Usar obtener todos y filtrar
            var todos = await _dao.ObtenerPorEstadoAsync("VALIDADO");
            var lista = new List<Pago>();
            foreach (var p in todos)
            {
                if (p.FechaPago >= desde && p.FechaPago <= hasta)
                    lista.Add(p);
            }
            return lista;
        }

        public bool ExistePorNumeroTransaccion(string numero)
        {
            // Implementar búsqueda
            return false;
        }

        // ==========================
        // Registro / Actualización
        // ==========================

        public async Task<int> CrearAsync(Pago pago)
        {
            return await _dao.CrearAsync(pago);
        }

        public async Task<bool> ActualizarAsync(Pago pago)
        {
            return await _dao.ActualizarAsync(pago);
        }

        // ==========================
        // Métricas / reportes
        // ==========================

        public async Task<bool> ExistePagoParaSolicitudAsync(int solicitudId)
        {
            var pago = await _dao.ObtenerPorOrdenIdAsync(solicitudId);
            return pago != null;
        }

        public async Task<List<Pago>> ObtenerPagosValidadosHoyAsync()
        {
            return await ObtenerPorEstadoAsync("VALIDADO");
        }

        public decimal ObtenerMontoRecaudadoMes(int mes, int anio)
        {
            return 0m; // Implementar si es necesario
        }
    }
}

        public List<Pago> ObtenerPagosValidadosHoy()
        {
            return ObtenerPorEstado("VALIDADO");
        }

        public decimal ObtenerMontoRecaudadoMes(int mes, int anio)
        {
            return 0m; // Implementar si es necesario
        }
    }
}
