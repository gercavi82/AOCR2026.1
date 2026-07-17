using System;
using CapaModelo;

namespace CapaNegocio.Interfaces
{
    public interface IFinalizacionInstitucionalService
    {
        FirmaDocumentoResultado Finalizar(int solicitudId, int usuarioId, string tipoDocumentoActual, Func<string, bool> rutaExiste);
    }
}
