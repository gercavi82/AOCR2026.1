using System;

namespace CapaDatos.Models
{
    public class ResultadoTransicion
    {
        public bool Exitoso { get; set; }
        public string Codigo { get; set; }
        public string Mensaje { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public bool YaProcesado { get; set; }
        public AocrProcesoEstadoRecord EstadoActual { get; set; }
    }
}
