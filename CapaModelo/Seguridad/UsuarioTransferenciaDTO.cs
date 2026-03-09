using System.Collections.Generic;

namespace CapaModelo.Seguridad
{
    public class UsuarioReferenciaImpactoDTO
    {
        public string Grupo { get; set; }
        public string Tabla { get; set; }
        public string Campo { get; set; }
        public string Descripcion { get; set; }
        public string Estrategia { get; set; }
        public bool Transferible { get; set; }
        public int RegistrosDetectados { get; set; }
        public int RegistrosAfectados { get; set; }
        public string Observacion { get; set; }
    }

    public class UsuarioTransferenciaPreviewDTO
    {
        public int UsuarioOrigenId { get; set; }
        public string UsuarioOrigenCodigo { get; set; }
        public int TotalRegistrosDetectados { get; set; }
        public int TotalRegistrosTransferibles { get; set; }
        public int TotalRegistrosHistoricos { get; set; }
        public IList<UsuarioReferenciaImpactoDTO> Referencias { get; set; } = new List<UsuarioReferenciaImpactoDTO>();
    }

    public class UsuarioTransferenciaResultadoDTO
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public long TransferenciaId { get; set; }
        public int UsuarioOrigenId { get; set; }
        public int UsuarioDestinoId { get; set; }
        public int TotalRegistrosDetectados { get; set; }
        public int TotalRegistrosTransferidos { get; set; }
        public bool UsuarioOrigenDesactivado { get; set; }
        public IList<UsuarioReferenciaImpactoDTO> Referencias { get; set; } = new List<UsuarioReferenciaImpactoDTO>();
    }
}
