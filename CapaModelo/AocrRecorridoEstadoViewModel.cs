using System;

namespace CapaModelo
{
    public class AocrRecorridoEstadoViewModel
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int? OrdenRecaudacionId { get; set; }
        public int? InspeccionId { get; set; }
        public int? InformeId { get; set; }
        public string Fecha { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Etapa { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Accion { get; set; }
        public string RolUsuario { get; set; }
        public string Usuario { get; set; }
        public string RolResponsable { get; set; }
        public string Responsable { get; set; }
        public string Observacion { get; set; }
        public bool EsEstadoActual { get; set; }
    }
}
