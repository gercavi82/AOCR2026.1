using System.Collections.Generic;
using CapaModelo;

namespace CapaDatos.Repositories
{
    public interface ISolicitudRepository
    {
        int Crear(SolicitudAOCR solicitud);
        bool Actualizar(SolicitudAOCR solicitud);
        bool Eliminar(int id);
        SolicitudAOCR ObtenerPorId(int id);
        List<SolicitudAOCR> ObtenerTodas();
        List<SolicitudAOCR> ObtenerPorUsuario(int usuarioId);
        List<SolicitudAOCR> ObtenerPorEstado(string estado);
        List<object> ObtenerHistorialEstados(int solicitudId);
    }
}