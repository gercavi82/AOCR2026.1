using System;
using CapaNegocio.DTOs;

namespace CapaPresentacion.Models
{
    // Placeholders para mantener compatibilidad de firmas en Controllers con los DTOs reales.
    public class CrearOrdenViewModel : CrearOrdenRequest { }
    public class RegistrarPagoViewModel : RegistrarPagoRequest { }
    public class ValidarPagoViewModel : ValidarPagoRequest { }

    // ViewModels faltantes usados en controladores/vistas antiguas
    public class EventoOrdenViewModel { }
    public class OrdenRecaudacionIndexViewModel { }
    public class OrdenRecaudacionResumenViewModel { }

    // OrdenRecaudacionViewModel eliminado → usar CapaPresentacion.Models.ViewModels.OrdenRecaudacionViewModel (OrdenRecaudacionMV.cs)
}
