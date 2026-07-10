using System;

namespace CapaModelo
{
    public class ResultadoDocumento
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public AocrDocumentoGenerado Documento { get; set; }
    }
}
