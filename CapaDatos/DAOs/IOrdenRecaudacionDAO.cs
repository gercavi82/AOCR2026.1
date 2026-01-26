using System.Collections.Generic;
using System.Data;
using CapaDatos.Models;
using CapaModelo.DTOs;

namespace CapaDatos.DAOs
{
    public interface IOrdenRecaudacionDAO
    {
        // ===================== Validaciones de flujo =====================
        bool ExisteORGeneradaOPagada(int codigoUsuario);
        bool ExisteORMinima(int codigoUsuario);

        // ===================== Dashboard / Listados ======================
        List<OrdenRecaudacionModel> ListarPorUsuario(int codigoUsuario, string estado);

        // ===================== CRUD principal ============================
        List<OrdenRecaudacionModel> ObtenerOrdenes(int? codigoUsuario, string estado);
        OrdenRecaudacionModel ObtenerOrdenPorId(int id);
        int CrearOrden(OrdenRecaudacionModel orden);
        bool ActualizarOrden(OrdenRecaudacionModel orden);
        bool CambiarEstadoOrden(int id, string nuevoEstado);

        // ===================== Buscar / Estadísticas / Pagos ==============
        List<OrdenRecaudacionModel> BuscarOrdenes(string criterio, int? codigoUsuario);
        Dictionary<string, object> ObtenerEstadisticas(int codigoUsuario);
        bool RegistrarPago(int idOrden, PagoModel pago);

        // ===================== Legacy (si aún existe código viejo) ========
        DataTable ObtenerOrdenesPorUsuario(int codigoUsuario);

        // ===================== PDF =======================================
        OrdenRecaudacionPdfDto ObtenerDatosParaPdf(int ordenId, int usuarioId);

        // ===================== Wrappers de compatibilidad =================
        // (Para que compile si en otros lugares llamas: ObtenerPorId/Insertar/Actualizar/CambiarEstado)
        OrdenRecaudacionModel ObtenerPorId(int id);
        int Insertar(OrdenRecaudacionModel orden);
        bool Actualizar(OrdenRecaudacionModel orden);
        bool CambiarEstado(int id, string estado);
    }
}
