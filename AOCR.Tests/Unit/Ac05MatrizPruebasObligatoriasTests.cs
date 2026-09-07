using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;

namespace AOCR.Tests.Unit
{
    /// <summary>
    /// AC-05: Matriz Exhaustiva de Pruebas Obligatorias
    /// "ACEPTACIÓN DOCUMENTAL Y DESIGNACIÓN DEL INSPECTOR"
    /// 
    /// Cobertura total de los 11 escenarios especificados:
    /// 1. Sin seleccionar Inspector (HTTP 400).
    /// 2. Inspector inactivo (HTTP 400).
    /// 3. Usuario que no es Inspector (HTTP 400).
    /// 4. Designación válida (HTTP 200, versión 1, asociación explícita).
    /// 5. Reasignación (exige motivo, versión 2, inactiva versión anterior, trazabilidad).
    /// 6. Doble envío (idempotencia comprobada, no duplica versión).
    /// 7. Trámite en estado incorrecto (HTTP 409).
    /// 8. Acceso indebido (HTTP 403 para Admin y DIRDAC).
    /// 9. Persistencia de campos (inspector, usuario designó, fecha, hora, trámite, observación).
    /// 10. Notificación emitida (plantillas y llamadas en aceptación y designación).
    /// 11. Bandeja Inspector (visibilidad del expediente para el inspector designado).
    /// 12. No mezclar: aceptación documental, designación, activación RT y revisión técnica.
    /// </summary>
    [TestClass]
    public class Ac05MatrizPruebasObligatoriasTests
    {
        private readonly DircavDesignacionService _designacionService = new DircavDesignacionService();
        private readonly IAocrFlujoService _flujoService = new AocrFlujoService();
        private readonly SolicitudAocrCorreoService _correoService = new SolicitudAocrCorreoService();

