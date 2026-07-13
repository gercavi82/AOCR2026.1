using CapaNegocio.DTOs;

namespace CapaNegocio.Services
{
    public interface IFirmaDocumentoInstitucionalService
    {
        ResultadoFirmaDocumento Firmar(FirmarDocumentoInstitucionalRequest request);
        ResultadoValidacionFirma ValidarFirma(int solicitudId, int inspeccionId, string tipoDocumento, int usuarioId);
        EstadoFirmasExpedienteDto ObtenerEstadoFirmas(int solicitudId, int inspeccionId);
    }
}
