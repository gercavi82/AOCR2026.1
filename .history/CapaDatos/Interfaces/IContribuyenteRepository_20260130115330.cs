using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapaDatos.Interfaces
{
    public interface IContribuyenteRepository
    {
        Task<IEnumerable<object>> ObtenerTodosAsync();
        Task<object> ObtenerPorIdAsync(int id);
        Task<object> ObtenerPorRucAsync(string ruc);
    }
}