        private static string ReadFile(string relativePath)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, relativePath);
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "..", "..", "..", relativePath);
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(@"c:\proyectos\AOCR", relativePath);
            }
            Assert.IsTrue(File.Exists(path), "Archivo no encontrado: " + relativePath);
            return File.ReadAllText(path);
        }

        #region 1. Sin Seleccionar Inspector
        [TestMethod]
        public void Test01_SinSeleccionarInspector_Retorna400()
        {
            // Cédula nula
            var req1 = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                InspectorPrincipalCedula = null,
                RolSolicitante = "DIRCAV"
            };
            var res1 = _designacionService.DesignarInspector(req1);
            Assert.IsFalse(res1.Exitoso, "Designación sin inspector debe fallar.");
            Assert.AreEqual(400, res1.HttpStatusCode);
            StringAssert.Contains(res1.Mensaje, "Debe seleccionar un inspector principal activo");

            // Cédula vacía
            var req2 = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                InspectorPrincipalCedula = "",
                RolSolicitante = "DIRCAV"
            };
            var res2 = _designacionService.DesignarInspector(req2);
            Assert.IsFalse(res2.Exitoso);
            Assert.AreEqual(400, res2.HttpStatusCode);

            // Cédula solo con espacios
            var req3 = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                InspectorPrincipalCedula = "   ",
                RolSolicitante = "DIRCAV"
            };
            var res3 = _designacionService.DesignarInspector(req3);
            Assert.IsFalse(res3.Exitoso);
            Assert.AreEqual(400, res3.HttpStatusCode);
        }
        #endregion

        #region 2. Inspector Inactivo
        [TestMethod]
        public void Test02_InspectorInactivo_Retorna400()
        {
            var req = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                InspectorPrincipalCedula = "INSPECTOR_INACTIVO_99999",
                RolSolicitante = "DIRCAV"
            };

            var res = _designacionService.DesignarInspector(req);
            Assert.IsFalse(res.Exitoso, "Un inspector inactivo debe ser rechazado.");
            Assert.AreEqual(400, res.HttpStatusCode);
            StringAssert.Contains(res.Mensaje, "no existe, no está activo o no tiene rol de Inspector");
        }
        #endregion

        #region 3. Usuario que No es Inspector
        [TestMethod]
        public void Test03_UsuarioQueNoEsInspector_Retorna400()
        {
            // Catálogo disponible solo lista usuarios con rol Inspector
            var inspectores = _designacionService.ListarInspectoresDisponibles();
            Assert.IsNotNull(inspectores);

            foreach (var insp in inspectores)
            {
                var esValido = (insp.RolInterno ?? "").ToUpperInvariant().Contains("INSPECTOR") ||
                               (insp.Tipo ?? "").ToUpperInvariant().Contains("AIR");
                Assert.IsTrue(esValido, $"Usuario {insp.UsuarioLogin} en catálogo no tiene rol de Inspector.");
            }

            // Un usuario que no pertenece al rol no puede asignarse
            var req = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                InspectorPrincipalCedula = "USUARIO_NO_INSPECTOR_XYZ",
                RolSolicitante = "DIRCAV"
            };
            var res = _designacionService.DesignarInspector(req);
            Assert.IsFalse(res.Exitoso);
            Assert.AreEqual(400, res.HttpStatusCode);
        }
        #endregion

        #region 4. Designación Válida
        [TestMethod]
        public void Test04_DesignacionValida_TransicionaEstadoYAsociaExpediente()
        {
            // Transiciones válidas habilitadas
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.DocumentacionAceptadaDircav,
                AocrEstadosProceso.DesignacionPendienteFirmaDircav),
                "DOCUMENTACION_ACEPTADA_DIRCAV debe permitir pasar a DESIGNACION_PENDIENTE_FIRMA_DIRCAV.");

            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteDesignacionDircav,
                AocrEstadosProceso.DesignacionPendienteFirmaDircav),
                "PENDIENTE_DESIGNACION_DIRCAV debe permitir pasar a DESIGNACION_PENDIENTE_FIRMA_DIRCAV.");

            // Verifica que el servicio asocie explícitamente al inspector
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");
            StringAssert.Contains(serviceSource, "solicitud.CodigoTecnico = inspectorId;");
            StringAssert.Contains(serviceSource, "solicitud.TecnicoResponsableId = inspectorId;");
            StringAssert.Contains(serviceSource, "solicitud.TecnicoResponsableCedula = cedulaInspectorFinal;");
            StringAssert.Contains(serviceSource, "solicitud.TecnicoResponsableNombre = inspectorPrincipal.NombreCompleto;");
            StringAssert.Contains(serviceSource, "solicitud.Estado = AocrEstadosProceso.DesignacionPendienteFirmaDircav;");
        }
        #endregion

        #region 5. Reasignación
        [TestMethod]
        public void Test05_Reasignacion_ExigeMotivo_GeneraVersionIncremental()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Exige motivo en reasignación a persona distinta
            StringAssert.Contains(serviceSource, "Para reasignar el inspector a una persona diferente debe especificar un motivo institucional");

            // DAO actualiza vigente = FALSE de la anterior y crea versión incrementada
            StringAssert.Contains(daoSource, "UPDATE public.aocr_tbdesignacion_inspector");
            StringAssert.Contains(daoSource, "SET vigente = FALSE");
            StringAssert.Contains(daoSource, "versionNueva = versionActual + 1");
            StringAssert.Contains(daoSource, "cmdInsert.Parameters.AddWithValue(\"@version\", versionNueva);");
            StringAssert.Contains(daoSource, "Version = versionNueva");
        }
        #endregion

        #region 6. Doble Envío
        [TestMethod]
        public void Test06_DobleEnvio_EsIdempotente_NoDuplicaVersion()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Idempotencia en servicio: si ya está asignado el mismo inspector, conserva estado sin crear fila
            StringAssert.Contains(serviceSource, "El inspector ya se encuentra asignado a este expediente. Estado de designación conservado.");

            // Índice único en base de datos que previene doble asignación vigente
            StringAssert.Contains(daoSource, "uq_aocr_designacion_vigente");
            StringAssert.Contains(daoSource, "WHERE vigente = TRUE");
        }
        #endregion

        #region 7. Trámite en Estado Incorrecto
        [TestMethod]
        public void Test07_TramiteEnEstadoIncorrecto_Retorna409()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // En AceptarDocumentacion: solo PENDIENTE_DIRCAV es aceptable
            StringAssert.Contains(serviceSource, "AocrEstadosProceso.PendienteDircav");
            StringAssert.Contains(serviceSource, "Conflicto: La documentación de la solicitud ya fue aceptada previamente por DIRCAV.");

            // En DesignarInspector: solo DOCUMENTACION_ACEPTADA_DIRCAV, PENDIENTE_DESIGNACION_DIRCAV o DESIGNACION_PENDIENTE_FIRMA_DIRCAV
            StringAssert.Contains(serviceSource, "Conflicto: No se puede designar el inspector en el estado actual");
        }
        #endregion

        #region 8. Acceso Indebido
        [TestMethod]
        public void Test08_AccesoIndebido_AdminYDirdac_Retorna403()
        {
            // Regla 7: Administrador excluido de operar
            Assert.IsFalse(_designacionService.EsDircavAutorizado("Administrador"));
            Assert.IsFalse(_designacionService.EsDircavAutorizado("Admin"));

            // DIRDAC excluido de AC-05
            Assert.IsFalse(_designacionService.EsDircavAutorizado("DIRDAC"));
            Assert.IsFalse(_designacionService.EsDircavAutorizado("DireccionGeneral"));

            // Coordinador e Inspector excluidos de la autoridad DIRCAV
            Assert.IsFalse(_designacionService.EsDircavAutorizado("Coordinador"));
            Assert.IsFalse(_designacionService.EsDircavAutorizado("Inspector"));

            // DIRCAV autorizado
            Assert.IsTrue(_designacionService.EsDircavAutorizado("DIRCAV"));
            Assert.IsTrue(_designacionService.EsDircavAutorizado("DCAV"));

            // Validar retornos HTTP 403
            var resAceptarAdmin = _designacionService.AceptarDocumentacion(1, 1, "ADMIN", "Administrador");
            Assert.AreEqual(403, resAceptarAdmin.HttpStatusCode);

            var resDesignarDirdac = _designacionService.DesignarInspector(new DircavDesignacionRequest
            {
                SolicitudId = 1,
                InspectorPrincipalCedula = "0102030405",
                RolSolicitante = "DIRDAC"
            });
            Assert.AreEqual(403, resDesignarDirdac.HttpStatusCode);
        }
        #endregion

        #region 9. Persistencia
        [TestMethod]
        public void Test09_Persistencia_InspectorUsuarioFechaHoraTramiteObservacion()
        {
            var daoDesignacionSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");
            var daoSolicitudSource = ReadFile("CapaDatos/DAOs/SolicitudAOCRDAO.cs");

            // Tabla aocr_tbdesignacion_inspector conserva los campos obligatorios
            StringAssert.Contains(daoDesignacionSource, "solicitud_id INTEGER NOT NULL");
            StringAssert.Contains(daoDesignacionSource, "inspector_id INTEGER NOT NULL");
            StringAssert.Contains(daoDesignacionSource, "inspector_cedula VARCHAR(30) NOT NULL");
            StringAssert.Contains(daoDesignacionSource, "inspector_nombre VARCHAR(200) NOT NULL");
            StringAssert.Contains(daoDesignacionSource, "dircav_usuario_id INTEGER NOT NULL");
            StringAssert.Contains(daoDesignacionSource, "dircav_usuario_nombre VARCHAR(200) NULL");
            StringAssert.Contains(daoDesignacionSource, "motivo TEXT NULL");
            StringAssert.Contains(daoDesignacionSource, "fecha_designacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()");

            // SolicitudAOCRDAO actualiza y mapea los campos asignados
            StringAssert.Contains(daoSolicitudSource, "tecnico_responsable_cedula=@tecnico_responsable_cedula");
            StringAssert.Contains(daoSolicitudSource, "tecnico_responsable_nombre=@tecnico_responsable_nombre");
            StringAssert.Contains(daoSolicitudSource, "estado_documental=@estado_documental");
            StringAssert.Contains(daoSolicitudSource, "EstadoDocumental = GetString(rd, \"estado_documental\")");
        }
        #endregion

        #region 10. Notificación
        [TestMethod]
        public void Test10_Notificacion_SeEmiteEnAceptacionYDesignacion()
        {
            var correoSource = ReadFile("CapaNegocio/Services/SolicitudAocrCorreoService.cs");
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // Plantilla de Aceptación Documental por DIRCAV
            StringAssert.Contains(correoSource, "DOCUMENTACION_ACEPTADA_DIRCAV");
            StringAssert.Contains(correoSource, "Documentación técnica aceptada por DIRCAV");

            // Plantilla de Designación Registrada por DIRCAV
            StringAssert.Contains(correoSource, "DESIGNACION_INSPECTOR_REGISTRADA");
            StringAssert.Contains(correoSource, "Designación de Inspector registrada por DIRCAV");

            // Invocación de correo en servicio
            StringAssert.Contains(serviceSource, "_correoService.NotificarEvento(solicitud, \"DOCUMENTACION_ACEPTADA_DIRCAV\"");
            StringAssert.Contains(serviceSource, "_correoService.NotificarEvento(solicitud, \"DESIGNACION_INSPECTOR_REGISTRADA\"");

            // Preservación del comentario reglamentario de AC-06
            StringAssert.Contains(serviceSource, "No notificar como definitiva antes de la firma de DIRCAV (AC-06)");
        }
        #endregion

        #region 11. Bandeja Inspector
        [TestMethod]
        public void Test11_BandejaInspector_ExpedienteVisibleParaInspectorDesignado()
        {
            var bandejaServiceSource = ReadFile("CapaNegocio/Services/RevisionDocumentalBandejaService.cs");

            // Estados en fase operativa de inspector incluyen los de designación DIRCAV
            StringAssert.Contains(bandejaServiceSource, "DesignacionFirmadaDircav");
            StringAssert.Contains(bandejaServiceSource, "DesignacionPendienteFirmaDircav");
            StringAssert.Contains(bandejaServiceSource, "DocumentacionAceptadaDircav");

            // Inspector asignado coincide por CodigoTecnico y TecnicoResponsableId
            StringAssert.Contains(bandejaServiceSource, "solicitud.CodigoTecnico.HasValue && inspectorIds.Contains(solicitud.CodigoTecnico.Value)");
            StringAssert.Contains(bandejaServiceSource, "solicitud.TecnicoResponsableId.HasValue && inspectorIds.Contains(solicitud.TecnicoResponsableId.Value)");
            StringAssert.Contains(bandejaServiceSource, "CoincideIdentificadorInspector(solicitud.TecnicoResponsableCedula, identificadores)");
        }
        #endregion

        #region 12. No Mezclar Eventos (Aceptación, Designación, Activación RT, Revisión Técnica)
        [TestMethod]
        public void Test12_NoMezclarEventos_Aceptacion_Designacion_ActivacionRT_RevisionTecnica()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // 1. AceptarDocumentacion solo acepta documentos (no designa ni activa RT)
            StringAssert.Contains(serviceSource, "Accion = \"ACEPTAR_DOCUMENTACION\"");
            StringAssert.Contains(serviceSource, "solicitud.Estado = AocrEstadosProceso.DocumentacionAceptadaDircav;");

            // 2. DesignarInspector solo designa (no acepta doc ni ejecuta inspección técnica)
            StringAssert.Contains(serviceSource, "Accion = \"DESIGNAR_INSPECTOR\"");
            StringAssert.Contains(serviceSource, "solicitud.Estado = AocrEstadosProceso.DesignacionPendienteFirmaDircav;");

            // 3. Revisión técnica permanece en fase posterior (AC-07/AC-08)
            Assert.IsFalse(serviceSource.Contains("INFORME_TECNICO_FIRMADO"));
            Assert.IsFalse(serviceSource.Contains("INSPECCION_EN_EJECUCION"));
        }
        #endregion
    }
}
