using System;
using System.Collections.Generic;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;

namespace CapaPresentacion.Models.ViewModels
{
    /// <summary>
    /// Flags de UI derivados del flujo institucional (sin lógica duplicada en Razor).
    /// </summary>
    public sealed class SolicitudAocrFlujoViewModel
    {
        public string ClaveInstitucional { get; set; }
        public string EstadoVisible { get; set; }
        public bool ProcesoCerrado { get; set; }
        public bool TieneInspectorAsignado { get; set; }
        public string InspectorPrincipalNombre { get; set; }
        public string InspectorPrincipalIdentificador { get; set; }
        public bool MostrarAccionesRt { get; set; }
        public bool MostrarAccionesCoordinador { get; set; }
        public bool MostrarAccionesInspector { get; set; }
        public bool MostrarAccionesDireccion { get; set; }
        public bool PuedeGenerarOrden { get; set; }
        public bool PuedeCargarDocumentos { get; set; }
        public bool PuedeEnviarCoordinacion { get; set; }
        public bool PuedeAsignarInspector { get; set; }
        public bool PuedeRevisarTecnico { get; set; }
        public bool PuedeGenerarLv { get; set; }
        public bool PuedeGenerarInforme { get; set; }
        public bool PuedeAceptarDocumentacion { get; set; }
        public bool PuedeAbrirRevisionDocumental { get; set; }
        public bool PuedeGenerarAocr { get; set; }
        public string MotivoGenerarAocr { get; set; }
        public bool PuedeFirmarDireccion { get; set; }
        public bool PuedeDescargarFinal { get; set; }
    }

