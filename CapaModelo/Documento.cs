using System;

namespace CapaModelo
{
    public class Documento
    {
        public int CodigoDocumento { get; set; }
        public int CodigoSolicitud { get; set; }

        public string TipoDocumento { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaGuardada { get; set; }

        public string Extension { get; set; }
        public long? TamanoBytes { get; set; }

        public string Estado { get; set; }
        public bool? Validado { get; set; }

        public DateTime? FechaCarga { get; set; }
        public string Observaciones { get; set; }

        public int? Version { get; set; }

        public string UsuarioRegistro { get; set; }

        // ====== ALIAS para compatibilidad con código viejo ======
        public string RutaArchivo
        {
            get => RutaGuardada;
            set => RutaGuardada = value;
        }

        public long? TamanioArchivo
        {
            get => TamanoBytes;
            set => TamanoBytes = value;
        }

        public DateTime? FechaSubida
        {
            get => FechaCarga;
            set => FechaCarga = value;
        }

        public string ExtensionArchivo
        {
            get => Extension;
            set => Extension = value;
        }

    }
}
