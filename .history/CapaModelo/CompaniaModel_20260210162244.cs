using System;

namespace CapaModelo
{
    public class CompaniaModel
    {
        public int Id { get; set; }
        public string RazonSocial { get; set; }
        public string Ruc { get; set; }
        public string Telefono { get; set; }
        public string EmailContacto { get; set; }
        public string AreaContableJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}