using System.Collections.Generic;
using CapaDatos.Constants;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class GateEAuthorizationTests
    {
    public class FakeUsuarioAS400DAO : CapaDatos.Interfaces.IUsuarioAS400DAO
    {
        public string ObtenerCodigoCiudadPorCodigoUsuario(string codigoUsuario) => "UIO";
        public CapaDatos.Models.UsuarioInternoAs400Info ObtenerDatosUsuarioInterno(string codigoUsuario) => new CapaDatos.Models.UsuarioInternoAs400Info();
        public string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario) => "1790000000001";
        public string ObtenerCedulaPorCodigoUsuario(string codigoUsuario) => "1700000000";
        public bool UpsertUsuarioCompleto(CapaDatos.Models.UsuarioAs400Record record, out string error) { error = null; return true; }
    }

    public class FakeEmpresaAS400DAO : CapaDatos.Interfaces.IEmpresaAS400DAO
    {
        public bool TestConnection() => true;
        public System.Collections.Generic.List<CapaDatos.DAOs.Empresa> ObtenerEmpresas() => new System.Collections.Generic.List<CapaDatos.DAOs.Empresa>();
        public CapaDatos.DAOs.Empresa ObtenerEmpresaPorCodigo(string codigoOaci) => new CapaDatos.DAOs.Empresa { CodigoOaci = codigoOaci };
    }

        private static AocrAuthorizationService CrearServicio()
        {
            return new AocrAuthorizationService(new FakeUsuarioAS400DAO(), new FakeEmpresaAS400DAO());
        }

        private static AocrAuthorizationContext ContextoRt(int userId = 100)
        {
            return new AocrAuthorizationContext
            {
                UserId = userId,
                IsAuthenticated = true,
                SelectedRole = "Solicitante",
                Roles = new List<string> { "Solicitante" },
                CompanyCode = "OP001"
            };
        }

        private static AocrAuthorizationContext ContextoInspector(int userId = 43)
        {
            return new AocrAuthorizationContext
            {
                UserId = userId,
                IsAuthenticated = true,
                SelectedRole = "Inspector",
                Roles = new List<string> { "InspectorTecnico" },
                CodigoUsuario = userId.ToString()
            };
        }

        private static AocrAuthorizationContext ContextoCoordinador(int userId = 20)
        {
            return new AocrAuthorizationContext
            {
                UserId = userId,
                IsAuthenticated = true,
                SelectedRole = "Coordinador",
                Roles = new List<string> { "Coordinacion" }
            };
        }

        private static AocrAuthorizationContext ContextoDireccion(int userId = 30)
        {
            return new AocrAuthorizationContext
            {
                UserId = userId,
                IsAuthenticated = true,
                SelectedRole = "DIRDAC",
                Roles = new List<string> { "DireccionJefaturaTecnica" }
            };
        }

        [TestMethod]
        public void Adm1_Rt_InformeTecnicoInspector_DebeDenegar()
        {
            var resultado = CrearServicio().PuedeEjecutarAccion(
                "Inspector",
                ContextoRt(),
                codigoInspeccion: 11,
                modulo: "InformeTecnico");

            Assert.IsFalse(resultado.Permitido, "RT no debe abrir Informe Tecnico de inspector. Motivo=" + resultado.Motivo);
        }

        [TestMethod]
        public void Adm1_Rt_FirmarLv_DebeDenegar()
        {
            var resultado = CrearServicio().PuedeEjecutarAccion(
                "FirmarListaVerificacionOperacionalEae",
                ContextoRt(),
                codigoInspeccion: 11,
                modulo: "Inspeccion");

            Assert.IsFalse(resultado.Permitido, "RT no debe firmar LV. Motivo=" + resultado.Motivo);
        }

        [TestMethod]
        public void Adm1_Inspector_GenerarAocrFinal_DebeDenegar()
        {
            var resultado = CrearServicio().PuedeEjecutarAccion(
                "Generar",
                ContextoInspector(),
                codigoSolicitud: 12,
                modulo: "SolicitudAOCR");

            Assert.IsFalse(resultado.Permitido, "Inspector no debe generar AOCR final. Motivo=" + resultado.Motivo);
        }

        [TestMethod]
        public void Adm1_Coordinador_ModificarInformeTecnico_DebeDenegar()
        {
            var resultado = CrearServicio().PuedeEjecutarAccion(
                "GuardarInformeTecnico",
                ContextoCoordinador(),
                codigoInspeccion: 11,
                modulo: "Inspeccion");

            Assert.IsFalse(resultado.Permitido, "Coordinador no debe modificar informe tecnico. Motivo=" + resultado.Motivo);
        }

        [TestMethod]
        public void Adm1_Direccion_CargarDocumentosComoRt_DebeDenegar()
        {
            var resultado = CrearServicio().PuedeEjecutarAccion(
                "Subir",
                ContextoDireccion(),
                codigoSolicitud: 12,
                modulo: "Documento");

            Assert.IsFalse(resultado.Permitido, "Direccion no debe cargar documentos como RT. Motivo=" + resultado.Motivo);
        }

        [TestMethod]
        public void Adm1_InspectorNoAsignado_DetalleInspeccion_DebeDenegar()
        {
            var resultado = CrearServicio().PuedeEjecutarAccion(
                "Detalle",
                ContextoInspector(userId: 99999),
                codigoInspeccion: 99999,
                modulo: "Inspeccion");

            Assert.IsFalse(resultado.Permitido, "Inspector ajeno no debe abrir detalle. Motivo=" + resultado.Motivo);
        }

        [TestMethod]
        public void GateE_MatrizDebeIncluirDescargasYAccionesPostCriticas()
        {
            var service = CrearServicio();
            var admin = new AocrAuthorizationContext
            {
                UserId = 1,
                IsAuthenticated = true,
                Roles = new List<string> { "Administrador" },
                SelectedRole = "Administrador"
            };

            var acciones = new[]
            {
                "VerInforme",
                "DescargarInforme",
                "DescargarListaVerificacionOperacionalEae",
                "CambiarEstado",
                "SubirInforme",
                "RegistrarNoConforme",
                "RevisarDocumentos"
            };

            foreach (var accion in acciones)
            {
                var modulo = accion == "RevisarDocumentos" ? "Documento" : "Inspeccion";
                var resultado = service.PuedeEjecutarAccion(accion, admin, codigoInspeccion: 1, modulo: modulo);
                Assert.IsTrue(resultado.Permitido, "Administrador debe invocar " + accion + ". Motivo=" + resultado.Motivo);
            }
        }

        [TestMethod]
        public void GateE_Rt_CambiarEstadoInspeccion_DebeDenegar()
        {
            var resultado = CrearServicio().PuedeEjecutarAccion(
                "CambiarEstado",
                ContextoRt(),
                codigoInspeccion: 11,
                modulo: "Inspeccion");

            Assert.IsFalse(resultado.Permitido, "RT no debe cambiar estado de inspeccion. Motivo=" + resultado.Motivo);
        }

        [TestMethod]
        public void GateE_LegacyEstadoService_DebeMapearCatalogoHistorico()
        {
            var service = new AocrEstadoService();
            Assert.AreEqual(EstadoSolicitud.EnInspeccion, service.NormalizarDesdeLegacyCatalogo("EN_EVALUACION_TECNICA"));
            Assert.AreEqual(EstadoSolicitud.Observada, service.NormalizarDesdeLegacyCatalogo("SUBSANACION"));
            Assert.AreEqual(EstadoSolicitud.AOCR_EmitidoRecibido, service.NormalizarDesdeLegacyCatalogo("AOCR_EMITIDO"));
        }

        [TestMethod]
        public void GateE_Administrador_AccionesOperativas_DebeDenegar()
        {
            var service = CrearServicio();
            var admin = new AocrAuthorizationContext
            {
                UserId = 1,
                IsAuthenticated = true,
                Roles = new List<string> { "Administrador" },
                SelectedRole = "Administrador"
            };

            // Regla 7: El Administrador no puede aprobar, devolver, designar o firmar en representación de roles operativos
            Assert.IsFalse(service.PuedeEjecutarAccion("AprobarPago", admin, codigoOrden: 1, modulo: "Financiero").Permitido, "Admin no debe aprobar pago");
            Assert.IsFalse(service.PuedeEjecutarAccion("AprobarOrden", admin, codigoOrden: 1, modulo: "Financiero").Permitido, "Admin no debe aprobar orden");
            Assert.IsFalse(service.PuedeEjecutarAccion("AsignarInspector", admin, codigoSolicitud: 1, modulo: "Coordinador").Permitido, "Admin no debe asignar inspector");
            Assert.IsFalse(service.PuedeEjecutarAccion("AsignarInspector", admin, codigoSolicitud: 1, modulo: "Tecnico").Permitido, "Admin no debe asignar inspector en Tecnico");
            Assert.IsFalse(service.PuedeEjecutarAccion("FirmarAceptacionDocumental", admin, codigoSolicitud: 1, modulo: "CoordinacionJefatura").Permitido, "Admin no debe firmar aceptacion documental");
            Assert.IsFalse(service.PuedeEjecutarAccion("FirmarInformeInspector", admin, codigoInspeccion: 1, modulo: "Inspeccion").Permitido, "Admin no debe firmar informe inspector");
            Assert.IsFalse(service.PuedeEjecutarAccion("FirmarListaVerificacionOperacionalEae", admin, codigoInspeccion: 1, modulo: "Inspeccion").Permitido, "Admin no debe firmar LV inspector");
            Assert.IsFalse(service.PuedeEjecutarAccion("FirmarDireccion", admin, codigoInspeccion: 1, modulo: "Inspeccion").Permitido, "Admin no debe firmar como Direccion");
            Assert.IsFalse(service.PuedeEjecutarAccion("FirmarInformeDirdac", admin, codigoInspeccion: 1, modulo: "Inspeccion").Permitido, "Admin no debe firmar informe Dirdac");
            Assert.IsFalse(service.PuedeEjecutarAccion("AprobarDecisionFinalDireccion", admin, codigoInspeccion: 1, modulo: "Inspeccion").Permitido, "Admin no debe aprobar decision direccion");
            Assert.IsFalse(service.PuedeEjecutarAccion("DevolverDecisionFinalDireccion", admin, codigoInspeccion: 1, modulo: "Inspeccion").Permitido, "Admin no debe devolver decision direccion");
        }
    }
}
