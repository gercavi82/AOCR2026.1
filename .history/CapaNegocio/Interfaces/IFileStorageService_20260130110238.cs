using System.Threading.Tasks;

namespace CapaNegocio.Interfaces
{
    /// <summary>
    /// Interface para servicio de almacenamiento de archivos
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Guarda un archivo y retorna la ruta
        /// </summary>
        Task<string> GuardarArchivoAsync(string directorio, string nombreArchivo, byte[] contenido);

        /// <summary>
        /// Obtiene el contenido de un archivo
        /// </summary>
        Task<byte[]> ObtenerArchivoAsync(string ruta);

        /// <summary>
        /// Elimina un archivo
        /// </summary>
        Task<bool> EliminarArchivoAsync(string ruta);

        /// <summary>
        /// Verifica si un archivo existe
        /// </summary>
        Task<bool> ExisteArchivoAsync(string ruta);
    }
}
