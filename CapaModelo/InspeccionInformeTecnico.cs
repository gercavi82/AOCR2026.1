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
        public string Antecedentes { get; set; }
        public string Alcance { get; set; }
        public string Desarrollo { get; set; }
        public string Evidencias { get; set; }
        public string NumeroLicenciaInspector { get; set; }
        public string TrabajosRealizados { get; set; }
        public string FechasInspeccionManual { get; set; }
        public string EstacionesInspeccionManual { get; set; }
        public string OperacionComercial { get; set; }
        public string ServiciosEstaciones { get; set; }
        public string Notas { get; set; }
        public string NoConformidades { get; set; }
        public string DocumentosAdjuntos { get; set; }
        public string DocumentosAdjuntosArchivos { get; set; }
        public string OtrosAdjuntos { get; set; }
        public string Resultado { get; set; }
        public string TipoResultadoInsatisfactorio { get; set; }
        public string Observaciones { get; set; }
        public string Conclusiones { get; set; }
        public string Recomendaciones { get; set; }
        public string RutaPdf { get; set; }
        public string EstadoInforme { get; set; }
        public bool FirmadoInspector { get; set; }
        public bool FirmadoDirdac { get; set; }
        public string RutaDocumentoFirmado { get; set; }
        public string HashDocumento { get; set; }
        public DateTime? FechaFirma1 { get; set; }
        public DateTime? FechaFirma2 { get; set; }
        public string UsuarioFirma1 { get; set; }
        public string UsuarioFirma2 { get; set; }
        public DateTime? FechaEnvioDirdac { get; set; }
        public string UsuarioEnvioDirdac { get; set; }
        public bool Finalizado { get; set; }
        public bool CorreoEnviado { get; set; }
        public bool NotificadoRt { get; set; }
        public DateTime? FechaNotificacionRt { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public string ObservacionDevolucion { get; set; }
        public DateTime? FechaDevolucion { get; set; }
        public string UsuarioDevolucion { get; set; }
    }
}
