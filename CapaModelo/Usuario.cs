using System;

namespace CapaModelo
{
    public class Usuario
    {
        public int Id { get; set; } // Usaremos Id como estándar
        public string NombreUsuario { get; set; }
        public string Email { get; set; }
        public string Contrasena { get; set; }
        public string NombreCompleto { get; set; }
        public string Rol { get; set; }
        public bool Activo { get; set; }
        public DateTime? FechaCreacion { get; set; }
        public DateTime? FechaUltimaConexion { get; set; }
        public bool MustChangePassword { get; set; }
        public string CodigoUsuario { get; set; }
        public int IdUsuario { get; set; }
        public string ApellidoUsuario { get; set; }
        
        // Campos adicionales para registro
        public string EmpresaCodigo { get; set; }
        public string RutaDocumentoLegal { get; set; }
        public string EstadoDesignacionRT { get; set; }
        public string RutaConstanciaRT { get; set; }
        public DateTime? FechaRevisionDesignacion { get; set; }
        public string Ruc { get; set; }

        // Métricas para gestión RT (evita intentar eliminar usuarios con datos operativos asociados)
        public int OrdenesRecaudacionCount { get; set; }
        public int DocumentosSubsanacionCount { get; set; }
        public int SubsanacionesCount { get; set; }
        public int TotalRelacionesBloqueantes { get; set; }
        public bool PuedeEliminarRT => TotalRelacionesBloqueantes <= 0;
    }
}
