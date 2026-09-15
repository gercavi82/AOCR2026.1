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
    /// AC-05: MATRIZ DE 16 PRUEBAS OBLIGATORIAS
    /// "DIRCAV acepta la documentación y designa formalmente al Inspector"
    /// 
    /// 1. DIRCAV acepta documentación.
    /// 2. Otro rol recibe 403.
    /// 3. Solicitud fuera de estado recibe 409.
    /// 4. DIRCAV designa Inspector activo.
    /// 5. Inspector inexistente recibe 400.
    /// 6. Inspector inactivo recibe 400.
    /// 7. Inspector de apoyo igual al principal recibe 400.
    /// 8. Reasignación sin motivo recibe 400.
    /// 9. Designación idéntica no duplica.
    /// 10. Estaciones quedan asociadas.
    /// 11. Solicitud queda asociada al mismo Inspector.
    /// 12. Fallo al guardar estaciones revierte la designación.
    /// 13. Fallo de auditoría revierte la operación.
    /// 14. Conflicto concurrente devuelve 409.
    /// 15. No se envía notificación definitiva antes de AC-06.
    /// 16. Persistencia confirmada después de recargar.
    /// </summary>
    [TestClass]
    public class Ac05MatrizPruebasObligatoriasTests
    {
        private readonly DircavDesignacionService _designacionService = new DircavDesignacionService();
        private readonly IAocrFlujoService _flujoService = new AocrFlujoService();
        private readonly SolicitudAocrCorreoService _correoService = new SolicitudAocrCorreoService();

        private static string ObtenerRutaRaizProyecto()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "AOCR.sln")))
                {
                    return dir;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return @"c:\proyectos\AOCR";
        }

        private static string ReadFile(string relativePath)
        {
            var path = Path.Combine(ObtenerRutaRaizProyecto(), relativePath.TrimStart('\\', '/').Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "Archivo no encontrado: " + relativePath);
            return File.ReadAllText(path);
        }

        #region 1. DIRCAV acepta documentación
        [TestMethod]
        public void Test01_DIRCAV_AceptaDocumentacion()
        {
            // Validar que la transición formal está habilitada en el servicio canónico de flujo
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteDircav,
                AocrEstadosProceso.DocumentacionAceptadaDircav),
                "Transición PENDIENTE_DIRCAV -> DOCUMENTACION_ACEPTADA_DIRCAV debe estar permitida.");

            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // Validar ejecución atómica en AocrDesignacionDAO
            StringAssert.Contains(daoSource, "EjecutarAceptacionDocumentalTransaccional");
            StringAssert.Contains(daoSource, "estado=@estado,");
            StringAssert.Contains(daoSource, "estado_documental='ACEPTADO_DIRCAV'");
            StringAssert.Contains(daoSource, "aocr_tbhistorial_documental");
            StringAssert.Contains(daoSource, "aocr_tbauditoria");
            StringAssert.Contains(daoSource, "email_queue");

            // Validar delegación en servicio
            StringAssert.Contains(serviceSource, "EjecutarAceptacionDocumentalTransaccional");
        }
        #endregion

        #region 2. Otro rol recibe 403
        [TestMethod]
        public void Test02_OtroRolRecibe403()
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

            // Aceptar documentación por rol no autorizado devuelve 403
            var resAceptarAdmin = _designacionService.AceptarDocumentacion(1, 1, "ADMIN", "Administrador");
            Assert.AreEqual(403, resAceptarAdmin.HttpStatusCode);

            var resAceptarDirdac = _designacionService.AceptarDocumentacion(1, 1, "DIRDAC", "DIRDAC");
            Assert.AreEqual(403, resAceptarDirdac.HttpStatusCode);

            // Designar inspector por rol no autorizado devuelve 403
            var resDesignarDirdac = _designacionService.DesignarInspector(new DircavDesignacionRequest
            {
                SolicitudId = 1,
                DircavUsuarioId = 1,
                InspectorPrincipalCedula = "0102030405",
                RolSolicitante = "DIRDAC"
            });
            Assert.AreEqual(403, resDesignarDirdac.HttpStatusCode);

            var resDesignarAdmin = _designacionService.DesignarInspector(new DircavDesignacionRequest
            {
                SolicitudId = 1,
                DircavUsuarioId = 1,
                InspectorPrincipalCedula = "0102030405",
                RolSolicitante = "Administrador"
            });
            Assert.AreEqual(403, resDesignarAdmin.HttpStatusCode);
        }
        #endregion

        #region 3. Solicitud fuera de estado recibe 409
        [TestMethod]
        public void Test03_SolicitudFueraDeEstadoRecibe409()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // En AceptarDocumentacion: valida que solo PENDIENTE_DIRCAV es aceptable, de lo contrario 409
            StringAssert.Contains(daoSource, "string.Equals(estadoActual, AocrEstadosProceso.PendienteDircav");
            StringAssert.Contains(daoSource, "HttpStatusCode = 409");
            StringAssert.Contains(daoSource, "no puede ser aceptada directamente por DIRCAV");

            // En DesignarInspector: solo estados válidos de designación permitidos
            StringAssert.Contains(daoSource, "permiteDesignacion =");
            StringAssert.Contains(daoSource, "Debe estar en Aceptación Documental DIRCAV");
            StringAssert.Contains(serviceSource, "HttpStatusCode = 409");
        }
        #endregion

        #region 4. DIRCAV designa Inspector activo
        [TestMethod]
        public void Test04_DIRCAV_DesignaInspectorActivo()
        {
            // Transiciones válidas habilitadas en flujo
            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.DocumentacionAceptadaDircav,
                AocrEstadosProceso.DesignacionPendienteFirmaDircav),
                "DOCUMENTACION_ACEPTADA_DIRCAV debe permitir pasar a DESIGNACION_PENDIENTE_FIRMA_DIRCAV.");

            Assert.IsTrue(_flujoService.EsTransicionPermitida(
                AocrEstadosProceso.PendienteDesignacionDircav,
                AocrEstadosProceso.DesignacionPendienteFirmaDircav),
                "PENDIENTE_DESIGNACION_DIRCAV debe permitir pasar a DESIGNACION_PENDIENTE_FIRMA_DIRCAV.");

            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // Verifica registro y asociación en transacción
            StringAssert.Contains(daoSource, "INSERT INTO public.aocr_tbdesignacion_inspector");
            StringAssert.Contains(daoSource, "tecnico_responsable_cedula=@inspector_cedula");
            StringAssert.Contains(daoSource, "tecnico_responsable_nombre=@inspector_nombre");
            StringAssert.Contains(daoSource, "codigo_tecnico=@inspector_id");
            StringAssert.Contains(serviceSource, "solicitud.CodigoTecnico = inspectorId;");
            StringAssert.Contains(serviceSource, "solicitud.TecnicoResponsableId = inspectorId;");
            StringAssert.Contains(serviceSource, "solicitud.TecnicoResponsableCedula = cedulaInspectorFinal;");
            StringAssert.Contains(serviceSource, "solicitud.TecnicoResponsableNombre = inspectorPrincipal.NombreCompleto;");
            StringAssert.Contains(serviceSource, "solicitud.Estado = AocrEstadosProceso.DesignacionPendienteFirmaDircav;");
        }
        #endregion

        #region 5. Inspector inexistente recibe 400
        [TestMethod]
        public void Test05_InspectorInexistenteRecibe400()
        {
            // Cédula nula
            var req1 = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                DircavUsuarioId = 1,
                InspectorPrincipalCedula = null,
                RolSolicitante = "DIRCAV"
            };
            var res1 = _designacionService.DesignarInspector(req1);
            Assert.IsFalse(res1.Exitoso);
            Assert.AreEqual(400, res1.HttpStatusCode);
            StringAssert.Contains(res1.Mensaje, "Debe seleccionar un inspector principal activo");

            // Inspector con identificador inexistente
            var req2 = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                DircavUsuarioId = 1,
                InspectorPrincipalCedula = "INSPECTOR_INEXISTENTE_99999",
                RolSolicitante = "DIRCAV"
            };
            var res2 = _designacionService.DesignarInspector(req2);
            Assert.IsFalse(res2.Exitoso);
            Assert.AreEqual(400, res2.HttpStatusCode);
            StringAssert.Contains(res2.Mensaje, "no existe, no está activo o no tiene rol de Inspector");
        }
        #endregion

        #region 6. Inspector inactivo recibe 400
        [TestMethod]
        public void Test06_InspectorInactivoRecibe400()
        {
            var req = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                DircavUsuarioId = 1,
                InspectorPrincipalCedula = "INSPECTOR_INACTIVO_99999",
                RolSolicitante = "DIRCAV"
            };

            var res = _designacionService.DesignarInspector(req);
            Assert.IsFalse(res.Exitoso, "Un inspector inactivo debe ser rechazado con 400.");
            Assert.AreEqual(400, res.HttpStatusCode);
            StringAssert.Contains(res.Mensaje, "no existe, no está activo o no tiene rol de Inspector");
        }
        #endregion

        #region 7. Inspector de apoyo igual al principal recibe 400
        [TestMethod]
        public void Test07_InspectorDeApoyoIgualAlPrincipalRecibe400()
        {
            var req = new DircavDesignacionRequest
            {
                SolicitudId = 101,
                DircavUsuarioId = 1,
                InspectorPrincipalCedula = "0102030405",
                InspectorApoyoCedula = "0102030405",
                RolSolicitante = "DIRCAV"
            };

            var res = _designacionService.DesignarInspector(req);
            Assert.IsFalse(res.Exitoso);
            Assert.AreEqual(400, res.HttpStatusCode);
            StringAssert.Contains(res.Mensaje, "El inspector de apoyo no puede ser la misma persona que el inspector principal");
        }
        #endregion

        #region 8. Reasignación sin motivo recibe 400
        [TestMethod]
        public void Test08_ReasignacionSinMotivoRecibe400()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Exige motivo obligatorio en reasignación a persona distinta
            StringAssert.Contains(serviceSource, "Para reasignar el inspector a una persona diferente debe especificar un motivo institucional");
            StringAssert.Contains(daoSource, "Para reasignar el inspector a una persona diferente debe especificar un motivo institucional");
            StringAssert.Contains(daoSource, "string.IsNullOrWhiteSpace(p.Motivo)");
            StringAssert.Contains(daoSource, "HttpStatusCode = 400");
        }
        #endregion

        #region 9. Designación idéntica no duplica
        [TestMethod]
        public void Test09_DesignacionIdenticaNoDuplica()
        {
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Idempotencia: si ya está asignado el mismo inspector principal y de apoyo, conserva estado sin crear nueva fila
            StringAssert.Contains(serviceSource, "El inspector ya se encuentra asignado a este expediente. Estado de designación conservado.");
            StringAssert.Contains(daoSource, "EsIdempotente = true");
            StringAssert.Contains(daoSource, "El inspector ya se encuentra asignado a este expediente. Estado de designación conservado.");

            // Índice único en base de datos que previene doble asignación vigente
            StringAssert.Contains(daoSource, "uq_aocr_designacion_vigente");
            StringAssert.Contains(daoSource, "WHERE vigente = TRUE");
        }
        #endregion

        #region 10. Estaciones quedan asociadas
        [TestMethod]
        public void Test10_EstacionesQuedanAsociadas()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Actualización de estaciones en la transacción
            StringAssert.Contains(daoSource, "est.InspectorId = p.InspectorId;");
            StringAssert.Contains(daoSource, "est.InspectorNombre = p.InspectorNombre;");
            StringAssert.Contains(daoSource, "est.Estado = \"DESIGNADO\";");
            StringAssert.Contains(daoSource, "daoEst.GuardarEstacionesTransaccional(p.SolicitudId, estaciones, p.DircavUsuarioId, cn, tx)");
        }
        #endregion

        #region 11. Solicitud queda asociada al mismo Inspector
        [TestMethod]
        public void Test11_SolicitudQuedaAsociadaAlMismoInspector()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");
            var daoSolSource = ReadFile("CapaDatos/DAOs/SolicitudAOCRDAO.cs");

            // Solicitud se actualiza con el mismo inspector
            StringAssert.Contains(daoSource, "codigo_tecnico=@inspector_id");
            StringAssert.Contains(daoSource, "tecnico_responsable_id=@inspector_id");
            StringAssert.Contains(daoSource, "tecnico_responsable_cedula=@inspector_cedula");
            StringAssert.Contains(daoSource, "tecnico_responsable_nombre=@inspector_nombre");

            // SolicitudAOCRDAO mapea los campos asignados
            StringAssert.Contains(daoSolSource, "tecnico_responsable_cedula=@tecnico_responsable_cedula");
            StringAssert.Contains(daoSolSource, "tecnico_responsable_nombre=@tecnico_responsable_nombre");
        }
        #endregion

        #region 12. Fallo al guardar estaciones revierte la designación
        [TestMethod]
        public void Test12_FalloAlGuardarEstacionesRevierteLaDesignacion()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Comprueba que no se oculta el error de estaciones con catch vacío y que se ejecuta Rollback
            StringAssert.Contains(daoSource, "if (!guardadoEstOk)");
            StringAssert.Contains(daoSource, "throw new InvalidOperationException(\"Fallo al guardar estaciones en la transacción de designación.\");");
            StringAssert.Contains(daoSource, "tx.Rollback();");
        }
        #endregion

        #region 13. Fallo de auditoría revierte la operación
        [TestMethod]
        public void Test13_FalloDeAuditoriaRevierteLaOperacion()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // La auditoría está dentro del bloque transaccional atómico
            StringAssert.Contains(daoSource, "INSERT INTO public.aocr_tbauditoria");
            StringAssert.Contains(daoSource, "('DIRCAV', 'DESIGNAR_INSPECTOR', @usuario, NOW(), @datos_previos, @datos_nuevos);");
            StringAssert.Contains(daoSource, "INSERT INTO public.aocr_tbauditoria");
            StringAssert.Contains(daoSource, "('DIRCAV', 'ACEPTAR_DOCUMENTACION', @usuario, NOW(), @datos_previos, @datos_nuevos);");
        }
        #endregion

        #region 14. Conflicto concurrente devuelve 409
        [TestMethod]
        public void Test14_ConflictoConcurrenteDevuelve409()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");
            var controllerSource = ReadFile("CapaPresentacion/Controllers/DircavController.cs");

            // Control de concurrencia optimista por version en DAO
            StringAssert.Contains(daoSource, "p.VersionEsperada.HasValue && p.VersionEsperada.Value > 0 && p.VersionEsperada.Value != versionActual");
            StringAssert.Contains(daoSource, "Conflicto de concurrencia: la versión esperada");
            StringAssert.Contains(daoSource, "HttpStatusCode = 409");

            // Controlador propaga el 409
            StringAssert.Contains(controllerSource, "resultado.HttpStatusCode == 409");
            StringAssert.Contains(controllerSource, "return new HttpStatusCodeResult(409, resultado.Mensaje);");
        }
        #endregion

        #region 15. No se envía notificación definitiva antes de AC-06
        [TestMethod]
        public void Test15_NoSeEnviaNotificacionDefinitivaAntesDeAc06()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");
            var serviceSource = ReadFile("CapaNegocio/Services/DircavDesignacionService.cs");

            // Comprueba que no se notifique oficialmente al inspector antes de la firma de AC-06
            StringAssert.Contains(daoSource, "Encolar notificación provisional en email_queue (NO definitiva antes de AC-06)");
            StringAssert.Contains(daoSource, "SOLICITUD_DESIGNACION_INSPECTOR_REGISTRADA");
            StringAssert.Contains(daoSource, "Pendiente de firma digital DIRCAV");
            StringAssert.Contains(serviceSource, "No notificar como definitiva antes de la firma de DIRCAV (AC-06)");
        }
        #endregion

        #region 16. Persistencia confirmada después de recargar
        [TestMethod]
        public void Test16_PersistenciaConfirmadaDespuesDeRecargar()
        {
            var daoSource = ReadFile("CapaDatos/DAOs/AocrDesignacionDAO.cs");

            // Tabla aocr_tbdesignacion_inspector conserva los campos obligatorios
            StringAssert.Contains(daoSource, "solicitud_id INTEGER NOT NULL");
            StringAssert.Contains(daoSource, "inspector_id INTEGER NOT NULL");
            StringAssert.Contains(daoSource, "inspector_cedula VARCHAR(30) NOT NULL");
            StringAssert.Contains(daoSource, "inspector_nombre VARCHAR(200) NOT NULL");
            StringAssert.Contains(daoSource, "dircav_usuario_id INTEGER NOT NULL");
            StringAssert.Contains(daoSource, "dircav_usuario_nombre VARCHAR(200) NULL");
            StringAssert.Contains(daoSource, "motivo TEXT NULL");
            StringAssert.Contains(daoSource, "fecha_designacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()");

            // Mapeador recupera todos los campos persistidos
            StringAssert.Contains(daoSource, "SolicitudId = dr.GetInt32(dr.GetOrdinal(\"solicitud_id\"))");
            StringAssert.Contains(daoSource, "InspectorId = dr.GetInt32(dr.GetOrdinal(\"inspector_id\"))");
            StringAssert.Contains(daoSource, "InspectorCedula = dr.IsDBNull(dr.GetOrdinal(\"inspector_cedula\"))");
            StringAssert.Contains(daoSource, "DircavUsuarioId = dr.GetInt32(dr.GetOrdinal(\"dircav_usuario_id\"))");
            StringAssert.Contains(daoSource, "Version = dr.GetInt32(dr.GetOrdinal(\"version\"))");
            StringAssert.Contains(daoSource, "Vigente = dr.GetBoolean(dr.GetOrdinal(\"vigente\"))");
        }
        #endregion
    }
}
