using CapaModelo;

namespace CapaNegocio.Interfaces
{
    public interface IDocumentoFirmaService
    {
        FirmaDocumentoResultado Firmar(FirmaDocumentoRequest request);
    }
}
