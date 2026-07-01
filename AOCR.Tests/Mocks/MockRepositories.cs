using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CapaDatos.Entidades;
using CapaDatos.Interfaces;
using CapaDatos.Services;

namespace AOCR.Tests.Mocks
{
    /// <summary>
    /// Mock de repositorio de órdenes en memoria
    /// </summary>
    public class MockOrdenRecaudacionRepository : IOrdenRecaudacionRepository
    {
        private readonly List<OrdenRecaudacion> _ordenes = new List<OrdenRecaudacion>();
        private int _nextId = 1;

        public Task<int> CrearAsync(OrdenRecaudacion orden)
        {
            orden.Id = _nextId++;
            orden.FechaCreacion = DateTime.Now;
            _ordenes.Add(orden);
            return Task.FromResult(orden.Id);
        }

        public Task CrearDetalleAsync(DetalleOrden detalle)
        {
            return Task.CompletedTask;
        }

        public Task<OrdenRecaudacion> ObtenerPorIdAsync(int id)
        {
            var orden = _ordenes.FirstOrDefault(o => o.Id == id);
            return Task.FromResult(orden);
        }

        public Task<IEnumerable<OrdenRecaudacion>> ObtenerTodosAsync()
        {
            return Task.FromResult(_ordenes.AsEnumerable());
        }

        public Task<IEnumerable<OrdenRecaudacion>> ObtenerPorEstadoAsync(string estado)
        {
            var filtradas = _ordenes.Where(o => o.Estado == estado);
            return Task.FromResult(filtradas);
        }

        public Task<int> ObtenerConsecutivoDiarioAsync(DateTime fecha)
        {
            var count = _ordenes.Count(o => o.FechaCreacion.Date == fecha.Date);
            return Task.FromResult(count);
        }

        public Task<bool> ActualizarAsync(OrdenRecaudacion orden)
        {
            var existente = _ordenes.FirstOrDefault(o => o.Id == orden.Id);
            if (existente != null)
            {
                existente.Estado = orden.Estado;
                existente.Total = orden.Total;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> ActualizarEstadoAsync(int id, string nuevoEstado, string usuario)
        {
            var orden = _ordenes.FirstOrDefault(o => o.Id == id);
            if (orden != null)
            {
                orden.Estado = nuevoEstado;
                orden.UsuarioModificacion = usuario;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> EliminarAsync(int id, string usuario)
        {
            var orden = _ordenes.FirstOrDefault(o => o.Id == id);
            if (orden != null)
            {
                orden.Activo = false;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        // Helpers para tests
        public void Reset() => _ordenes.Clear();
        public int Count => _ordenes.Count;
    }

    /// <summary>
    /// Mock de repositorio de pagos
    /// </summary>
    public class MockPagoRepository : IPagoRepository
    {
        private readonly List<Pago> _pagos = new List<Pago>();
        private int _nextId = 1;

        public Task<int> CrearAsync(Pago pago)
        {
            pago.Id = _nextId++;
            pago.FechaRegistro = DateTime.Now;
            _pagos.Add(pago);
            return Task.FromResult(pago.Id);
        }

        public Task<Pago> ObtenerPorIdAsync(int id)
        {
            return Task.FromResult(_pagos.FirstOrDefault(p => p.Id == id));
        }

        public Task<Pago> ObtenerPorOrdenIdAsync(int ordenId)
        {
            return Task.FromResult(_pagos.FirstOrDefault(p => p.OrdenId == ordenId));
        }

        public Task<IEnumerable<Pago>> ObtenerPorEstadoAsync(string estado)
        {
            return Task.FromResult(_pagos.Where(p => p.Estado == estado));
        }

        public Task<bool> ActualizarAsync(Pago pago)
        {
            var existente = _pagos.FirstOrDefault(p => p.Id == pago.Id);
            if (existente != null)
            {
                existente.Estado = pago.Estado;
                existente.Observaciones = pago.Observaciones;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> ActualizarEstadoAsync(int id, string nuevoEstado, string usuario)
        {
            var pago = _pagos.FirstOrDefault(p => p.Id == id);
            if (pago != null)
            {
                pago.Estado = nuevoEstado;
                pago.UsuarioValidacion = usuario;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public void Reset() => _pagos.Clear();
    }

    /// <summary>
    /// Mock de servicio de email (no envía realmente)
    /// </summary>
    public class MockEmailService : IEmailService
    {
        public List<EmailSendResult> EnviosRealizados { get; } = new List<EmailSendResult>();
        public bool SimularFallo { get; set; }

        public Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null, string aliasRemitente = null)
        {
            if (SimularFallo)
            {
                return Task.FromResult(new EmailSendResult { Success = false, Error = "Simulated failure" });
            }

            var result = new EmailSendResult
            {
                Success = true,
                MessageId = Guid.NewGuid().ToString()
            };
            EnviosRealizados.Add(result);
            return Task.FromResult(result);
        }

        public Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            IEnumerable<EmailSendAttachment> adjuntos, string aliasRemitente = null)
        {
            return EnviarAsync(para, paraNombre, asunto, cuerpo, null, null, aliasRemitente);
        }

        public void Reset()
        {
            EnviosRealizados.Clear();
            SimularFallo = false;
        }
    }
}
