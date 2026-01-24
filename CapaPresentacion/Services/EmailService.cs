using CapaDatos.Models;

namespace CapaPresentacion.Services
{
    public class EmailService
    {
        public bool EnviarOrdenRecaudacion(OrdenRecaudacionModel orden, byte[] pdfBytes)
        {
            // TODO: Implementar SMTP real.
            // Por ahora es “mínimo profesional” para compilar y luego conectas correo.
            return false;
        }
    }
}
