using System;

namespace CapaModelo
{
    public class AocrFirmaPosicionDocumento
    {
        public int CodigoPosicionFirma { get; set; }
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public string TipoDocumento { get; set; }
        public string RolFirmante { get; set; }
        public string OrigenPosicion { get; set; }
        public int NumeroPagina { get; set; }
        public decimal PosicionXRatio { get; set; }
        public decimal PosicionYRatio { get; set; }
        public decimal AnchoRatio { get; set; }
        public decimal AltoRatio { get; set; }
        public int? CodigoUsuario { get; set; }
        public string UsuarioNombre { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}