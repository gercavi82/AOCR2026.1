using System;

namespace CapaModelo
{
    public class InspeccionInformeTecnico
    {
        public int CodigoInforme { get; set; }
        public int CodigoInspeccion { get; set; }
        public int Version { get; set; }
        public string Titulo { get; set; }
        public string Resumen { get; set; }
        public string Resultado { get; set; }
        public string Observaciones { get; set; }
        public string Conclusiones { get; set; }
        public string Recomendaciones { get; set; }
        public string RutaPdf { get; set; }
        public bool Finalizado { get; set; }
        public bool CorreoEnviado { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }
}