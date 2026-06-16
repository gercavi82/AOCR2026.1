using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using CapaDatos.Constants;

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
            Estado = EstadoOrden.Borrador;
            Activo = true;
            ContribuyenteId = 0;
        }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Código del usuario que creó la orden
        /// </summary>
        [Column("codigo_usuario")]
        public int? CodigoUsuario { get; set; }

        /// <summary>
        /// Código de la solicitud asociada
        /// </summary>
        [Column("codigo_solicitud")]
        public int? CodigoSolicitud { get; set; }

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

        /// <summary>
        /// C?digo OACI en memoria (no persistido; la tabla aocr_or_orden solo tiene columna compania).
        /// </summary>
        [NotMapped]
        public string CompaniaCodigo { get; set; }

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
            get => CodigoSolicitud;
            set => CodigoSolicitud = value;
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
            get => CodigoUsuario?.ToString();
            set => CodigoUsuario = int.TryParse(value, out var id) ? (int?)id : null;
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

        /// <summary>
        /// Calcula los totales de la orden basándose en los detalles
        /// </summary>
        public void CalcularTotales()
        {
            if (Detalles == null || Detalles.Count == 0)
            {
                Subtotal = 0;
                Admin = 0;
                Total = 0;
                return;
            }

            // Calcular subtotal y admin desde detalles
            Subtotal = Detalles.Sum(d => d.Subtotal);
            Admin = Detalles.Sum(d => d.Admin);
            Total = Subtotal + Admin;
        }

        /// <summary>
        /// Valida que la orden tenga los datos mínimos requeridos
        /// </summary>
        public bool EsValida()
        {
            // Validar datos básicos
            if (CodigoUsuario == null || CodigoUsuario <= 0)
                return false;

            if (CodigoSolicitud == null || CodigoSolicitud <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(NumeroOrden))
                return false;

            if (string.IsNullOrWhiteSpace(Compania))
                return false;

            if (string.IsNullOrWhiteSpace(RucCedula))
                return false;

            // Validar que tenga al menos un detalle
            if (Detalles == null || Detalles.Count == 0)
                return false;

            // Validar que todos los detalles sean válidos
            return Detalles.All(d => d.EsValido());
        }
    }
}
