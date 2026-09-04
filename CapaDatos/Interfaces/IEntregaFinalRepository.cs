using System.Collections.Generic;
using CapaModelo;

namespace CapaDatos.Interfaces
{
    public interface IEntregaFinalRepository
    {
        EntregaFinalResult Solicitar(SolicitarEntregaFinalRequest request);
        IList<DocumentoFinalDisponibleViewModel> ListarDocumentos(EntregaFinalActor actor);
        DescargaFinalAutorizada AutorizarDescarga(int documentoId, EntregaFinalActor actor);
        IList<EstadoEntregaFinalViewModel> ConsultarEstados(int? solicitudId);
        void ActualizarDesdeCola(int emailQueueId, string estadoCola, string messageId, string error);
    }
}
