using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapaDatos.Entidades;

namespace CapaDatos.Interfaces
{
    /// <summary>
    /// Interface para repositorio de Órdenes de recaudación
    /// </summary>
    public interface IOrdenRecaudacionRepository
    {
        // Consultas
        Task<OrdenRecaudacion> ObtenerPorIdAsync(int id);
        Task<IEnumerable<OrdenRecaudacion>> ObtenerTodosAsync();
        Task<IEnumerable<OrdenRecaudacion>> ObtenerPorEstadoAsync(string estado);
        Task<int> ObtenerConsecutivoDiarioAsync(DateTime fecha);

        // Crear
        Task<int> CrearAsync(OrdenRecaudacion orden);
        Task CrearDetalleAsync(DetalleOrden detalle);

        // Actualizar
        Task<bool> ActualizarAsync(OrdenRecaudacion orden);
        Task<bool> ActualizarEstadoAsync(int id, string nuevoEstado, string usuario);

        // Eliminar (lógico)
        Task<bool> EliminarAsync(int id, string usuario);
    }
}
