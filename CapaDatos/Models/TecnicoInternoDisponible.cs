namespace CapaDatos.Models
{
    public class TecnicoInternoDisponible
    {
        public int CodigoTecnico { get; set; }
        public int? UsuarioId { get; set; }
        public string CodigoUsuario { get; set; }
        public string Identificacion { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string NombreCompleto { get; set; }
        public string CorreoActual { get; set; }
        public string Especialidad { get; set; }
        public bool Activo { get; set; }
        public bool YaVinculado { get; set; }
    }
}
