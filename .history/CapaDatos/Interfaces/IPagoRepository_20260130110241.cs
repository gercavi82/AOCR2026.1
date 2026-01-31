using System.Collections.Generic;
using System.Threading.Tasks;
using CapaDatos.Entidades;

namespace CapaDatos.Interfaces
{
    /// <summary>
    /// Interface para repositorio de Pagos
    /// </summary>
    public interface IPagoRepository
    {
        Task<int> CrearAsync(Pago pago);
        Task<Pago> ObtenerPorIdAsync(int id);
        Task<Pago> ObtenerPorOrdenIdAsync(int ordenId);
        Task<IEnumerable<Pago>> ObtenerPorEstadoAsync(string estado);
        Task<bool> ActualizarAsync(Pago pago);
        Task<bool> ActualizarEstadoAsync(int id, string nuevoEstado, string usuario);
    }
}
