using System.Data;
using CapaModelo.DTOs;

namespace CapaDatos.DAOs
{
    public interface IOrdenRecaudacionDAO
    {
        bool ExisteORMinima(int usuarioId);
        bool ExisteORGeneradaOPagada(int usuarioId);
        bool ConceptoExiste(string conceptoCodigo);

        int InsertarOrdenAOCR(int idUsuario, string codigoSolicitud, int conceptoId, int estaciones, int dias, string obs);

        decimal ObtenerValorConceptoPorId(int conceptoId);

        DataTable ObtenerConceptosActivos();
        DataTable ObtenerOrdenesPorUsuario(int usuarioId);

        OrdenRecaudacionPdfDto ObtenerDatosParaPdf(int ordenId, int usuarioId);
        byte[] GenerarPDFOrden(int ordenId, int usuarioId);
    }
}
