using CapaNegocio.DTOs;

namespace CapaNegocio.Services
{
    public interface IHabilitacionDocumentosFinalesService
    {
        ResultadoHabilitacionDocumentos Habilitar(HabilitarDocumentosRequest request);
    }
}
