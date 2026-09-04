using System.Collections.Generic;
using CapaModelo;

namespace CapaDatos.Interfaces
{
    public interface IAocrFinalWorkflowRepository
    {
        AocrWorkflowResult RemitirAocrDirdac(RemitirAocrDirdacRequest request);
        AocrWorkflowResult DevolverAocrDircav(DevolverAocrDircavRequest request);
        AocrWorkflowResult FirmarLegalizarAocr(FirmarLegalizarAocrRequest request);
        AocrWorkflowResult EvaluarFirmasCompletas(int solicitudId, long versionEsperada, AocrWorkflowActor actor);
        IList<BandejaAocrDirdacItemViewModel> ListarBandejaDirdac();
        DetalleAocrDirdacViewModel ObtenerDetalleDirdac(int solicitudId);
        BandejaAocrDirdacItemViewModel ObtenerContextoRemisionDircav(int solicitudId);
    }
}
