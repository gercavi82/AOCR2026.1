using System;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad para registrar metadatos de archivos subidos de forma segura
    /// </summary>
    public class ArchivoSubido
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Nombre original del archivo (para mostrar al usuario)
        /// </summary>
        public string NombreOriginal { get; set; }
        
        /// <summary>
        /// Nombre seguro generado (GUID + timestamp)
        /// </summary>
        public string NombreSeguro { get; set; }
        
        /// <summary>
        /// Ruta relativa donde está almacenado
        /// </summary>
        public string RutaAlmacenamiento { get; set; }
        
        /// <summary>
        /// Tipo MIME detectado por magic bytes
        /// </summary>
        public string TipoMime { get; set; }
        
        /// <summary>
        /// Extensión del archivo
        /// </summary>
        public string Extension { get; set; }
        
        /// <summary>
        /// Tamaño en bytes
        /// </summary>
        public long TamanoBytes { get; set; }
        
        /// <summary>
        /// Hash SHA256 del contenido
        /// </summary>
        public string HashSha256 { get; set; }
        
        /// <summary>
        /// Entidad relacionada (ej. "OrdenRecaudacion", "Pago")
        /// </summary>
        public string EntidadRelacionada { get; set; }
        
        /// <summary>
        /// ID de la entidad relacionada
        /// </summary>
        public int EntidadId { get; set; }
        
        /// <summary>
        /// Fecha de subida
        /// </summary>
        public DateTime FechaSubida { get; set; }
        
        /// <summary>
        /// Usuario que subió el archivo
        /// </summary>
        public string UsuarioSubida { get; set; }
        
        /// <summary>
        /// Indica si el archivo está activo o fue eliminado lógicamente
        /// </summary>
        public bool Activo { get; set; }
        
        /// <summary>
        /// IP desde donde se subió
        /// </summary>
        public string IpOrigen { get; set; }
    }
}
