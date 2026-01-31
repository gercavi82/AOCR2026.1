// Stub mínimo para compilar si la referencia a SelectPdf no está presente.
// Reemplaza con el paquete real en producción.
using System.IO;

namespace SelectPdf
{
    public class PdfDocument
    {
        public void Save(string path) { File.WriteAllBytes(path, new byte[0]); }
        public void Save(Stream stream) { /* no-op stub */ }
    }

    public class HtmlToPdf
    {
        public PdfDocument ConvertHtmlString(string html)
        {
            return new PdfDocument();
        }
    }
}