    public static class SolicitudAocrFlujoViewModelBuilder
    {
        public static SolicitudAocrFlujoViewModel Construir(
            SolicitudAOCR solicitud,
            AocrAuthorizationContext authContext,
            bool procesoCerrado,
            bool puedeGenerarAocr,
            string motivoGenerarAocr,
            IEnumerable<Inspeccion> inspecciones = null)
        {
            var vm = new SolicitudAocrFlujoViewModel();
            if (solicitud == null)
            {
                return vm;
            }

            var estadoService = new AocrEstadoService();
            var flujoService = new AocrFlujoService();
            var authService = new AocrAuthorizationService();
            var ordenDao = new OrdenRecaudacionDAO();
            var estado = estadoService.Normalizar(solicitud.Estado);
            var codigoSolicitud = solicitud.CodigoSolicitud;
            var inspeccionActiva = ResolverInspeccionActiva(inspecciones);
            var codigoInspeccion = inspeccionActiva != null ? inspeccionActiva.CodigoInspeccion : 0;

            vm.ClaveInstitucional = estadoService.NormalizarClaveInstitucional(solicitud.Estado);
            vm.EstadoVisible = solicitud.Estado ?? estado;
            vm.ProcesoCerrado = procesoCerrado;
            vm.TieneInspectorAsignado = TieneInspector(solicitud, inspecciones);
            vm.InspectorPrincipalNombre = solicitud.TecnicoResponsableNombre ?? string.Empty;
            vm.InspectorPrincipalIdentificador = solicitud.TecnicoResponsableCedula
                ?? (solicitud.CodigoTecnico.HasValue ? solicitud.CodigoTecnico.Value.ToString() : string.Empty);

            var roles = authContext != null ? authContext.Roles : new List<string>();
            vm.MostrarAccionesRt = ContieneRol(roles, "Solicitante") || ContieneRol(roles, "Administrador");
            vm.MostrarAccionesCoordinador = ContieneRol(roles, "Coordinacion") || ContieneRol(roles, "Administrador");
            vm.MostrarAccionesInspector = ContieneRol(roles, "InspectorTecnico") || ContieneRol(roles, "Administrador");
            vm.MostrarAccionesDireccion = ContieneRol(roles, "DireccionJefaturaTecnica") || ContieneRol(roles, "Administrador");

            var tieneAprobacionFinanciera = ordenDao.TieneAprobacionFinancieraSolicitud(codigoSolicitud);
            vm.PuedeAsignarInspector = vm.MostrarAccionesCoordinador
                && !procesoCerrado
                && flujoService.PuedeCoordinadorAsignarInspector(solicitud, tieneAprobacionFinanciera);

            vm.PuedeAceptarDocumentacion = vm.MostrarAccionesCoordinador
                && !procesoCerrado
                && authService.PuedeEjecutarAccion("Aprobar", authContext, codigoSolicitud, modulo: "SolicitudAOCR").Permitido;

            vm.PuedeAbrirRevisionDocumental = vm.TieneInspectorAsignado
                && (vm.MostrarAccionesInspector || vm.MostrarAccionesCoordinador || vm.MostrarAccionesDireccion);

            vm.PuedeGenerarAocr = puedeGenerarAocr;
            vm.MotivoGenerarAocr = motivoGenerarAocr ?? string.Empty;

            vm.PuedeGenerarOrden = vm.MostrarAccionesRt
                && !procesoCerrado
                && authService.PuedeEjecutarAccion("Generar", authContext, codigoSolicitud, modulo: "OrdenRecaudacion").Permitido;

            vm.PuedeCargarDocumentos = vm.MostrarAccionesRt
                && !procesoCerrado
                && authService.PuedeEjecutarAccion("Subir", authContext, codigoSolicitud, modulo: "Documento").Permitido;

            vm.PuedeEnviarCoordinacion = vm.MostrarAccionesRt
                && !procesoCerrado
                && authService.PuedeEjecutarAccion("FinalizarRT", authContext, codigoSolicitud, modulo: "SolicitudAOCR").Permitido;

            vm.PuedeRevisarTecnico = vm.MostrarAccionesInspector
                && codigoInspeccion > 0
                && authService.PuedeEjecutarAccion("ModalInformeTecnico", authContext, codigoInspeccion: codigoInspeccion, modulo: "Inspeccion").Permitido;

            vm.PuedeGenerarLv = vm.MostrarAccionesInspector
                && codigoInspeccion > 0
                && authService.PuedeEjecutarAccion("LV", authContext, codigoInspeccion: codigoInspeccion, modulo: "Inspeccion").Permitido;

            vm.PuedeGenerarInforme = vm.MostrarAccionesInspector
                && codigoInspeccion > 0
                && authService.PuedeEjecutarAccion("GuardarInformeTecnico", authContext, codigoInspeccion: codigoInspeccion, modulo: "Inspeccion").Permitido;

            vm.PuedeFirmarDireccion = vm.MostrarAccionesDireccion
                && !procesoCerrado
                && authService.PuedeEjecutarAccion("FirmarDireccion", authContext, codigoInspeccion: codigoInspeccion > 0 ? codigoInspeccion : (int?)null, modulo: "Inspeccion").Permitido;

            vm.PuedeDescargarFinal = !procesoCerrado
                && authService.PuedeEjecutarAccion("DescargarGenerada", authContext, codigoSolicitud, modulo: "SolicitudAOCR").Permitido;

            return vm;
        }

        private static Inspeccion ResolverInspeccionActiva(IEnumerable<Inspeccion> inspecciones)
        {
            Inspeccion activa = null;
            foreach (var inspeccion in inspecciones ?? new Inspeccion[0])
            {
                if (inspeccion == null || inspeccion.CodigoInspeccion <= 0)
                {
                    continue;
                }

                if (activa == null || inspeccion.CodigoInspeccion > activa.CodigoInspeccion)
                {
                    activa = inspeccion;
                }
            }

            return activa;
        }

        private static bool TieneInspector(SolicitudAOCR solicitud, IEnumerable<Inspeccion> inspecciones)
        {
            if (solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableCedula)
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
            {
                return true;
            }

            foreach (var inspeccion in inspecciones ?? new Inspeccion[0])
            {
                if (inspeccion != null && inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContieneRol(IList<string> roles, string rolBuscado)
        {
            if (roles == null)
            {
                return false;
            }

            foreach (var rol in roles)
            {
                if (string.Equals(rol, rolBuscado, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
