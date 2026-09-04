using CapaModelo;

namespace CapaNegocio.Interfaces
{
    public interface IAocrFinalWorkflowService
    {
        AocrWorkflowResult RemitirAocrDirdac(RemitirAocrDirdacRequest request);
        BandejaAocrDirdacViewModel ObtenerBandejaDirdac();
        DetalleAocrDirdacViewModel ObtenerDetalleDirdac(int solicitudId);
        BandejaAocrDirdacItemViewModel ObtenerContextoRemisionDircav(int solicitudId);
        AocrWorkflowResult DevolverAocrDircav(DevolverAocrDircavRequest request);
        AocrWorkflowResult FirmarLegalizarAocr(FirmarLegalizarAocrRequest request);
        AocrWorkflowResult EvaluarFirmasCompletas(int solicitudId, long versionEsperada, AocrWorkflowActor actor);
    }
}
