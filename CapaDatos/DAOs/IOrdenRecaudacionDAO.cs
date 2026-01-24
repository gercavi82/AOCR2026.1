using System.Collections.Generic;
using System.Data;
using CapaDatos.Models;
using CapaModelo.DTOs;

namespace CapaDatos.DAOs
{
    public interface IOrdenRecaudacionDAO
    {
        // Validaciones flujo
        bool ExisteORGeneradaOPagada(int codigoUsuario);
        bool ExisteORMinima(int codigoUsuario);

        // CRUD principal
        List<OrdenRecaudacionModel> ObtenerOrdenes(int? codigoUsuario, string estado);
        OrdenRecaudacionModel ObtenerOrdenPorId(int id);
        int CrearOrden(OrdenRecaudacionModel orden);
        bool ActualizarOrden(OrdenRecaudacionModel orden);
        bool CambiarEstadoOrden(int id, string nuevoEstado);

        // Buscar / Estadísticas / Pagos
        List<OrdenRecaudacionModel> BuscarOrdenes(string criterio, int? codigoUsuario);
        Dictionary<string, object> ObtenerEstadisticas(int codigoUsuario);
        bool RegistrarPago(int idOrden, PagoModel pago);

        // Para Dashboard/Orden (si aún lo usas en DataTable)
        DataTable ObtenerOrdenesPorUsuario(int codigoUsuario);

        // Para PDF
        OrdenRecaudacionPdfDto ObtenerDatosParaPdf(int ordenId, int usuarioId);

        // Wrappers BL (para que compile cuando llamas _dao.ListarPorUsuario / ObtenerPorId / etc.)
        List<OrdenRecaudacionModel> ListarPorUsuario(int codigoUsuario, string estado);
        OrdenRecaudacionModel ObtenerPorId(int id);
        int Insertar(OrdenRecaudacionModel orden);
        bool Actualizar(OrdenRecaudacionModel orden);
        bool CambiarEstado(int id, string estado);
    }
}
