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
            errores = new List<string>();

            if (Items == null || Items.Count == 0)
            {
                errores.Add("La lista de verificación operacional EAE no contiene ítems configurados.");
                return false;
            }

            var camposCabeceraPendientes = new[]
            {
                new { Nombre = "Nombre del EAE / Nombre comercial del EAE", Valor = NombreEae },
                new { Nombre = "N AOC / Fecha de expedicion / Validez", Valor = NumeroAocFechaValidez },
                new { Nombre = "Direccion del EAE en el Estado del explotador", Valor = DireccionEstadoExplotador },
                new { Nombre = "Direccion del EAE en el Estado que emite el reconocimiento", Valor = DireccionEstadoReconocimiento },
                new { Nombre = "Tipo/s de aeronave/s", Valor = TiposAeronaves },
                new { Nombre = "Tipo de operacion", Valor = TipoOperacion },
                new { Nombre = "Inspector responsable de la aprobacion", Valor = InspectorResponsable }
            };

            var campoPendiente = camposCabeceraPendientes.FirstOrDefault(campo => string.IsNullOrWhiteSpace(campo.Valor));
            if (campoPendiente != null)
            {
                errores.Add("Complete el campo de cabecera de la LV: " + campoPendiente.Nombre);
                return false;
            }

            var itemsValidables = Items
                .Where(item => item != null && !item.EsNotaOrientacion)
                .ToList();
            if (itemsValidables.Count == 0)
            {
                itemsValidables = Items
                    .Where(item => item != null)
                    .ToList();
            }

            var itemSinEstadosNiObservacion = itemsValidables.FirstOrDefault(item =>
                (string.IsNullOrWhiteSpace(item.EstadoCumplimiento)
                    || string.IsNullOrWhiteSpace(item.EstadoImplementacion))
                && string.IsNullOrWhiteSpace(item.PruebasNotasComentarios));
            if (itemSinEstadosNiObservacion != null)
            {
                errores.Add("Debe seleccionar el estado de cumplimiento/implementación o registrar una observación en la columna 14 para la orientación: " + itemSinEstadosNiObservacion.ObtenerEtiqueta());
                return false;
            }

            foreach (var grupo in itemsValidables
                .GroupBy(item => !string.IsNullOrWhiteSpace(item.CodigoPregunta) ? item.CodigoPregunta.Trim() : (item.Codigo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var comentarioGrupo = grupo
                    .Select(item => (item.PruebasNotasComentarios ?? string.Empty).Trim())
                    .FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor)) ?? string.Empty;

                var itemCumplimientoNoSatisfactorio = grupo.FirstOrDefault(item =>
                    string.Equals(item.EstadoCumplimiento, "NO_SATISFACTORIO", StringComparison.OrdinalIgnoreCase));
                if (itemCumplimientoNoSatisfactorio != null && string.IsNullOrWhiteSpace(comentarioGrupo))
                {
                    errores.Add("Ingrese una observación en Pruebas / Notas / Comentarios para el requisito: " + itemCumplimientoNoSatisfactorio.ObtenerEtiqueta());
                    return false;
                }
            }

            var itemNoImplementadoSinObservacion = itemsValidables.FirstOrDefault(item =>
                string.Equals(item.EstadoImplementacion, "NO_IMPLEMENTADO", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(item.PruebasNotasComentarios));
            if (itemNoImplementadoSinObservacion != null)
            {
                errores.Add("Ingrese una observación en Pruebas / Notas / Comentarios para la orientación: " + itemNoImplementadoSinObservacion.ObtenerEtiqueta());
                return false;
            }

            return errores.Count == 0;
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
            return !string.IsNullOrWhiteSpace(EstadoCumplimiento)
                && !string.IsNullOrWhiteSpace(EstadoImplementacion)
                && (!string.Equals(EstadoCumplimiento, "NO_SATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(PruebasNotasComentarios))
                && (!string.Equals(EstadoImplementacion, "NO_IMPLEMENTADO", StringComparison.OrdinalIgnoreCase)
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
