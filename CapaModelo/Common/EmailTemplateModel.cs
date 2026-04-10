using System.Collections.Generic;

namespace CapaModelo.Common
{
    public class EmailTemplateModel
    {
        public string Titulo { get; set; }
        public string NombreDestinatario { get; set; }
        public string MensajePrincipal { get; set; }
        public List<EmailFieldItem> Resumen { get; set; }
        public string Observaciones { get; set; }
        public string EnlaceUrl { get; set; }
        public string EnlaceTexto { get; set; }
        public string TextoCierre { get; set; }
        public string Footer { get; set; }

        /// <summary>
        /// Contenido HTML libre que se inserta despues del resumen y antes del cierre.
        /// Usar solo cuando el bloque estandar de resumen no es suficiente
        /// (por ejemplo, credenciales con caja destacada).
        /// </summary>
        public string ContenidoHtmlExtra { get; set; }

        public EmailTemplateModel()
        {
            Resumen = new List<EmailFieldItem>();
            TextoCierre = "Puede revisar el detalle desde el sistema AOCR.";
            Footer = "Este es un mensaje automatico del sistema AOCR.";
        }
    }

    public class EmailFieldItem
    {
        public string Label { get; set; }
        public string Value { get; set; }

        public EmailFieldItem() { }

        public EmailFieldItem(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }
}
