using CapaDatos.Entidades;

namespace CapaDatos.Interfaces
{
    public interface IFr3OutboxDAO
    {
        bool EncolarEvento(Fr3OutboxEvent evento);
    }
}
