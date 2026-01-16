using System;

namespace CapaModelo
{
    public class SolicitudAOCR
    {
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public DateTime? FechaSolicitud { get; set; }
        public int? TipoSolicitud { get; set; }
        public string Estado { get; set; }

        public string NombreOperador { get; set; }
        public string Ruc { get; set; }
        public string RazonSocial { get; set; }

        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Direccion { get; set; }
        public string Ciudad { get; set; }
        public string Provincia { get; set; }
        public string Pais { get; set; }

        public string RepresentanteLegal { get; set; }
        public string CedulaRepresentante { get; set; }

        public string TipoOperacion { get; set; }
        public string DescripcionOperacion { get; set; }
        public string Observaciones { get; set; }

        public int CodigoUsuario { get; set; }
        public int? CodigoTecnico { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }

        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public DateTime? FechaInicioOperacion { get; set; }
        public DateTime? FechaFinOperacion { get; set; }
        public string ObservacionesGenerales { get; set; }

    }
}
