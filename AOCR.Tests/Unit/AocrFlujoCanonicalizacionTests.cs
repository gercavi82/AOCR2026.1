using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrFlujoCanonicalizacionTests
    {
        private class FakeUsuarioAS400DAO : CapaDatos.Interfaces.IUsuarioAS400DAO
        {
            public string ObtenerCodigoCiudadPorCodigoUsuario(string codigoUsuario) => "UIO";
            public CapaDatos.Models.UsuarioInternoAs400Info ObtenerDatosUsuarioInterno(string codigoUsuario) => new CapaDatos.Models.UsuarioInternoAs400Info();
            public string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario) => "1790000000001";
            public string ObtenerCedulaPorCodigoUsuario(string codigoUsuario) => "1700000000";
            public bool UpsertUsuarioCompleto(CapaDatos.Models.UsuarioAs400Record record, out string error) { error = null; return true; }
        }

        private class FakeEmpresaAS400DAO : CapaDatos.Interfaces.IEmpresaAS400DAO
        {
            public bool TestConnection() => true;
            public System.Collections.Generic.List<CapaDatos.DAOs.Empresa> ObtenerEmpresas() => new System.Collections.Generic.List<CapaDatos.DAOs.Empresa>();
            public CapaDatos.DAOs.Empresa ObtenerEmpresaPorCodigo(string codigoOaci) => new CapaDatos.DAOs.Empresa { CodigoOaci = codigoOaci };
        }

        private readonly AocrFlujoService _flujoService = new AocrFlujoService();
        private readonly AocrEstadoService _estadoService = new AocrEstadoService();
        private readonly AocrAuthorizationService _authService = new AocrAuthorizationService(new FakeUsuarioAS400DAO(), new FakeEmpresaAS400DAO());

        // ====================================================================
        // PRUEBA 1: Reconocimiento de los siete roles canónicos
        // ====================================================================
        [TestMethod]
        public void Prueba01_ReconocimientoDeSieteRolesCanonicos()
        {
            var roles = AocrRolesInstitucionales.RolesCanonicos;
            Assert.AreEqual(7, roles.Length, "Deben existir exactamente 7 roles canónicos.");

            CollectionAssert.Contains(roles, AocrRolesInstitucionales.Dircav);
            CollectionAssert.Contains(roles, AocrRolesInstitucionales.Dirdac);
            CollectionAssert.Contains(roles, AocrRolesInstitucionales.Coordinador);
            CollectionAssert.Contains(roles, AocrRolesInstitucionales.RT);
            CollectionAssert.Contains(roles, AocrRolesInstitucionales.Financiero);
            CollectionAssert.Contains(roles, AocrRolesInstitucionales.Inspector);
            CollectionAssert.Contains(roles, AocrRolesInstitucionales.Administrador);

            // Mapeo canónico
            Assert.AreEqual(AocrRolesInstitucionales.Dircav, AocrRolesInstitucionales.NormalizarRolCanonico("DIRCAV"));
            Assert.AreEqual(AocrRolesInstitucionales.Dircav, AocrRolesInstitucionales.NormalizarRolCanonico("DCAV"));
            Assert.AreEqual(AocrRolesInstitucionales.Dircav, AocrRolesInstitucionales.NormalizarRolCanonico("DirectorCertificacionesDcav"));

            Assert.AreEqual(AocrRolesInstitucionales.Dirdac, AocrRolesInstitucionales.NormalizarRolCanonico("DIRDAC"));
            Assert.AreEqual(AocrRolesInstitucionales.Dirdac, AocrRolesInstitucionales.NormalizarRolCanonico("DIRECTORDIRDAC"));
            Assert.AreEqual(AocrRolesInstitucionales.Dirdac, AocrRolesInstitucionales.NormalizarRolCanonico("DIRECTORGENERAL"));

            Assert.AreEqual(AocrRolesInstitucionales.Coordinador, AocrRolesInstitucionales.NormalizarRolCanonico("COORDINADOR"));
            Assert.AreEqual(AocrRolesInstitucionales.Coordinador, AocrRolesInstitucionales.NormalizarRolCanonico("Coordinacion"));

            Assert.AreEqual(AocrRolesInstitucionales.Inspector, AocrRolesInstitucionales.NormalizarRolCanonico("INSPECTOR"));
            Assert.AreEqual(AocrRolesInstitucionales.Inspector, AocrRolesInstitucionales.NormalizarRolCanonico("InspectorTecnico"));

            Assert.AreEqual(AocrRolesInstitucionales.Financiero, AocrRolesInstitucionales.NormalizarRolCanonico("FINANCIERO"));
            Assert.AreEqual(AocrRolesInstitucionales.Financiero, AocrRolesInstitucionales.NormalizarRolCanonico("CoordinadorFinanciero"));

            Assert.AreEqual(AocrRolesInstitucionales.RT, AocrRolesInstitucionales.NormalizarRolCanonico("RT"));
            Assert.AreEqual(AocrRolesInstitucionales.RT, AocrRolesInstitucionales.NormalizarRolCanonico("Solicitante"));

            Assert.AreEqual(AocrRolesInstitucionales.Administrador, AocrRolesInstitucionales.NormalizarRolCanonico("ADMINISTRADOR"));
            Assert.AreEqual(AocrRolesInstitucionales.Administrador, AocrRolesInstitucionales.NormalizarRolCanonico("Admin"));
        }

        // ====================================================================
        // PRUEBA 2: Separación completa entre DIRCAV y DIRDAC
        // ====================================================================
        [TestMethod]
        public void Prueba02_SeparacionCompletaEntreDircavYDirdac()
        {
            Assert.IsTrue(AocrRolesInstitucionales.EsDircav("DIRCAV"));
            Assert.IsTrue(AocrRolesInstitucionales.EsDircav("DCAV"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("DIRDAC"), "DIRDAC no debe coincidir con DIRCAV.");
            Assert.IsFalse(AocrRolesInstitucionales.EsDircav("DIRECTORGENERAL"), "DIRECTORGENERAL no debe ser DIRCAV.");

            Assert.IsTrue(AocrRolesInstitucionales.EsDirdac("DIRDAC"));
            Assert.IsTrue(AocrRolesInstitucionales.EsDirdac("DIRECTORDIRDAC"));
            Assert.IsFalse(AocrRolesInstitucionales.EsDirdac("DIRCAV"), "DIRCAV no debe coincidir con DIRDAC.");
            Assert.IsFalse(AocrRolesInstitucionales.EsDirdac("DCAV"), "DCAV no debe coincidir con DIRDAC.");
        }

        // ====================================================================
        // PRUEBA 3: Administrador bloqueado en todas las acciones operativas
        // ====================================================================
        [TestMethod]
        public void Prueba03_AdministradorBloqueadoEnTodasLasAccionesOperativas()
        {
            var accionesOperativas = new[]
            {
                AocrFlujoAcciones.AprobarPago,
                AocrFlujoAcciones.AceptarDocumentacion,
                AocrFlujoAcciones.DevolverRtObservaciones,
                AocrFlujoAcciones.AsignarInspector,
                AocrFlujoAcciones.FirmarListaVerificacion,
                AocrFlujoAcciones.FirmarInformeTecnico,
                AocrFlujoAcciones.FirmarAocrFinal,
                AocrFlujoAcciones.DircavAceptarDocumentacion,
                AocrFlujoAcciones.DircavDevolverCoordinador,
                AocrFlujoAcciones.DircavConfirmarDesignacion,
                AocrFlujoAcciones.DircavFirmarDesignacion,
                AocrFlujoAcciones.DircavRevisarInforme,
                AocrFlujoAcciones.DircavFirmarCl,
                AocrFlujoAcciones.DircavRemitirDirdac,
                AocrFlujoAcciones.DirdacRevisarAocr,
                AocrFlujoAcciones.DirdacDevolverDircav,
                AocrFlujoAcciones.DirdacFirmarAocr,
                AocrFlujoAcciones.DirdacConfirmarLegalizacion,
                AocrFlujoAcciones.CoordinadorRemitirDircav,
                AocrFlujoAcciones.CoordinadorDevolverInspector,
                AocrFlujoAcciones.CoordinadorRemitirClDircav,
                AocrFlujoAcciones.CoordinadorDevolverClInspector,
                AocrFlujoAcciones.CoordinadorRevisarInformeTecnico,
                AocrFlujoAcciones.InspectorRemitirInformeCoordinador,
                AocrFlujoAcciones.LiberarDocumentosFinales
            };

            foreach (var accion in accionesOperativas)
            {
                Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", accion),
                    "El Administrador debe estar bloqueado para la acción operativa: " + accion);
                Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("ADMIN", accion),
                    "El Administrador (ADMIN) debe estar bloqueado para la acción operativa: " + accion);
            }
        }

        // ====================================================================
        // PRUEBA 4: Inspector bloqueado para remitir a DIRCAV y DIRDAC
        // ====================================================================
        [TestMethod]
        public void Prueba04_InspectorBloqueadoParaRemitirADircavYDirdac()
        {
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.EnviarDirdac),
                "Inspector no puede remitir a DIRDAC.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.CoordinadorRemitirDircav),
                "Inspector no puede remitir expediente a DIRCAV.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.DircavRemitirDirdac),
                "Inspector no puede remitir a DIRDAC en representación de DIRCAV.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("INSPECTOR", AocrFlujoAcciones.EnviarDirdac),
                "Inspector canónico no puede remitir a DIRDAC.");

            // Inspector sí puede remitir su informe a Coordinador
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.InspectorRemitirInformeCoordinador),
                "Inspector debe poder remitir su informe firmado a Coordinador.");
        }

        // ====================================================================
        // PRUEBA 5: Coordinador bloqueado para remitir a DIRDAC y sin GenerarAocr
        // ====================================================================
        [TestMethod]
        public void Prueba05_CoordinadorBloqueadoParaRemitirADirdacYSinGenerarAocr()
        {
            // Coordinador NO puede remitir directamente a DIRDAC
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.EnviarDirdac),
                "El Coordinador NUNCA puede remitir a DIRDAC.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("COORDINADOR", AocrFlujoAcciones.EnviarDirdac),
                "El rol canónico COORDINADOR no puede remitir a DIRDAC.");

            // Coordinador NO tiene permiso GenerarAocr
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.GenerarAocr),
                "El permiso GenerarAocr debe estar eliminado del Coordinador.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("COORDINADOR", AocrFlujoAcciones.GenerarAocr),
                "El rol canónico COORDINADOR no puede GenerarAocr.");

            // Coordinador sí puede remitir a DIRCAV
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.CoordinadorRemitirDircav),
                "Coordinador debe poder remitir expediente a DIRCAV.");
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("COORDINADOR", AocrFlujoAcciones.CoordinadorRemitirClDircav),
                "Coordinador debe poder remitir C&L a DIRCAV.");
        }

        // ====================================================================
        // PRUEBA 6: DIRCAV bloqueado para firmar AOCR
        // ====================================================================
        [TestMethod]
        public void Prueba06_DircavBloqueadoParaFirmarAocr()
        {
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRCAV", AocrFlujoAcciones.DirdacFirmarAocr),
                "DIRCAV no puede firmar AOCR.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRCAV", AocrFlujoAcciones.FirmarAocrFinal),
                "DIRCAV no puede firmar AOCR final.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Dcav", AocrFlujoAcciones.DirdacFirmarAocr),
                "Alias histórico DCAV no puede firmar AOCR.");

            // DIRCAV sí puede firmar C&L y Designación
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("DIRCAV", AocrFlujoAcciones.DircavFirmarCl),
                "DIRCAV firma exclusivamente Condiciones y Limitaciones.");
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("DIRCAV", AocrFlujoAcciones.DircavFirmarDesignacion),
                "DIRCAV firma la designación del inspector.");
        }

        // ====================================================================
        // PRUEBA 7: DIRDAC bloqueado para firmar Condiciones y Limitaciones
        // ====================================================================
        [TestMethod]
        public void Prueba07_DirdacBloqueadoParaFirmarCondicionesYLimitaciones()
        {
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.DircavFirmarCl),
                "DIRDAC no puede firmar Condiciones y Limitaciones.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.DircavConfirmarDesignacion),
                "DIRDAC no puede designar inspector.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.DircavFirmarDesignacion),
                "DIRDAC no puede firmar designación.");

            // DIRDAC sí puede firmar y legalizar AOCR
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.DirdacFirmarAocr),
                "DIRDAC firma exclusivamente el Certificado AOCR.");
            Assert.IsTrue(_flujoService.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.DirdacConfirmarLegalizacion),
                "DIRDAC legaliza el Certificado AOCR.");
        }

        // ====================================================================
        // PRUEBA 8: Acción desconocida devuelve acceso denegado (Default Deny)
        // ====================================================================
        [TestMethod]
        public void Prueba08_AccionDesconocidaDevuelveAccesoDenegado()
        {
            const string accionInexistente = "ACCION_INEXISTENTE_NO_REGISTRADA";

            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRCAV", accionInexistente),
                "Acción no registrada debe retornar false para DIRCAV.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRDAC", accionInexistente),
                "Acción no registrada debe retornar false para DIRDAC.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Administrador", accionInexistente),
                "Acción no registrada debe retornar false para Administrador.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("Coordinacion", accionInexistente),
                "Acción no registrada debe retornar false para Coordinador.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("InspectorTecnico", accionInexistente),
                "Acción no registrada debe retornar false para Inspector.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion(null, accionInexistente),
                "Rol nulo debe retornar false.");
            Assert.IsFalse(_flujoService.RolPuedeEjecutarAccion("DIRCAV", null),
                "Acción nula debe retornar false.");
        }

        // ====================================================================
        // PRUEBA 9: UsuarioId cero devuelve 401 (no autorizado)
        // ====================================================================
        [TestMethod]
        public void Prueba09_UsuarioIdCeroDevuelveNoAutorizado()
        {
            var ctxInvalido = new AocrAuthorizationContext
            {
                IsAuthenticated = true,
                UserId = 0,
                SelectedRole = "Coordinador"
            };

            var resultado = _authService.PuedeEjecutarAccion("RevisionCl", ctxInvalido, modulo: "CoordinacionJefatura");
            Assert.IsFalse(resultado.Permitido, "Contexto con UserId == 0 debe ser denegado.");
            Assert.AreEqual("La sesión expiró o no ha iniciado sesión.", resultado.Motivo);

            var ctxAnonimo = new AocrAuthorizationContext
            {
                IsAuthenticated = false,
                UserId = 1,
                SelectedRole = "Coordinador"
            };
            var resAnonimo = _authService.PuedeEjecutarAccion("RevisionCl", ctxAnonimo, modulo: "CoordinacionJefatura");
            Assert.IsFalse(resAnonimo.Permitido, "Contexto no autenticado debe ser denegado.");
        }

        // ====================================================================
        // PRUEBA 10: No existe transición de una sola firma a ENTREGADO
        // ====================================================================
        [TestMethod]
        public void Prueba10_NoExisteTransicionDeUnaSolaFirmaAEntregado()
        {
            // De C&L firmada por DIRCAV NO se puede saltar directamente a ENTREGADO
            Assert.IsFalse(_flujoService.EsTransicionPermitida(AocrEstadosProceso.ClFirmadaDircav, AocrEstadosProceso.Entregado),
                "No debe permitirse saltar de CL_FIRMADA_DIRCAV a ENTREGADO sin la firma de DIRDAC.");

            // De AOCR firmada por DIRDAC NO se puede saltar directamente a ENTREGADO sin pasar por FIRMAS_COMPLETAS / LISTO_PARA_ENTREGA
            Assert.IsFalse(_flujoService.EsTransicionPermitida(AocrEstadosProceso.AocrFirmadaDirdac, AocrEstadosProceso.Entregado),
                "No debe permitirse saltar de AOCR_FIRMADA_DIRDAC directamente a ENTREGADO.");

            // De FIRMAS_COMPLETAS debe pasar a LISTO_PARA_ENTREGA
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.AocrFirmadaDirdac, AocrEstadosProceso.FirmasCompletas));
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.FirmasCompletas, AocrEstadosProceso.ListoParaEntrega));

            // Solo desde LISTO_PARA_ENTREGA se alcanza ENTREGADO
            Assert.IsTrue(_flujoService.EsTransicionPermitida(AocrEstadosProceso.ListoParaEntrega, AocrEstadosProceso.Entregado),
                "Desde LISTO_PARA_ENTREGA debe permitirse la transición a ENTREGADO.");
        }

        // ====================================================================
        // PRUEBA 11: No existe transición heredada directa a FINALIZADO
        // ====================================================================
        [TestMethod]
        public void Prueba11_NoExisteTransicionHeredadaDirectaAFinalizado()
        {
            // El bypass legacy Firmado DCAV -> Finalizado debe estar bloqueado
            Assert.IsFalse(_flujoService.EsTransicionPermitida(EstadoSolicitud.FirmadoDcav, EstadoSolicitud.Finalizado),
                "El salto heredado directo de 'Firmado DCAV' a 'Finalizado' debe estar bloqueado.");

            // Tampoco en la matriz canónica directa de EstadoSolicitud
            Assert.IsFalse(EstadoSolicitud.EsTransicionValida(EstadoSolicitud.FirmadoDcav, EstadoSolicitud.Finalizado),
                "EstadoSolicitud.EsTransicionValida no debe permitir FirmadoDcav -> Finalizado.");

            // AocrFirmadaDirdac tampoco pasa directo a Finalizado
            Assert.IsFalse(_flujoService.EsTransicionPermitida(AocrEstadosProceso.AocrFirmadaDirdac, EstadoSolicitud.Finalizado),
                "AOCR_FIRMADA_DIRDAC no debe saltar directo a Finalizado.");
        }

        // ====================================================================
        // PRUEBA 12: Los estados históricos pueden leerse sin volver a guardarlos
        // ====================================================================
        [TestMethod]
        public void Prueba12_EstadosHistoricosPuedenLeerseSinVolverAGuardarlos()
        {
            // Lectura y normalización histórica a clave canónica
            Assert.AreEqual("CL_PENDIENTE_DIRCAV", _estadoService.NormalizarClaveInstitucional("Enviado DCAV"));
            Assert.AreEqual("CL_FIRMADA_DIRCAV", _estadoService.NormalizarClaveInstitucional("Firmado DCAV"));
            Assert.AreEqual("CL_FIRMADA_DIRCAV", _estadoService.NormalizarClaveInstitucional("CONDICIONES_FIRMADAS_DCAV"));
            Assert.AreEqual("ENTREGADO", _estadoService.NormalizarClaveInstitucional("FINALIZADO"));

            // Estado final reconoce los estados históricos terminales sin alterarlos
            Assert.IsTrue(_estadoService.EsEstadoFinal("FINALIZADO"));
            Assert.IsTrue(_estadoService.EsEstadoFinal("CERRADO"));
            Assert.IsTrue(_estadoService.EsEstadoFinal(AocrEstadosProceso.Entregado));

            // Transiciones históricas hacia la ruta canónica para migración en lectura
            Assert.IsTrue(_flujoService.EsTransicionPermitida("Enviado DCAV", AocrEstadosProceso.ClPendienteDircav));
            Assert.IsTrue(_flujoService.EsTransicionPermitida("Firmado DCAV", AocrEstadosProceso.AocrPendienteDirdac),
                "Un expediente histórico con 'Firmado DCAV' puede avanzar a DIRDAC para completar ambas firmas.");
        }
    }
}
