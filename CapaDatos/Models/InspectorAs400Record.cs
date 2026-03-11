using System;

namespace CapaDatos.Models
{
    public class InspectorAs400Record
    {
        public string Cedula { get; set; }
        public string NombreCompleto { get; set; }
        public string Estado { get; set; }
        public string Tipo { get; set; }

        public string EtiquetaLista
        {
            get
            {
                var cedula = (Cedula ?? string.Empty).Trim();
                var nombre = (NombreCompleto ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(nombre) && string.IsNullOrWhiteSpace(cedula))
                {
                    return string.Empty;
                }

                if (string.IsNullOrWhiteSpace(nombre))
                {
                    return cedula;
                }

                if (string.IsNullOrWhiteSpace(cedula))
                {
                    return nombre;
                }

                return nombre + " - " + cedula;
            }
        }

        public bool EsActivo =>
            string.Equals((Estado ?? string.Empty).Trim(), "AC", StringComparison.OrdinalIgnoreCase);
    }
}
