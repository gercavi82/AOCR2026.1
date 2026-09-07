using System;

namespace CapaModelo.RT
{
    /// <summary>
    /// Resultado de la validación contextual de correo de Representante Técnico (AC-01).
    /// Evalúa la relación: RT + Compañía + Trámite/Designación + Estado.
    /// </summary>
    public class ResultadoValidacionCorreoRT
    {
        public bool Valido { get; set; }
        public string Mensaje { get; set; }
        public bool EsReutilizable { get; set; }
        public int? UsuarioIdExistente { get; set; }
        public string CodigoUsuarioExistente { get; set; }
        public string EstadoDesignacionExistente { get; set; }
        public bool UsuarioActivoExistente { get; set; }
        public bool MismaPersona { get; set; }
        public bool MultiCompaniaPermitida { get; set; }
    }
}
