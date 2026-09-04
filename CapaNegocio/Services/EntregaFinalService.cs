using System;
using System.Collections.Generic;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Interfaces;
using CapaModelo;
using CapaNegocio.Interfaces;

namespace CapaNegocio.Services
{
    public sealed class EntregaFinalService : IEntregaFinalService
    {
        public const string PermisoSolicitar = "ENTREGA_FINAL_SOLICITAR";
        public const string PermisoConsultaInstitucional = "ENTREGA_FINAL_CONSULTAR";
        public const string PermisoAuditoria = "ENTREGA_FINAL_AUDITAR";

        private readonly IEntregaFinalRepository _repository;

        public EntregaFinalService() : this(new EntregaFinalDAO()) { }

        public EntregaFinalService(IEntregaFinalRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException("repository");
        }

        public EntregaFinalResult Solicitar(SolicitarEntregaFinalRequest request)
        {
            var error = ValidarActor(request != null ? request.Actor : null, AocrRolesInstitucionales.Dirdac, true);
            if (error != null) return error;
            if (request.SolicitudId <= 0 || request.VersionExpedienteEsperada <= 0)
                return EntregaFinalResult.Error(400, "REQUEST_INVALIDO", "Solicitud y versión esperada son obligatorias.");
            try { return _repository.Solicitar(request); }
            catch { return EntregaFinalResult.Error(500, "ERROR_INTERNO", "No fue posible solicitar la entrega final."); }
        }

        public DocumentosFinalesViewModel ListarDocumentos(EntregaFinalActor actor)
        {
            if (!EsDestinatarioOConsultaInstitucional(actor))
                return new DocumentosFinalesViewModel { Rol = actor != null ? actor.RolActivo : string.Empty };
            return new DocumentosFinalesViewModel
            {
                Rol = actor.RolActivo,
                Documentos = _repository.ListarDocumentos(actor)
            };
        }

        public DescargaFinalAutorizada AutorizarDescarga(int documentoId, EntregaFinalActor actor)
        {
            if (documentoId <= 0) return ErrorDescarga(400, "DOCUMENTO_INVALIDO", "Documento inválido.");
            if (actor == null || actor.UsuarioId <= 0) return ErrorDescarga(401, "SESION_INVALIDA", "La sesión no es válida.");
            if (!EsDestinatarioOConsultaInstitucional(actor)) return ErrorDescarga(403, "ACCESO_DENEGADO", "No tiene acceso a documentos finales.");
            try { return _repository.AutorizarDescarga(documentoId, actor); }
            catch { return ErrorDescarga(500, "ERROR_INTERNO", "No fue posible autorizar la descarga."); }
        }

        public IList<EstadoEntregaFinalViewModel> ConsultarEstados(EntregaFinalActor actor, int? solicitudId)
        {
            if (actor == null || actor.UsuarioId <= 0 || !AocrRolesInstitucionales.EsAdministrador(actor.RolActivo) || !actor.TienePermiso)
                return new List<EstadoEntregaFinalViewModel>();
            return _repository.ConsultarEstados(solicitudId);
        }

        private static EntregaFinalResult ValidarActor(EntregaFinalActor actor, string rol, bool requierePermiso)
        {
            if (actor == null || actor.UsuarioId <= 0) return EntregaFinalResult.Error(401, "SESION_INVALIDA", "La sesión no es válida.");
            if (!string.Equals((actor.RolActivo ?? string.Empty).Trim(), rol, StringComparison.OrdinalIgnoreCase))
                return EntregaFinalResult.Error(403, "ROL_NO_AUTORIZADO", "El rol activo no puede ejecutar esta operación.");
            if (requierePermiso && !actor.TienePermiso) return EntregaFinalResult.Error(403, "PERMISO_DENEGADO", "No tiene el permiso requerido.");
            return null;
        }

        private static bool EsDestinatarioOConsultaInstitucional(EntregaFinalActor actor)
        {
            if (actor == null || actor.UsuarioId <= 0) return false;
            if (AocrRolesInstitucionales.EsRt(actor.RolActivo) || AocrRolesInstitucionales.EsInspector(actor.RolActivo)) return true;
            if (AocrRolesInstitucionales.EsAdministrador(actor.RolActivo) || string.Equals(actor.RolActivo, AocrRolesInstitucionales.Financiero, StringComparison.OrdinalIgnoreCase)) return false;
            return actor.TienePermiso && (AocrRolesInstitucionales.EsCoordinador(actor.RolActivo)
                || AocrRolesInstitucionales.EsDircav(actor.RolActivo) || AocrRolesInstitucionales.EsDirdac(actor.RolActivo));
        }

        private static DescargaFinalAutorizada ErrorDescarga(int status, string codigo, string mensaje)
        {
            return new DescargaFinalAutorizada { HttpStatusCode = status, Codigo = codigo, Mensaje = mensaje };
        }
    }
}
