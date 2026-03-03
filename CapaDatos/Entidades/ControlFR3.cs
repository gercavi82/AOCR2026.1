using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa un control FR3 de vuelos charter/especiales (tabla aocr_control_fr3)
    /// Migrada desde SistemaGestionCalidad (tabla OPCAR5 en DB2/AS400) a PostgreSQL
    /// </summary>
    [Table("aocr_control_fr3")]
    public class ControlFR3
    {
        public ControlFR3()
        {
            Estado = "E"; // Estado inicial: Emitido
            Procesado = "E";
            FechaCreacion = DateTime.Now;
        }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("secuencial")]
        public decimal Secuencial { get; set; }

        [Column("aeropuerto")]
        [StringLength(10)]
        public string Aeropuerto { get; set; }

        [Column("anio")]
        [StringLength(4)]
        public string Anio { get; set; }

        [Column("fecha_control_vuelo")]
        [StringLength(10)]
        public string FechaControlVuelo { get; set; }

        [Column("tipo_operacion")]
        [StringLength(20)]
        public string TipoOperacion { get; set; }

        [Column("ruta_total_plan_vlo")]
        [StringLength(200)]
        public string RutaTotalPlanVlo { get; set; }

        [Column("num_aterriza_pais")]
        public int NumAterrizaPais { get; set; }

        [Column("subtotal")]
        public decimal SubTotal { get; set; }

        [Column("valor_charter")]
        public decimal ValorCharter { get; set; }

        [Column("total")]
        public decimal Total { get; set; }

        [Column("gran_total")]
        public decimal GranTotal { get; set; }

        [Column("gran_total_letras")]
        [StringLength(500)]
        public string GranTotalLetras { get; set; }

        [Column("autorizacion")]
        [StringLength(100)]
        public string Autorizacion { get; set; }

        [Column("observacion")]
        [StringLength(500)]
        public string Observacion { get; set; }

        [Column("oid_cia_aviacion")]
        public decimal OidCiaAviacion { get; set; }

        [Column("oid_ubicacion")]
        public decimal OidUbicacion { get; set; }

        [Column("origen")]
        [StringLength(100)]
        public string Origen { get; set; }

        [Column("destino")]
        [StringLength(100)]
        public string Destino { get; set; }

        [Column("retorno")]
        [StringLength(100)]
        public string Retorno { get; set; }

        [Column("callsign")]
        [StringLength(50)]
        public string Callsign { get; set; }

        [Column("estado")]
        [StringLength(10)]
        public string Estado { get; set; }

        [Column("ruc")]
        [StringLength(20)]
        public string Ruc { get; set; }

        [Column("email")]
        [StringLength(100)]
        public string Email { get; set; }

        [Column("nac_inter")]
        [StringLength(5)]
        public string NacInter { get; set; }

        [Column("usuario_cr")]
        [StringLength(50)]
        public string UsuarioCr { get; set; }

        [Column("fecha_cr")]
        [StringLength(10)]
        public string FechaCr { get; set; }

        [Column("hora_cr")]
        [StringLength(10)]
        public string HoraCr { get; set; }

        [Column("id_aeropuerto")]
        public decimal IdAeropuerto { get; set; }

        [Column("telefono")]
        [StringLength(20)]
        public string Telefono { get; set; }

        [Column("nombre_cliente")]
        [StringLength(200)]
        public string NombreCliente { get; set; }

        [Column("direccion")]
        [StringLength(300)]
        public string Direccion { get; set; }

        [Column("oid_ubicacion_cliente")]
        public decimal OidUbicacionCliente { get; set; }

        [Column("forma_pago")]
        [StringLength(50)]
        public string FormaPago { get; set; }

        [Column("nombre_cia")]
        [StringLength(200)]
        public string NombreCia { get; set; }

        [Column("modelo")]
        [StringLength(100)]
        public string Modelo { get; set; }

        [Column("peso_matricula")]
        public decimal PesoMatricula { get; set; }

        [Column("codigo_oaci_cia")]
        [StringLength(20)]
        public string CodigoOACICia { get; set; }

        [Column("nombre_aeropuerto")]
        [StringLength(200)]
        public string NombreAeropuerto { get; set; }

        [Column("email_usuario_dgac")]
        [StringLength(100)]
        public string EmailUsuarioDGAC { get; set; }

        [Column("matricula")]
        [StringLength(50)]
        public string Matricula { get; set; }

        [Column("procesado")]
        [StringLength(5)]
        public string Procesado { get; set; }

        [Column("valor_total_millas")]
        public decimal ValorTotalMillas { get; set; }

        [Column("fecha_recepcion")]
        [StringLength(10)]
        public string FechaRecepcion { get; set; }

        [Column("codigo_banco")]
        [StringLength(20)]
        public string CodigoBanco { get; set; }

        [Column("deposito")]
        [StringLength(50)]
        public string Deposito { get; set; }

        [Column("numero_factura")]
        [StringLength(50)]
        public string NumeroFactura { get; set; }

        [Column("tipo_tramite")]
        [StringLength(50)]
        public string TipoTramite { get; set; }

        [Column("nombre_archivo_factura")]
        [StringLength(200)]
        public string NombreArchivoFactura { get; set; }

        // =============================
        // Campos de auditoría PostgreSQL
        // =============================
        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Column("fecha_actualizacion")]
        public DateTime? FechaActualizacion { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        // =============================
        // Relación con detalle
        // =============================
        [NotMapped]
        public ControlFR3Detalle Detalle { get; set; }

        /// <summary>
        /// Valida que el control FR3 tenga los datos mínimos requeridos
        /// </summary>
        public bool EsValido()
        {
            return !string.IsNullOrWhiteSpace(Aeropuerto)
                && !string.IsNullOrWhiteSpace(Anio)
                && !string.IsNullOrWhiteSpace(Ruc)
                && !string.IsNullOrWhiteSpace(Matricula);
        }
    }
}
