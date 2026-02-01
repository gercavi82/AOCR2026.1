using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa una orden de recaudación (tabla aocr_or_orden)
    /// </summary>
    [Table("aocr_or_orden")]
    public class OrdenRecaudacion
    {
        public OrdenRecaudacion()
        {
            Detalles = new List<DetalleOrden>();
            FechaCreacion = DateTime.Now;
            Estado = "BORRADOR";
            Activo = true;
        }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("codigo_usuario")]
        [StringLength(50)]
        public string CodigoUsuario { get; set; }

        [Column("codigo_solicitud")]
        [StringLength(50)]
        public string CodigoSolicitud { get; set; }

        [Column("numero_orden")]
        [StringLength(50)]
        public string NumeroOrden { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Column("estado")]
        [StringLength(50)]
        public string Estado { get; set; }

        [Column("observacion")]
        [StringLength(500)]
        public string Observacion { get; set; }

        [Column("subtotal")]
        public decimal? Subtotal { get; set; }

        [Column("admin")]
        public decimal? Admin { get; set; }

        [Column("total")]
        public decimal? Total { get; set; }

        [Column("lugar_emision")]
        [StringLength(200)]
        public string LugarEmision { get; set; }

        [Column("compania")]
        [StringLength(200)]
        public string Compania { get; set; }

        [Column("ruc_cedula")]
        [StringLength(20)]
        public string RucCedula { get; set; }

        [Column("correo")]
        [StringLength(100)]
        public string Correo { get; set; }

        [Column("telefono")]
        [StringLength(20)]
        public string Telefono { get; set; }

        [Column("concepto_id")]
        public int? ConceptoId { get; set; }

        // =====================================================
        // Propiedades adicionales requeridas por DAO/Controller
        // (Algunas son alias o campos extendidos no en BD)
        // =====================================================

        /// <summary>
        /// ID de la solicitud relacionada (alias de CodigoSolicitud para compatibilidad)
        /// </summary>
        [NotMapped]
        public int? SolicitudId
        {
            get
            {
                if (int.TryParse(CodigoSolicitud, out int id))
                    return id;
                return null;
            }
            set
            {
                CodigoSolicitud = value?.ToString();
            }
        }

        /// <summary>
        /// ID del contribuyente (se almacena en Compania/RucCedula)
        /// </summary>
        [NotMapped]
        public int? ContribuyenteId { get; set; }

        /// <summary>
        /// Nombre del contribuyente (alias de Compania)
        /// </summary>
        [NotMapped]
        public string NombreContribuyente
        {
            get { return Compania; }
            set { Compania = value; }
        }

        /// <summary>
        /// RUC del contribuyente (alias de RucCedula)
        /// </summary>
        [NotMapped]
        public string RucContribuyente
        {
            get { return RucCedula; }
            set { RucCedula = value; }
        }

        /// <summary>
        /// Email del contribuyente (alias de Correo)
        /// </summary>
        [NotMapped]
        public string EmailContribuyente
        {
            get { return Correo; }
            set { Correo = value; }
        }

        /// <summary>
        /// Observaciones (alias de Observacion)
        /// </summary>
        [NotMapped]
        public string Observaciones
        {
            get { return Observacion; }
            set { Observacion = value; }
        }

        /// <summary>
        /// IVA calculado
        /// </summary>
        [NotMapped]
        public decimal? Iva { get; set; }

        /// <summary>
        /// Usuario que creó la orden
        /// </summary>
        [NotMapped]
        public string UsuarioCreacion
        {
            get { return CodigoUsuario; }
            set { CodigoUsuario = value; }
        }

        /// <summary>
        /// Fecha de modificación
        /// </summary>
        [NotMapped]
        public DateTime? FechaModificacion { get; set; }

        /// <summary>
        /// Usuario que modificó la orden
        /// </summary>
        [NotMapped]
        public string UsuarioModificacion { get; set; }

        /// <summary>
        /// Indica si la orden está activa
        /// </summary>
        [NotMapped]
        public bool Activo { get; set; }

        /// <summary>
        /// Nombre del concepto (para mostrar en la vista)
        /// </summary>
        [NotMapped]
        public string ConceptoNombre { get; set; }

        /// <summary>
        /// Colección de detalles de la orden
        /// </summary>
        [NotMapped]
        public virtual List<DetalleOrden> Detalles { get; set; }
    }
}
