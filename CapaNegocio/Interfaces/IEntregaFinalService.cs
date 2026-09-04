using System.Collections.Generic;
using CapaModelo;

namespace CapaNegocio.Interfaces
{
    public interface IEntregaFinalService
    {
        EntregaFinalResult Solicitar(SolicitarEntregaFinalRequest request);
        DocumentosFinalesViewModel ListarDocumentos(EntregaFinalActor actor);
        DescargaFinalAutorizada AutorizarDescarga(int documentoId, EntregaFinalActor actor);
        IList<EstadoEntregaFinalViewModel> ConsultarEstados(EntregaFinalActor actor, int? solicitudId);
    }
}
