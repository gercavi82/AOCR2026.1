using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaModelo
{
    public sealed class PendienteListaVerificacion
    {
        public string Codigo { get; set; }
        public string Etiqueta { get; set; }
        public List<string> Errores { get; set; } = new List<string>();
    }

    public sealed class ValidacionListaVerificacionResultado
    {
        public List<PendienteListaVerificacion> Pendientes { get; set; } = new List<PendienteListaVerificacion>();
        public List<string> ErroresCabecera { get; set; } = new List<string>();
        public int CantidadPendientes => Pendientes.Count;
        public bool EsValida => Pendientes.Count == 0 && ErroresCabecera.Count == 0;
        public string Mensaje => EsValida ? "Todos los elementos obligatorios están completos."
            : (Pendientes.Count > 0 ? Pendientes.Count + " ítem(s) incompleto(s). " : string.Empty)
              + string.Join(" ", ErroresCabecera)
              + (Pendientes.Count > 0 ? " No puede completar, finalizar ni firmar la LV. Pendientes: "
                  + string.Join(", ", Pendientes.Select(p => p.Codigo)) + ". "
                  + Pendientes[0].Errores.FirstOrDefault() : string.Empty);
    }

    public sealed class ListaVerificacionIncompletaException : InvalidOperationException
    {
        public ValidacionListaVerificacionResultado Resultado { get; private set; }
        public ListaVerificacionIncompletaException(ValidacionListaVerificacionResultado resultado)
            : base(resultado.Mensaje) { Resultado = resultado; }
    }

    public static class ValidadorListaVerificacion
    {
        public static readonly IReadOnlyList<string> Cumplimientos = Array.AsReadOnly(new[]
            { "SATISFACTORIO", "NO_SATISFACTORIO", "NO_APLICABLE" });
        public static readonly IReadOnlyList<string> Implementaciones = Array.AsReadOnly(new[]
            { "IMPLEMENTADO", "NO_IMPLEMENTADO", "NO_APLICABLE" });

        public static bool EsCumplimientoValido(string valor) =>
            Cumplimientos.Contains((valor ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);
        public static bool EsImplementacionValida(string valor) =>
            Implementaciones.Contains((valor ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

        // Se evalúan instancias hidratadas desde el catálogo de confianza del servidor.
        // Las observaciones complementan el resultado; nunca lo sustituyen.
        public static ValidacionListaVerificacionResultado Evaluar(ListaVerificacionOperacionalEae lista)
        {
            var resultado = new ValidacionListaVerificacionResultado();
            if (lista == null)
            {
                resultado.ErroresCabecera.Add("No existe una lista de verificación para procesar.");
                return resultado;
            }
            var cabecera = new[]
            {
                new { Nombre = "Nombre del EAE", Valor = lista.NombreEae },
                new { Nombre = "N AOC / Fecha de expedición / Validez", Valor = lista.NumeroAocFechaValidez },
                new { Nombre = "Dirección en el Estado del explotador", Valor = lista.DireccionEstadoExplotador },
                new { Nombre = "Dirección en el Estado de reconocimiento", Valor = lista.DireccionEstadoReconocimiento },
                new { Nombre = "Tipos de aeronaves", Valor = lista.TiposAeronaves },
                new { Nombre = "Tipo de operación", Valor = lista.TipoOperacion },
                new { Nombre = "Inspector responsable", Valor = lista.InspectorResponsable }
            };
            resultado.ErroresCabecera.AddRange(cabecera.Where(c => string.IsNullOrWhiteSpace(c.Valor))
                .Select(c => "Complete el campo de cabecera de la LV: " + c.Nombre + "."));
            var obligatorios = (lista.Items ?? new List<ListaVerificacionOperacionalEaeItem>())
                .Where(item => item != null && !item.EsNotaOrientacion).ToList();
            if (obligatorios.Count == 0)
                resultado.ErroresCabecera.Add("La lista de verificación no contiene ítems configurados obligatorios.");

            foreach (var grupo in obligatorios.GroupBy(i => string.IsNullOrWhiteSpace(i.CodigoPregunta)
                ? i.Codigo : i.CodigoPregunta, StringComparer.OrdinalIgnoreCase))
            {
                var tieneObservacion = grupo.Any(i => !string.IsNullOrWhiteSpace(i.PruebasNotasComentarios));
                foreach (var item in grupo)
                {
                    var pendiente = new PendienteListaVerificacion { Codigo = item.Codigo, Etiqueta = item.ObtenerEtiqueta() };
                    if (!EsCumplimientoValido(item.EstadoCumplimiento))
                        pendiente.Errores.Add("Seleccione un resultado de cumplimiento válido para " + item.Codigo + ".");
                    if (!EsImplementacionValida(item.EstadoImplementacion))
                        pendiente.Errores.Add("Seleccione un resultado de implementación válido para " + item.Codigo + ".");
                    if (string.Equals(item.EstadoCumplimiento?.Trim(), "NO_SATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                        && !tieneObservacion)
                        pendiente.Errores.Add("Ingrese una observación para el requisito " + grupo.Key + ".");
                    if (string.Equals(item.EstadoImplementacion?.Trim(), "NO_IMPLEMENTADO", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(item.PruebasNotasComentarios))
                        pendiente.Errores.Add("Ingrese una observación para la orientación " + item.Codigo + ".");
                    if (pendiente.Errores.Count > 0) resultado.Pendientes.Add(pendiente);
                }
            }
            return resultado;
        }
    }
}
