using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class InspeccionCorreoService
    {
        private readonly IEmailQueueService _emailQueueService;
        private readonly ILoggingService _logger;

        public InspeccionCorreoService()
            : this(new EmailQueueService())
        {
        }

        public InspeccionCorreoService(IEmailQueueService emailQueueService)
        {
            _emailQueueService = emailQueueService;
            _logger = LoggingServiceFactory.Create();
        }

        public ResultadoOperacion NotificarEvento(Inspeccion inspeccion, SolicitudAOCR solicitud, string evento, string observacion)
        {
            try
            {
                if (inspeccion == null || solicitud == null)
                {
                    return ResultadoOperacion.Error("No existe contexto suficiente para notificar el evento de inspeccion.");
                }

                var plantilla = ConstruirPlantilla(inspeccion, solicitud, evento, observacion);
                if (plantilla == null)
                {
                    return ResultadoOperacion.Ok(null, "Evento sin plantilla de correo configurada.");
                }

                var destinatarios = ResolverDestinatarios(inspeccion, solicitud, plantilla);
                if (destinatarios.Count == 0)
                {
                    return ResultadoOperacion.Ok(null, "Evento sin destinatarios de correo resolubles.");
                }

                foreach (var destinatario in destinatarios)
                {
                    var item = new EmailQueueItem
                    {
                        Para = destinatario.Email,
                        ParaNombre = destinatario.Nombre,
                        Asunto = plantilla.Asunto,
                        Cuerpo = ConstruirCuerpoHtml(destinatario.Nombre, plantilla, inspeccion, solicitud, observacion),
                        Estado = "PENDIENTE",
                        OrdenId = solicitud.CodigoSolicitud,
                        TipoNotificacion = "INSPECCION_" + (evento ?? string.Empty).Trim().ToUpperInvariant(),
                        EsHtml = true
                    };

                    _emailQueueService.EncolarAsync(item).GetAwaiter().GetResult();
                }

                return ResultadoOperacion.Ok(destinatarios.Count, "Correos de inspeccion encolados correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InspeccionCorreoService.NotificarEvento: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible encolar correos del evento de inspeccion.");
            }
        }

        private List<CorreoDestino> ResolverDestinatarios(Inspeccion inspeccion, SolicitudAOCR solicitud, PlantillaCorreoInspeccion plantilla)
        {
            var destinatarios = new Dictionary<string, CorreoDestino>(StringComparer.OrdinalIgnoreCase);

            if (plantilla.IncluirRt)
            {
                AgregarCorreo(destinatarios, solicitud.CorreoRepresentanteTecnico, solicitud.RepresentanteLegal);
                AgregarCorreo(destinatarios, solicitud.Email, solicitud.NombreOperador);
            }

            if (plantilla.IncluirInspector)
            {
                AgregarUsuario(destinatarios, solicitud.CodigoTecnico);
                AgregarUsuario(destinatarios, inspeccion.CodigoInspector);
            }

            if (plantilla.IncluirCoordinacion)
            {
                AgregarUsuariosPorRol(destinatarios, "CoordinadorInspecciones", "Coordinador", "JefaturaTecnica");
            }

            if (plantilla.IncluirDireccionLegal)
            {
                AgregarUsuariosPorRol(destinatarios, "CoordinacionLegal", "CoordinadorLegal", "DirectorGeneral", "Direccion");
            }

            return destinatarios.Values.ToList();
        }

        private void AgregarUsuariosPorRol(IDictionary<string, CorreoDestino> destinatarios, params string[] roles)
        {
            if (roles == null)
            {
                return;
            }

            foreach (var rol in roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var usuarios = UsuarioDAO.ListarPorRol(rol) ?? new List<Usuario>();
                foreach (var usuario in usuarios)
                {
                    AgregarCorreo(destinatarios, usuario != null ? usuario.Email : null, ConstruirNombreUsuario(usuario));
                }
            }
        }

        private void AgregarUsuario(IDictionary<string, CorreoDestino> destinatarios, int? idUsuario)
        {
            if (!idUsuario.HasValue || idUsuario.Value <= 0)
            {
                return;
            }

            try
            {
                var usuario = UsuarioDAO.ObtenerPorId(idUsuario.Value);
                if (usuario != null)
                {
                    AgregarCorreo(destinatarios, usuario.Email, ConstruirNombreUsuario(usuario));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InspeccionCorreoService.AgregarUsuario: " + ex.Message);
            }
        }

        private static void AgregarCorreo(IDictionary<string, CorreoDestino> destinatarios, string email, string nombre)
        {
            var correo = (email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(correo))
            {
                return;
            }

            if (!destinatarios.ContainsKey(correo))
            {
                destinatarios[correo] = new CorreoDestino
                {
                    Email = correo,
                    Nombre = string.IsNullOrWhiteSpace(nombre) ? "Usuario AOCR" : nombre.Trim()
                };
            }
        }

        private static PlantillaCorreoInspeccion ConstruirPlantilla(Inspeccion inspeccion, SolicitudAOCR solicitud, string evento, string observacion)
        {
            var eventoNormalizado = (evento ?? string.Empty).Trim().ToUpperInvariant();
            switch (eventoNormalizado)
            {
                case "NC_GENERADAS":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - No conformidades registradas en inspeccion #" + inspeccion.CodigoInspeccion,
                        Titulo = "No conformidades registradas",
                        Mensaje = "Se registraron no conformidades que requieren validacion de coordinacion y subsanacion del RT.",
                        IncluirRt = true,
                        IncluirCoordinacion = true
                    };
                case "DOCUMENTOS_SUBSANADOS":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Documentacion subsanada en inspeccion #" + inspeccion.CodigoInspeccion,
                        Titulo = "Documentacion subsanada",
                        Mensaje = "El RT actualizo documentos asociados a no conformidades y el expediente requiere revalidacion.",
                        IncluirInspector = true,
                        IncluirCoordinacion = true
                    };
                case "DEVOLUCION_INSPECCION":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Tramite de inspeccion devuelto #" + inspeccion.CodigoInspeccion,
                        Titulo = "Tramite devuelto para correccion",
                        Mensaje = "La inspeccion fue devuelta para correccion o para programar una nueva inspeccion, segun observaciones registradas.",
                        IncluirInspector = true,
                        IncluirRt = true,
                        IncluirCoordinacion = true
                    };
                case "APROBACION_INSPECCION":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Inspeccion aprobada #" + inspeccion.CodigoInspeccion,
                        Titulo = "Inspeccion aprobada",
                        Mensaje = "La inspeccion fue aprobada y el expediente queda listo para el siguiente tramo institucional.",
                        IncluirCoordinacion = true,
                        IncluirDireccionLegal = true
                    };
                case "REVALIDACION_OK":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Revalidacion satisfactoria #" + inspeccion.CodigoInspeccion,
                        Titulo = "Revalidacion satisfactoria",
                        Mensaje = "La revalidacion de la inspeccion fue satisfactoria y el tramite puede continuar.",
                        IncluirCoordinacion = true,
                        IncluirRt = true
                    };
                case "REVALIDACION_RECHAZADA":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Revalidacion con observaciones #" + inspeccion.CodigoInspeccion,
                        Titulo = "Revalidacion con observaciones",
                        Mensaje = "La revalidacion mantiene observaciones pendientes y se requiere una nueva subsanacion o ajuste del tramite.",
                        IncluirInspector = true,
                        IncluirCoordinacion = true,
                        IncluirRt = true
                    };
                default:
                    return null;
            }
        }

        private static string ConstruirCuerpoHtml(string nombreDestino, PlantillaCorreoInspeccion plantilla, Inspeccion inspeccion, SolicitudAOCR solicitud, string observacion)
        {
            var operador = string.IsNullOrWhiteSpace(solicitud.NombreOperador) ? (solicitud.RazonSocial ?? "Operador") : solicitud.NombreOperador;
            var observacionHtml = string.IsNullOrWhiteSpace(observacion)
                ? string.Empty
                : "<p><strong>Observaciones:</strong> " + System.Web.HttpUtility.HtmlEncode(observacion) + "</p>";

            return string.Format(@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background:#f5f8fb; padding:24px;'>
    <div style='max-width:680px; margin:0 auto; background:#ffffff; border:1px solid #dce7ef; border-radius:16px; overflow:hidden;'>
        <div style='background:linear-gradient(135deg,#143b57 0%,#1b6f8a 100%); color:#ffffff; padding:20px 24px;'>
            <div style='font-size:12px; letter-spacing:0.08em; text-transform:uppercase; opacity:0.85;'>Sistema AOCR DGAC</div>
            <h2 style='margin:8px 0 0 0;'>{0}</h2>
        </div>
        <div style='padding:24px;'>
            <p>Estimado/a <strong>{1}</strong>,</p>
            <p>{2}</p>
            <table style='width:100%; border-collapse:collapse; margin:18px 0;'>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Inspeccion</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>#{3}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Solicitud AOCR</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>#{4}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Operador</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>{5}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Estado actual</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>{6}</td>
                </tr>
            </table>
            {7}
            <p>Puede revisar el expediente desde el sistema AOCR en el detalle de inspeccion correspondiente.</p>
            <p style='margin-top:24px; color:#617588; font-size:12px;'>Este es un mensaje automatico del workflow de inspeccion AOCR.</p>
        </div>
    </div>
</body>
</html>",
                plantilla.Titulo,
                System.Web.HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(nombreDestino) ? "Usuario AOCR" : nombreDestino),
                System.Web.HttpUtility.HtmlEncode(plantilla.Mensaje),
                inspeccion.CodigoInspeccion,
                solicitud.CodigoSolicitud,
                System.Web.HttpUtility.HtmlEncode(operador),
                System.Web.HttpUtility.HtmlEncode(inspeccion.Estado ?? "PENDIENTE"),
                observacionHtml);
        }

        private static string ConstruirNombreUsuario(Usuario usuario)
        {
            if (usuario == null)
            {
                return "Usuario AOCR";
            }

            var nombre = string.Join(" ", new[]
            {
                usuario.NombreCompleto,
                usuario.NombreUsuario,
                usuario.ApellidoUsuario
            }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

            return string.IsNullOrWhiteSpace(nombre) ? usuario.CodigoUsuario ?? "Usuario AOCR" : nombre;
        }

        private sealed class CorreoDestino
        {
            public string Email { get; set; }
            public string Nombre { get; set; }
        }

        private sealed class PlantillaCorreoInspeccion
        {
            public string Asunto { get; set; }
            public string Titulo { get; set; }
            public string Mensaje { get; set; }
            public bool IncluirRt { get; set; }
            public bool IncluirInspector { get; set; }
            public bool IncluirCoordinacion { get; set; }
            public bool IncluirDireccionLegal { get; set; }
        }
    }
}