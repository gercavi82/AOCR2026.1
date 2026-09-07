using CapaDatos.Catalogos;

namespace CapaNegocio.Services
{
    // Compatibilidad para los consumidores de AC-07. El catalogo y la hidratacion
    // se comparten con persistencia para que ningun cierre omita su validacion.
    public class ListaVerificacionCatalogService : ListaVerificacionCatalogo
    {
    }
}
