using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using CapaDatos.DAOs;
using CapaDatos.Models;

namespace CapaNegocio.Services
{
    public class CorreoInstitucionalService
    {
        public const string CoordinadorAocr = "COORDINADOR_AOCR";
        public const string FinancieroAocr = "FINANCIERO_AOCR";
        public const string Dirdac = "DIRDAC";
        public const string DireccionJefatura = "DIRECCION_JEFATURA";
        public const string SoporteAocr = "SOPORTE_AOCR";
        public const string NotificacionesAocr = "NOTIFICACIONES_AOCR";
        private const string CodigoAreaInspectorAocr = "INSPECTOR_AOCR";

        private readonly CorreoInstitucionalDAO _dao;

        public CorreoInstitucionalService()
            : this(new CorreoInstitucionalDAO())
        {
        }

        public CorreoInstitucionalService(CorreoInstitucionalDAO dao)
        {
            _dao = dao;
        }

        public string ObtenerCorreoCoordinadorAocr() { return ObtenerCorreoPrincipal(CoordinadorAocr); }
        public string ObtenerCorreoFinancieroAocr() { return ObtenerCorreoPrincipal(FinancieroAocr); }
        public string ObtenerCorreoDirdac() { return ObtenerCorreoPrincipal(Dirdac); }
        public string ObtenerCorreoDireccionJefatura() { return ObtenerCorreoPrincipal(DireccionJefatura); }
        public string ObtenerCorreoNotificacionesAocr() { return ObtenerCorreoPrincipal(NotificacionesAocr); }

        public string ObtenerCorreoPrincipal(string codigoArea)
        {
            var destinatarios = ObtenerDestinatariosPorArea(codigoArea);
            return destinatarios != null ? destinatarios.CorreoPrincipal : null;
        }

        public static bool EsAreaReservadaNoAdministrable(string codigoArea)
        {
            return string.Equals(
                (codigoArea ?? string.Empty).Trim(),
                CodigoAreaInspectorAocr,
                StringComparison.OrdinalIgnoreCase);
        }

        public DestinatariosCorreoInstitucional ObtenerDestinatariosPorArea(string codigoArea)
        {
            if (EsAreaReservadaNoAdministrable(codigoArea))
            {
                return null;
            }

            var model = _dao.ObtenerDestinatarios(codigoArea);
            if (model == null || !model.Activo || string.IsNullOrWhiteSpace(model.CorreoPrincipal))
            {
                return null;
            }

            return new DestinatariosCorreoInstitucional
            {
                CodigoArea = model.CodigoArea,
                NombreArea = model.NombreArea,
                CorreoPrincipal = model.CorreoPrincipal.Trim(),
                CorreosCc = SepararCorreos(model.CorreosCc),
                CorreosBcc = SepararCorreos(model.CorreosBcc)
            };
        }

        public static List<string> SepararCorreos(string correos)
        {
            if (string.IsNullOrWhiteSpace(correos))
            {
                return new List<string>();
            }

            return correos
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool EsCorreoValido(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo)) return false;
            try
            {
                var mail = new MailAddress(correo.Trim());
                return string.Equals(mail.Address, correo.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool SonCorreosMultiplesValidos(string correos, out string mensaje)
        {
            mensaje = string.Empty;
            foreach (var correo in SepararCorreos(correos))
            {
                if (!EsCorreoValido(correo))
                {
                    mensaje = "El correo '" + correo + "' no tiene un formato válido.";
                    return false;
                }
            }
            return true;
        }
    }

    public class DestinatariosCorreoInstitucional
    {
        public string CodigoArea { get; set; }
        public string NombreArea { get; set; }
        public string CorreoPrincipal { get; set; }
        public List<string> CorreosCc { get; set; }
        public List<string> CorreosBcc { get; set; }

        public IEnumerable<string> ObtenerTodosLosCorreos()
        {
            if (!string.IsNullOrWhiteSpace(CorreoPrincipal))
            {
                yield return CorreoPrincipal.Trim();
            }

            foreach (var correo in CorreosCc ?? new List<string>())
            {
                yield return correo;
            }

            foreach (var correo in CorreosBcc ?? new List<string>())
            {
                yield return correo;
            }
        }
    }
}
