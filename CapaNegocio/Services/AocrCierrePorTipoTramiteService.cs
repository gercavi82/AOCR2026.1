using System;
using System.Collections.Generic;
using CapaModelo;

namespace CapaNegocio.Services
{
    public enum AocrTipoCierre
    {
        NoSoportado = 0,
        EmisionRenovacion = 7,
        Modificacion = 8
    }

    public sealed class AocrCierrePorTipoTramitePlan
    {
        public AocrTipoCierre TipoCierre { get; set; }
        public string TipoTramite { get; set; }
        public string Modulo { get; set; }
        public bool GenerarAocr { get; set; }
        public bool GenerarCondiciones { get; set; }
        public IList<string> DocumentosRequeridos { get; set; }
        public bool EsValido { get; set; }
        public string Motivo { get; set; }
    }

    /// <summary>Fuente única para decidir documentos y cierre de los módulos 7 y 8.</summary>
    public sealed class AocrCierrePorTipoTramiteService
    {
        public const string Reconocimiento = "RECONOCIMIENTO";
        public const string Condiciones = "CONDICIONES_LIMITACIONES";

        public AocrCierrePorTipoTramitePlan Resolver(SolicitudAOCR solicitud)
        {
            if (solicitud == null) return Invalido("Solicitud no encontrada.");
            switch (solicitud.TipoSolicitud.GetValueOrDefault())
            {
                case 1:
                    return Modulo7("EMISION");
                case 2:
                    return Modulo7("RENOVACION");
                case 3:
                    return Modulo8(AocrModificationWorkflowService.TieneNuevoAeropuertoDeclarado(solicitud)
                        ? "MODIFICACION_CON_NUEVO_AEROPUERTO"
                        : "MODIFICACION");
                default:
                    return Invalido("El tipo de trámite no tiene una regla institucional de cierre configurada.");
            }
        }

        public bool PuedeGenerarDocumento(SolicitudAOCR solicitud, string tipoDocumento, out string motivo)
        {
            var plan = Resolver(solicitud);
            var tipo = (tipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
            if (!plan.EsValido) { motivo = plan.Motivo; return false; }
            var permitido = (tipo == Reconocimiento && plan.GenerarAocr)
                || ((tipo == Condiciones || tipo == "CONDICIONES") && plan.GenerarCondiciones);
            motivo = permitido ? string.Empty
                : "El " + plan.TipoTramite + " pertenece al " + plan.Modulo + " y no permite generar " + tipo + ".";
            return permitido;
        }

        private static AocrCierrePorTipoTramitePlan Modulo7(string tramite)
        {
            return new AocrCierrePorTipoTramitePlan { EsValido = true, TipoCierre = AocrTipoCierre.EmisionRenovacion,
                TipoTramite = tramite, Modulo = "MODULO_7", GenerarAocr = true, GenerarCondiciones = true,
                DocumentosRequeridos = new[] { Reconocimiento, Condiciones } };
        }

        private static AocrCierrePorTipoTramitePlan Modulo8(string tramite)
        {
            return new AocrCierrePorTipoTramitePlan { EsValido = true, TipoCierre = AocrTipoCierre.Modificacion,
                TipoTramite = tramite, Modulo = "MODULO_8", GenerarAocr = false, GenerarCondiciones = true,
                DocumentosRequeridos = new[] { Condiciones },
                Motivo = "No existe una regla institucional documentada que autorice emitir un AOCR nuevo para modificaciones." };
        }

        private static AocrCierrePorTipoTramitePlan Invalido(string motivo)
        {
            return new AocrCierrePorTipoTramitePlan { EsValido = false, TipoCierre = AocrTipoCierre.NoSoportado,
                Modulo = "NO_CONFIGURADO", DocumentosRequeridos = new string[0], Motivo = motivo };
        }
    }
}
