using System;
using System.Collections.Generic;
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

        public List<Pago> ObtenerTodos()
        {
            var result = _dao.ObtenerPorEstadoAsync("TODOS").Result;
            return new List<Pago>(result);
        }

        public Pago ObtenerPorId(int id)
        {
            return _dao.ObtenerPorIdAsync(id).Result;
        }

        public List<Pago> ObtenerPorSolicitud(int solicitudId)
        {
            var pago = _dao.ObtenerPorOrdenIdAsync(solicitudId).Result;
            return pago != null ? new List<Pago> { pago } : new List<Pago>();
        }

        public Pago ObtenerPorSolicitudCompleto(int solicitudId)
        {
            return _dao.ObtenerPorOrdenIdAsync(solicitudId).Result;
        }

        public List<Pago> ObtenerPorEstado(string estado)
        {
            var result = _dao.ObtenerPorEstadoAsync(estado).Result;
            return new List<Pago>(result);
        }

        public List<Pago> ObtenerPorRangoFechas(DateTime desde, DateTime hasta)
        {
            // Usar obtener todos y filtrar
            var todos = _dao.ObtenerPorEstadoAsync("VALIDADO").Result;
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

        public int Crear(Pago pago)
        {
            return _dao.CrearAsync(pago).Result;
        }

        public bool Actualizar(Pago pago)
        {
            return _dao.ActualizarAsync(pago).Result;
        }

        // ==========================
        // Métricas / reportes
        // ==========================

        public bool ExistePagoParaSolicitud(int solicitudId)
        {
            var pago = _dao.ObtenerPorOrdenIdAsync(solicitudId).Result;
            return pago != null;
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
