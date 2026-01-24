using System.Collections.Generic;
using CapaModelo;

namespace CapaDatos.Repositories
{
    public interface IDocumentoRepository
    {
        int Crear(Documento documento);
        bool Actualizar(Documento documento);
        bool Eliminar(int id);
        Documento ObtenerPorId(int id);
        List<Documento> ObtenerPorSolicitud(int solicitudId);
        List<Documento> ObtenerPorTipo(int solicitudId, string tipoDocumento);
        List<Documento> ObtenerSubsanaciones(int solicitudId);
        List<Documento> ObtenerPorEstado(int solicitudId, string estado);
        bool ValidarDocumento(int documentoId, bool validado, string observaciones, string usuario);
    }
}