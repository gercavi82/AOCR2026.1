using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class NotificacionDestinatarioPolicyService
    {
        public const string GrupoOperadorSolicitante = "OPERADOR_SOLICITANTE";
        public const string GrupoRepresentanteTecnico = "REPRESENTANTE_TECNICO";
        public const string GrupoInspectorAsignado = "INSPECTOR_ASIGNADO";
        public const string GrupoCoordinacionInspeccion = "COORDINACION_INSPECCION";
        public const string GrupoCoordinacionLegal = "COORDINACION_LEGAL";
        public const string GrupoDireccionFinal = "DIRECCION_FINAL";
        public const string GrupoFinanciero = "FINANCIERO";

        private readonly ILoggingService _logger;

        public NotificacionDestinatarioPolicyService()
        {
            _logger = LoggingServiceFactory.Create();
        }

        public List<NotificacionDestinatario> ResolverDestinatarios(
            SolicitudAOCR solicitud,
            Inspeccion inspeccion,
            params string[] grupos)
        {
            var destinatarios = new Dictionary<string, NotificacionDestinatario>(StringComparer.OrdinalIgnoreCase);

            if (solicitud == null || grupos == null || grupos.Length == 0)
            {
                return destinatarios.Values.ToList();
            }

            foreach (var grupo in grupos.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                switch ((grupo ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case GrupoOperadorSolicitante:
                        AgregarCorreo(destinatarios, solicitud.Email, solicitud.NombreOperador ?? solicitud.RazonSocial);
                        break;

                    case GrupoRepresentanteTecnico:
                        AgregarCorreo(destinatarios, solicitud.CorreoRepresentanteTecnico, solicitud.RepresentanteLegal);
                        break;

                    case GrupoInspectorAsignado:
                        AgregarUsuario(destinatarios, solicitud.CodigoTecnico);
                        if (inspeccion != null)
                        {
                            AgregarUsuario(destinatarios, inspeccion.CodigoInspector);
                        }
                        break;

                    case GrupoCoordinacionInspeccion:
                        AgregarUsuariosPorRol(destinatarios,
                            RolesAOCR.COORDINADOR_INSPECCIONES,
                            "Coordinador",
                            RolesAOCR.JEFATURA_TECNICA,
                            RolesAOCR.ADMINISTRADOR);
                        break;

                    case GrupoCoordinacionLegal:
                        AgregarUsuariosPorRol(destinatarios,
                            "CoordinacionLegal",
                            RolesAOCR.COORDINADOR_LEGAL,
                            RolesAOCR.ADMINISTRADOR);
                        break;

                    case GrupoDireccionFinal:
                        AgregarUsuariosPorRol(destinatarios,
                            "Direccion",
                            "DirectorGeneral",
                            RolesAOCR.JEFATURA_TECNICA,
                            RolesAOCR.ADMINISTRADOR);
                        break;

                    case GrupoFinanciero:
                        AgregarUsuariosPorRol(destinatarios,
                            "Financiero",
                            RolesAOCR.COORDINADOR_FINANCIERO,
                            RolesAOCR.DIRECTOR_FINANCIERO,
                            RolesAOCR.ADMINISTRADOR);
                        break;
                }
            }

            return destinatarios.Values.ToList();
        }

        private void AgregarUsuariosPorRol(IDictionary<string, NotificacionDestinatario> destinatarios, params string[] roles)
        {
            if (roles == null)
            {
                return;
            }

            foreach (var rol in roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var usuarios = UsuarioDAO.ListarPorRol(rol) ?? new List<Usuario>();
                    foreach (var usuario in usuarios)
                    {
                        AgregarCorreo(destinatarios, usuario != null ? usuario.Email : null, ConstruirNombreUsuario(usuario));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("NotificacionDestinatarioPolicyService.AgregarUsuariosPorRol(" + rol + "): " + ex.Message);
                }
            }
        }

        private void AgregarUsuario(IDictionary<string, NotificacionDestinatario> destinatarios, int? idUsuario)
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
                _logger.LogWarning("NotificacionDestinatarioPolicyService.AgregarUsuario: " + ex.Message);
            }
        }

        private static void AgregarCorreo(IDictionary<string, NotificacionDestinatario> destinatarios, string email, string nombre)
        {
            var correo = (email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(correo))
            {
                return;
            }

            if (!destinatarios.ContainsKey(correo))
            {
                destinatarios[correo] = new NotificacionDestinatario
                {
                    Email = correo,
                    Nombre = string.IsNullOrWhiteSpace(nombre) ? "Usuario AOCR" : nombre.Trim()
                };
            }
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
    }

    public class NotificacionDestinatario
    {
        public string Email { get; set; }
        public string Nombre { get; set; }
    }
}