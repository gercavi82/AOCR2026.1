using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CapaPresentacion.Models
{
    /// <summary>
    /// Estados posibles de una orden de recaudacion
    /// </summary>
    public enum EstadoOrden
    {
        Borrador = 1,
        Generada = 2,
        Enviada = 3,
        Pagada = 4,
        Anulada = 5
    }

    /// <summary>
    /// ViewModel para el Index de ordenes de recaudacion
    /// </summary>
    public class OrdenRecaudacionIndexViewModel
    {
        public OrdenRecaudacionIndexViewModel()
        {
            Ordenes = new List<OrdenRecaudacionResumenViewModel>();
        }

        public List<OrdenRecaudacionResumenViewModel> Ordenes { get; set; }
        
        // Resumen de totales
        public int TotalOrdenes { get; set; }
        public int OrdenesPendientes { get; set; }
        public int OrdenesPagadas { get; set; }
        public int OrdenesAnuladas { get; set; }
        public int OrdenesBorrador { get; set; }
        public decimal MontoTotalPendiente { get; set; }
        
        // Filtros activos
        public string FiltroEstado { get; set; }
        public DateTime? FiltroFechaDesde { get; set; }
        public DateTime? FiltroFechaHasta { get; set; }
        
        // Permisos del usuario
        public bool PuedeCrearOrden { get; set; }
        public bool ExisteOrdenEnBorrador { get; set; }
    }

    /// <summary>
    /// ViewModel para el resumen de cada orden en listados
    /// </summary>
    public class OrdenRecaudacionResumenViewModel
    {
        public int CodigoOrden { get; set; }
        public string NumeroOrden { get; set; }
        public string Cliente { get; set; }
        public decimal Monto { get; set; }
        public string Concepto { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaLimite { get; set; }
        public EstadoOrden Estado { get; set; }
        public string EstadoDescripcion { get; set; }
        public bool EstaVencida { get; set; }
        public int DiasParaVencer { get; set; }
        
        // Acciones disponibles segun estado y rol
        public bool PuedeEditar { get; set; }
        public bool PuedeEnviar { get; set; }
        public bool PuedeMarcarPagada { get; set; }
        public bool PuedeAnular { get; set; }
    }

    /// <summary>
    /// ViewModel para crear/editar una orden
    /// </summary>
    public class OrdenRecaudacionFormViewModel
    {
        public OrdenRecaudacionFormViewModel()
        {
            Adjuntos = new List<AdjuntoViewModel>();
            HistorialEventos = new List<EventoOrdenViewModel>();
        }

        public int CodigoOrden { get; set; }
        
        [Required(ErrorMessage = "El cliente es requerido")]
        [Display(Name = "Cliente")]
        public string Cliente { get; set; }
        
        [Required(ErrorMessage = "El monto es requerido")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        [Display(Name = "Monto")]
        public decimal Monto { get; set; }
        
        [Required(ErrorMessage = "El concepto es requerido")]
        [StringLength(500, ErrorMessage = "El concepto no puede exceder 500 caracteres")]
        [Display(Name = "Concepto")]
        public string Concepto { get; set; }
        
        [Required(ErrorMessage = "La fecha es requerida")]
        [Display(Name = "Fecha")]
        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; }
        
        [Display(Name = "Fecha Limite")]
        [DataType(DataType.Date)]
        public DateTime? FechaLimite { get; set; }
        
        [Display(Name = "Observaciones")]
        [StringLength(1000)]
        public string Observaciones { get; set; }
        
        public EstadoOrden Estado { get; set; }
        public string EstadoDescripcion { get; set; }
        
        // Para edicion
        public bool EsNuevo { get; set; }
        public bool PuedeEditar { get; set; }
        public string MensajeBloqueo { get; set; }
        
        // Adjuntos
        public List<AdjuntoViewModel> Adjuntos { get; set; }
        
        // Historial (para detalles)
        public List<EventoOrdenViewModel> HistorialEventos { get; set; }
        
        // Acciones disponibles
        public bool PuedeGuardarBorrador { get; set; }
        public bool PuedeGenerar { get; set; }
        public bool PuedeEnviar { get; set; }
        public bool PuedeMarcarPagada { get; set; }
        public bool PuedeAnular { get; set; }
    }

    /// <summary>
    /// ViewModel para adjuntos
    /// </summary>
    public class AdjuntoViewModel
    {
        public int CodigoAdjunto { get; set; }
        public string NombreArchivo { get; set; }
        public string TipoArchivo { get; set; }
        public long TamanoBytes { get; set; }
        public DateTime FechaCarga { get; set; }
        public string UrlDescarga { get; set; }
    }

    /// <summary>
    /// ViewModel para eventos del historial
    /// </summary>
    public class EventoOrdenViewModel
    {
        public int CodigoEvento { get; set; }
        public DateTime Fecha { get; set; }
        public string Accion { get; set; }
        public string Usuario { get; set; }
        public string Detalle { get; set; }
        public EstadoOrden? EstadoAnterior { get; set; }
        public EstadoOrden? EstadoNuevo { get; set; }
    }

    /// <summary>
    /// ViewModel para la vista Obligatoria (ordenes urgentes)
    /// </summary>
    public class OrdenRecaudacionObligatoriaViewModel
    {
        public OrdenRecaudacionObligatoriaViewModel()
        {
            OrdenesUrgentes = new List<OrdenRecaudacionResumenViewModel>();
        }

        public List<OrdenRecaudacionResumenViewModel> OrdenesUrgentes { get; set; }
        public int TotalVencidas { get; set; }
        public int TotalProximasAVencer { get; set; }
        public decimal MontoTotalUrgente { get; set; }
    }

    /// <summary>
    /// Helper para obtener descripcion de estados
    /// </summary>
    public static class EstadoOrdenHelper
    {
        public static string ObtenerDescripcion(EstadoOrden estado)
        {
            switch (estado)
            {
                case EstadoOrden.Borrador:
                    return "Borrador";
                case EstadoOrden.Generada:
                    return "Generada";
                case EstadoOrden.Enviada:
                    return "Enviada";
                case EstadoOrden.Pagada:
                    return "Pagada";
                case EstadoOrden.Anulada:
                    return "Anulada";
                default:
                    return "Desconocido";
            }
        }

        public static string ObtenerClaseBadge(EstadoOrden estado)
        {
            switch (estado)
            {
                case EstadoOrden.Borrador:
                    return "badge-secondary";
                case EstadoOrden.Generada:
                    return "badge-info";
                case EstadoOrden.Enviada:
                    return "badge-warning";
                case EstadoOrden.Pagada:
                    return "badge-success";
                case EstadoOrden.Anulada:
                    return "badge-danger";
                default:
                    return "badge-light";
            }
        }

        public static bool PuedeEditar(EstadoOrden estado)
        {
            return estado == EstadoOrden.Borrador || estado == EstadoOrden.Generada;
        }

        public static bool PuedeEnviar(EstadoOrden estado)
        {
            return estado == EstadoOrden.Generada;
        }

        public static bool PuedeMarcarPagada(EstadoOrden estado)
        {
            return estado == EstadoOrden.Enviada;
        }

        public static bool PuedeAnular(EstadoOrden estado)
        {
            return estado == EstadoOrden.Generada || estado == EstadoOrden.Enviada;
        }
    }
}
