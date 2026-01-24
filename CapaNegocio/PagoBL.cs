using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;

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
            _dao = new PagoDAO();
        }

        public PagoBL(PagoDAO dao)
        {
            _dao = dao ?? throw new ArgumentNullException(nameof(dao));
        }

        // ==========================
        // Consultas
        // ==========================

        public List<Pago> ObtenerTodos()
        {
            // Si no quieres listar todo en producción, filtra por rango/estado.
            return _dao.ObtenerTodos() ?? new List<Pago>();
        }

        public Pago ObtenerPorId(int codigoPago)
        {
            if (codigoPago <= 0) throw new ArgumentException("Código de pago inválido.");
            return _dao.ObtenerPorId(codigoPago);
        }

        public Pago ObtenerUltimoPorSolicitud(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0) throw new ArgumentException("Código de solicitud inválido.");
            return _dao.ObtenerPorSolicitud(codigoSolicitud);
        }

        public List<Pago> ObtenerPorSolicitud(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0) return new List<Pago>();
            return _dao.ObtenerPorSolicitudCompleto(codigoSolicitud) ?? new List<Pago>();
        }

        public List<Pago> ObtenerPorEstado(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return new List<Pago>();
            return _dao.ObtenerPorEstado(estado) ?? new List<Pago>();
        }

        public List<Pago> ObtenerPorRangoFechas(DateTime inicio, DateTime fin)
        {
            if (fin < inicio) throw new ArgumentException("Rango de fechas inválido.");
            return _dao.ObtenerPorRangoFechas(inicio, fin) ?? new List<Pago>();
        }

        // ==========================
        // Registro / Actualización
        // ==========================

        public int Crear(Pago pago)
        {
            if (pago == null) throw new ArgumentNullException(nameof(pago));
            ValidarPagoMinimo(pago);

            // Seguridad: evitar doble registro por transacción (si viene)
            if (!string.IsNullOrWhiteSpace(pago.NumeroTransaccion))
            {
                if (_dao.ExistePorNumeroTransaccion(pago.NumeroTransaccion))
                    throw new Exception("Ya existe un pago con ese número de transacción.");
            }

            // Defaults
            if (string.IsNullOrWhiteSpace(pago.Estado))
                pago.Estado = "REGISTRADO";
            if (!pago.FechaPago.HasValue)
                pago.FechaPago = DateTime.Now;

            // DAO devuelve int (RETURNING codigopago)
            int id = _dao.Crear(pago);
            if (id <= 0) throw new Exception("No se pudo registrar el pago.");
            pago.CodigoPago = id;
            return id;
        }

        public bool Actualizar(Pago pago)
        {
            if (pago == null) throw new ArgumentNullException(nameof(pago));
            if (pago.CodigoPago <= 0) throw new ArgumentException("Código de pago inválido.");

            ValidarPagoMinimo(pago);
            return _dao.Actualizar(pago);
        }

        // ==========================
        // Métricas / reportes
        // ==========================

        public bool ExistePagoParaSolicitud(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0) return false;
            return _dao.ExistePagoParaSolicitud(codigoSolicitud);
        }

        public List<Pago> ObtenerPagosValidadosHoy()
        {
            return _dao.ObtenerPagosValidadosHoy() ?? new List<Pago>();
        }

        public decimal ObtenerMontoRecaudadoMes(int year, int month)
        {
            if (year < 2000 || year > 2100) throw new ArgumentException("Año inválido.");
            if (month < 1 || month > 12) throw new ArgumentException("Mes inválido.");

            return _dao.ObtenerMontoRecaudadoMes(year, month);
        }

        // ==========================
        // Validaciones
        // ==========================

        private static void ValidarPagoMinimo(Pago pago)
        {
            if (pago.CodigoSolicitud <= 0) throw new Exception("Código de solicitud inválido.");
            if (pago.Monto <= 0m) throw new Exception("Monto inválido.");
            // NumeroComprobante puede ser opcional según tu flujo.
            // if (string.IsNullOrWhiteSpace(pago.NumeroComprobante)) throw new Exception("Número de comprobante requerido.");
        }
    }
}
