using System;

namespace CapaDatos.Entidades
{
    public class Fr3OutboxEvent
    {
        public int Id { get; set; }
        public string EventKey { get; set; }
        public int OrdenId { get; set; }
        public int? PagoId { get; set; }
        public string Estado { get; set; }
        public int Intentos { get; set; }
        public DateTime? ProximoIntento { get; set; }
        public string WorkerId { get; set; }
        public DateTime? LockUntil { get; set; }
        public string Payload { get; set; }
        public string ErrorLast { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
