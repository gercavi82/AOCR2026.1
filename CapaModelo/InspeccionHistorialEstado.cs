using System;

namespace CapaModelo
{
    public class InspeccionHistorialEstado
    {
        public int CodigoHistorial { get; set; }
        public int CodigoInspeccion { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Observacion { get; set; }
        public string Origen { get; set; }
        public int? CodigoUsuario { get; set; }
        public string UsuarioNombre { get; set; }
        public DateTime FechaCambio { get; set; }
    }
}