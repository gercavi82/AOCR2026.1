using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaModelo
{
    public class ListaVerificacionOperacionalEae
    {
        public int CodigoListaVerificacion { get; set; }
        public int CodigoInspeccion { get; set; }
        public int? SolicitudId { get; set; }
        public int? EstacionId { get; set; }
        public string EstacionCodigo { get; set; }
        public string EstacionNombre { get; set; }
        public string TipoLista { get; set; } = "EAE";
        public bool Vigente { get; set; } = true;
        public int Version { get; set; }
        public int? CodigoListaAnterior { get; set; }
        public int? CodigoNoConformidadOrigen { get; set; }
        public int CicloEvaluacion { get; set; } = 1;
        public bool EsReevaluacion { get; set; }
        public string EstadoLista { get; set; }
        public string NombreEae { get; set; }
        public string NumeroAocFechaValidez { get; set; }
        public string DireccionEstadoExplotador { get; set; }
        public string DireccionEstadoReconocimiento { get; set; }
        public string TiposAeronaves { get; set; }
        public string TipoOperacion { get; set; }
        public DateTime? FechaLista { get; set; }
        public string InspectorResponsable { get; set; }
        public string CargoInspector { get; set; }
        public string ResumenVerificacion { get; set; }
        public string ObservacionesGenerales { get; set; }
        public string ResultadoGeneral { get; set; }
        public string ItemsJson { get; set; }
        public string RutaPdf { get; set; }
        public string RutaDocumentoFirmado { get; set; }
        public string HashDocumento { get; set; }
        public bool Finalizado { get; set; }
        public bool FirmadoTecnico { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public DateTime? FechaFirma { get; set; }
        public string UsuarioFirma { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public List<ListaVerificacionOperacionalEaeItem> Items { get; set; }

        public ListaVerificacionOperacionalEae()
        {
            EstadoLista = "LV_BORRADOR";
            TipoLista = "EAE";
            Vigente = true;
            NombreEae = string.Empty;
            NumeroAocFechaValidez = string.Empty;
            DireccionEstadoExplotador = string.Empty;
            DireccionEstadoReconocimiento = string.Empty;
            TiposAeronaves = string.Empty;
            TipoOperacion = string.Empty;
            InspectorResponsable = string.Empty;
            CargoInspector = string.Empty;
            ResumenVerificacion = string.Empty;
            ObservacionesGenerales = string.Empty;
            ResultadoGeneral = string.Empty;
            ItemsJson = "[]";
            RutaPdf = string.Empty;
            RutaDocumentoFirmado = string.Empty;
            HashDocumento = string.Empty;
            UsuarioFirma = string.Empty;
            Items = new List<ListaVerificacionOperacionalEaeItem>();
        }

        public bool ValidarCompletitud(out List<string> errores)
        {
            var resultado = EvaluarCompletitud();
            errores = resultado.ErroresCabecera.Concat(resultado.Pendientes.SelectMany(p => p.Errores)).ToList();
            return resultado.EsValida;
        }

        public ValidacionListaVerificacionResultado EvaluarCompletitud()
        {
            return ValidadorListaVerificacion.Evaluar(this);
        }

        public bool EstaCompleta()
        {
            List<string> errores;
            return ValidarCompletitud(out errores);
        }
    }

    public class ListaVerificacionOperacionalEaeItem
    {
        public string Codigo { get; set; }
        public string CodigoPregunta { get; set; }
        public int Orden { get; set; }
        public int GrupoRequisitoId { get; set; }
        public string Referencia { get; set; }
        public string PreguntaRequisito { get; set; }
        public string NotaPregunta { get; set; }
        public string EstadoCumplimiento { get; set; }
        public string OrientacionEvidencia { get; set; }
        public string EstadoImplementacion { get; set; }
        public string PruebasNotasComentarios { get; set; }
        public bool EsOrientacionIndependiente { get; set; }
        public bool EsNotaOrientacion { get; set; }
        public bool EsLiteral { get; set; }
        public bool EsSubnumeral { get; set; }

        public ListaVerificacionOperacionalEaeItem()
        {
            Codigo = string.Empty;
            CodigoPregunta = string.Empty;
            Referencia = string.Empty;
            PreguntaRequisito = string.Empty;
            NotaPregunta = string.Empty;
            EstadoCumplimiento = string.Empty;
            OrientacionEvidencia = string.Empty;
            EstadoImplementacion = string.Empty;
            PruebasNotasComentarios = string.Empty;
            EsOrientacionIndependiente = true;
        }

        public bool EstaCompleto()
        {
            return ValidadorListaVerificacion.EsCumplimientoValido(EstadoCumplimiento)
                && ValidadorListaVerificacion.EsImplementacionValida(EstadoImplementacion)
                && ((!string.Equals(EstadoCumplimiento?.Trim(), "NO_SATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(EstadoImplementacion?.Trim(), "NO_IMPLEMENTADO", StringComparison.OrdinalIgnoreCase))
                    || !string.IsNullOrWhiteSpace(PruebasNotasComentarios));
        }

        public string ObtenerEtiqueta()
        {
            var codigo = !string.IsNullOrWhiteSpace(CodigoPregunta)
                ? CodigoPregunta.Trim()
                : (Codigo ?? string.Empty).Trim();
            var orientacion = (OrientacionEvidencia ?? string.Empty).Replace("\r\n", " ").Replace("\n", " ").Trim();
            if (orientacion.Length > 120)
            {
                orientacion = orientacion.Substring(0, 117).TrimEnd() + "...";
            }

            if (string.IsNullOrWhiteSpace(orientacion))
            {
                return codigo;
            }

            if (string.IsNullOrWhiteSpace(codigo))
            {
                return orientacion;
            }

            return codigo + " - " + orientacion;
        }
    }
}
