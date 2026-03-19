using System;

namespace CapaModelo
{
    public class UsuarioCompaniaRT
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string CompaniaCodigo { get; set; }
        public string CompaniaNombre { get; set; }
        public string Usuoid { get; set; }
        public bool Activo { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }
}
