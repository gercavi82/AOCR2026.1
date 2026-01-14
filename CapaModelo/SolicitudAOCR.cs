using System;
using System.Collections.Generic;

namespace CapaModelo
{
    public class SolicitudAOCR
    {
        // ==========================================
        // Identificación y Control (FIX CS0428 y CS0411)
        // ==========================================
        public int CodigoSolicitud { get; set; }

        // Alias para compatibilidad con código que busca ".Id"
        public int Id { get { return CodigoSolicitud; } set { CodigoSolicitud = value; } }

        public string NumeroSolicitud { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public int TipoSolicitud { get; set; }
        public string Estado { get; set; }

        // ==========================================
        // Datos del Operador (FIX CS1061 - Ruc)
        // ==========================================
        public string NombreOperador { get; set; }
        public string Ruc { get; set; } // Nombre exacto para la vista

        // Alias para compatibilidad con código que busca ".RUC" (Mayúsculas)
        public string RUC { get { return Ruc; } set { Ruc = value; } }

        public string RazonSocial { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Direccion { get; set; }
        public string Ciudad { get; set; }
        public string Provincia { get; set; }
        public string Pais { get; set; }

        // ==========================================
        // Representante y Operación (FIX CS1061 - Matricula)
        // ==========================================
        public string RepresentanteLegal { get; set; }
        public string CedulaRepresentante { get; set; }
        public string TipoOperacion { get; set; }
        public string DescripcionOperacion { get; set; }

        // Propiedad faltante requerida por la Vista
        public string Matricula { get; set; }

        public DateTime? FechaInicioOperacion { get; set; }
        public DateTime? FechaFinOperacion { get; set; }
        public string ObservacionesGenerales { get; set; }
        public string Observaciones { get; set; }
        public DateTime? FechaRegistro { get; set; }

        // ==========================================
        // Auditoría y Control (FIX CS1061 - FechaActualizacion)
        // ==========================================
        public int CodigoUsuario { get; set; }
        public int? CodigoTecnico { get; set; }

        // Propiedad requerida por SolicitudAOCRBL.cs
        public DateTime? FechaActualizacion { get { return UpdatedAt; } set { UpdatedAt = value; } }

        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }

        // ==========================================
        // Propiedades de Ayuda para la Vista (Mantenidas)
        // ==========================================
        public string NombreRepresentante { get { return RepresentanteLegal; } set { RepresentanteLegal = value; } }
        public string RucRepresentante { get { return CedulaRepresentante; } set { CedulaRepresentante = value; } }
        public string DireccionEcuador { get { return Direccion; } set { Direccion = value; } }
        public string Banco { get; set; }
        public string NumComp { get; set; }
        public string UsuarioRevisor { get; set; }
        public string ObservacionesInspector { get; set; }
        public string ObservacionesDirector { get; set; } // Añadida para flujo de Dirección
        public DateTime? FechaRevisionInspector { get; set; }
    }
}