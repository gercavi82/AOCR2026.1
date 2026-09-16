# Manual Técnico y de Arquitectura — Sistema AOCR

**Dirección General de Aviación Civil (DGAC) — Ecuador**  
**Proyecto:** DGAC-006-05-02 *Otorgamiento del Reconocimiento del Certificado de Explotador de Servicios Aéreos (AOCR)*  
**Versión:** 2.0 (Septiembre 2026 — Incorporación AC-01 a AC-12, Roles DIRDAC/DIRCAV y Entrega Digital)  
**Público objetivo:** Desarrolladores, Arquitectos de Software, Administradores de BD, QA y Soporte Nivel 3.

---

## 1. Arquitectura General del Sistema

El sistema está construido bajo una arquitectura multicapa en **.NET Framework 4.8** utilizando el patrón **ASP.NET MVC 5**, persistencia relacional en **PostgreSQL**, e integraciones institucionales legadas.

```
┌─────────────────────────────────────────────────────────────┐
│                 CapaPresentacion (ASP.NET MVC 5)             │
│   Controladores, Filtros ([AocrAuthorize]), Vistas Razor    │
└──────────────────────────────┬──────────────────────────────┘
                               │ Inyección de dependencias / DTOs
┌──────────────────────────────▼──────────────────────────────┐
│                  CapaNegocio (Servicios y Reglas)           │
│   AocrFlujoService, IAocrFinalWorkflowService, EntregaFinal │
│   RevisionDocumentalService, SolicitudAocrInfraBL           │
└──────────────────────────────┬──────────────────────────────┘
                               │ Consultas parametrizadas / Transacciones
┌──────────────────────────────▼──────────────────────────────┐
│                    CapaDatos (DAOs y Persistencia)          │
│   Npgsql (PostgreSQL 14+), Transacciones ACID, Row Locking  │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                 Base de Datos Relacional (PostgreSQL)       │
│   Esquema transaccional, historial de estados, outbox queue │
└─────────────────────────────────────────────────────────────┘
```

### 1.1 Estructura de Proyectos de la Solución (`AOCR.sln`)
* **`CapaPresentacion`:** Controladores MVC, endpoints AJAX, filtros de autorización, validadores antiforgery y componentes visuales.
* **`CapaNegocio`:** Lógica de negocio, validación de reglas aeronáuticas, matrices de transición de estados y despacho de eventos.
* **`CapaDatos`:** Acceso a datos mediante ADO.NET con `NpgsqlConnection`, consultas SQL seguras parametrizadas, gestión de bloqueos transaccionales (`SELECT ... FOR UPDATE`).
* **`CapaModelo`:** Clases de dominio, entidades de base de datos, DTOs y enumeraciones de estado.
* **`AOCR.Tests`:** Pruebas unitarias y de integración (MSTest) que validan matrices de estados, seguridad y reglas de negocio.

---

## 2. Máquina de Estados y Flujo Institucional (AC-11 y AC-12)

El sistema opera mediante una máquina de estados estricta y auditable. Cada cambio de estado genera un registro inmutable en `aocr_tbhistorial_estado` y un evento en `aocr_evento_workflow`.

```text
               [ RT: Ingreso Solicitud ]
                          │
                          ▼
            [ Financiero: Aprobación de Pago ]
                          │
                          ▼
       [ Coordinador: Aceptación Documental (AC-05/06) ]
                          │
                          ▼
      [ Inspector: Revisión Documental Especializada ]
                          │
                          ▼
   [ Inspector: Listas de Verificación por Estación (AC-07/08) ]
                          │
                          ▼
          [ Inspector: Informe Técnico y Dictamen ]
                          │
                          ▼
       [ DIRCAV: Aprobación y Firma de Condiciones y Limitaciones ]
                          │
                          ▼ (AOCR_PENDIENTE_DIRDAC)
       [ DIRDAC: Revisión, Firma y Legalización de AOCR (AC-11) ]
                          │
                          ▼ (FIRMAS_COMPLETAS)
       [ Sistema: Entrega Final Segura a RT e Inspector (AC-12) ]
                          │
                          ▼
                    [ FINALIZADO ]
```

### 2.1 Detalle de Transiciones Críticas

1. **Fase Inspector a Dirección:**
   * Inspector finaliza Informe Técnico: `FIRMADO_INSPECTOR` $\rightarrow$ `PENDIENTE_REVISION_FINAL_COORDINADOR` $\rightarrow$ `PENDIENTE_REVISION_FINAL_DIRCAV`.
2. **Fase DIRCAV (Dirección de Certificación):**
   * Emite y firma electrónicamente el documento de Condiciones y Limitaciones: `CL_FIRMADA_DIRCAV`.
   * Invoca `RemitirAocrDirdac`: Estado cambia a `AOCR_PENDIENTE_DIRDAC`.
3. **Fase DIRDAC (Dirección General):**
   * Si detecta novedades, ejecuta `DevolverAocrDircav`: Retorna a DIRCAV con estado `DEVUELTO_DIRCAV`.
   * Si es aprobado, ejecuta `FirmarLegalizarAocr`: Estado `AOCR_FIRMADA_DIRDAC`.
4. **Consolidación Automática de Firmas:**
   * Al verificarse ambas firmas activas y vigentes: `FIRMAS_COMPLETAS` $\rightarrow$ Dispara evento `ENTREGA_FINAL_SOLICITADA` $\rightarrow$ `LISTO_PARA_ENTREGA` $\rightarrow$ `ENTREGADO`.

---

## 3. Matriz de Autorización y Seguridad

La seguridad se evalúa en capa de negocio y en filtros de controlador mediante `[AocrAuthorize]` y `AocrAuthorizationService`:

| Rol Canónico | Permisos Específicos Asignados | Restricciones Estrictas |
| :--- | :--- | :--- |
| **`Solicitante` (RT)** | `SOLICITUD_CREAR`, `DOCUMENTO_SUBIR`, `ENTREGA_FINAL_DESCARGAR` | Acceso restringido únicamente a solicitudes de su propia compañía aérea (validación por `compania_id`). |
| **`Financiero`** | `ORDEN_CONSULTAR`, `PAGO_APROBAR`, `FACTURA_REGISTRAR` | Sin acceso a inspecciones ni firmas de documentos técnicos. |
| **`Coordinacion`** | `INSPECTOR_ASIGNAR`, `DOCUMENTO_REVISION_PRELIMINAR`, `DESIGNACION_GENERAR` | No puede emitir dictámenes de inspección técnica ni firmar AOCR/C&L. |
| **`InspectorTecnico`** | `LV_EVALUAR`, `LV_FIRMAR`, `INFORME_ELABORAR`, `INFORME_FIRMAR` | Solo puede operar sobre solicitudes donde esté formalmente asignado (`CodigoTecnico` / `CodigoInspector`). |
| **`DIRCAV`** | `CL_GENERAR`, `CL_FIRMAR`, `DIRCAV_REMITIR_DIRDAC`, `DIRCAV_DEVOLVER` | Autoridad exclusiva para la firma de Condiciones y Limitaciones. |
| **`DIRDAC`** | `DIRDAC_VER_BANDEJA`, `DIRDAC_DEVOLVER_DIRCAV`, `DIRDAC_FIRMAR_AOCR` | Máxima autoridad: firma exclusiva del Certificado AOCR. Denegado para otros roles. |
| **`Administrador`** | Auditoría, monitoreo de colas y configuración de catálogos | **Denegado expresamente** para firmar documentos o modificar dictámenes operativos. |

---

## 4. Estructura de Base de Datos y Persistencia

### 4.1 Tablas Principales del Módulo AOCR
* **`aocr_tbsolicitud`:** Registro maestro del trámite, compañía operadora, tipo de solicitud y estado general.
* **`aocr_tbrevision_documental`:** Registro de decisiones (`ACEPTADO`, `DEVUELTO`) y observaciones por cada documento subido.
* **`aocr_tbinspeccion`:** Datos de la inspección técnica, inspector asignado y fechas por estación.
* **`aocr_lista_verificacion_operacional_eae`:** Encabezado y detalle de las listas de verificación independientes por estación (AC-07).
* **`aocr_tbdocumento_generado`:** Almacena los metadatos y versiones generadas de AOCR, C&L, Designaciones e Informes.
* **`aocr_tbfirma_documento`:** Registro inmutable de firmas electrónicas, identificador del servidor, fecha y hash SHA-256 del archivo.
* **`aocr_tbentrega_final`:** Snapshots de entrega digital para RT e Inspector (AC-12).
* **`email_queue`:** Cola asíncrona y transaccional de notificaciones y correos institucionales.

### 4.2 Control de Concurrencia e Idempotencia
* **Bloqueo Pesimista Controlado:** Las operaciones críticas de cambio de estado y firmas aplican `SELECT ... FOR UPDATE` sobre la solicitud y verificación del campo `version`.
* **Idempotencia de Eventos:** Los eventos en `aocr_evento_workflow` utilizan llaves compuestas únicas (`solicitud_id`, `operacion`, `version_aocr`, `actor_id`) evitando reprocesamientos por dobles clics o envíos repetidos.

---

## 5. Subsistema de Notificaciones y Entrega (AC-12)

### 5.1 Cola Asíncrona (`EmailQueueProcessor`)
El sistema no invoca servidores SMTP directamente en las solicitudes HTTP del usuario para evitar bloqueos:
1. Las operaciones registran el correo en la tabla `email_queue` dentro de la misma transacción de base de datos.
2. Un worker en segundo plano (`EmailQueueProcessor`) procesa los mensajes con estado `PENDIENTE` utilizando `FOR UPDATE SKIP LOCKED`.
3. **Política de Reintentos:**
   * Intento 1: Inmediato.
   * Intento 2: A los 5 minutos.
   * Intento 3: A los 15 minutos.
   * Intento 4: A los 60 minutos.
4. Tras agotar reintentos, el mensaje pasa a `ERROR_DEFINITIVO` sin interrumpir la operación del sistema.

### 5.2 Integridad y Descarga Segura
Antes de servir cualquier documento legalizado (`/Documento/DescargarLegalizado`):
* Se verifica que el usuario autenticado sea el RT de la compañía o el inspector asignado.
* Se comprueba la existencia física del archivo en el directorio restringido del servidor.
* Se valida la cabecera del archivo (`%PDF-`).
* Se recalcula y valida el hash **SHA-256** contra el registro de firma para garantizar que el archivo no haya sido alterado.

---

## 6. Configuración y Despliegue

### 6.1 Parámetros de Configuración (`Web.config`)
```xml
<appSettings>
  <!-- Rutas seguras de almacenamiento de documentos -->
  <add key="AOCR.RutaDocumentos" value="D:\AOCR_Storage\Documentos\" />
  <add key="AOCR.RutaFirmados" value="D:\AOCR_Storage\Firmados\" />
  
  <!-- Configuración de Colas y Correo Institucional -->
  <add key="Smtp.Host" value="mail.aviacioncivil.gob.ec" />
  <add key="Smtp.Port" value="587" />
  <add key="Smtp.EnableSsl" value="true" />
  
  <!-- Modo de Seguridad y Auditoría -->
  <add key="AOCR.ValidarSha256Descargas" value="true" />
</appSettings>
```

### 6.2 Procedimiento de Publicación en IIS
1. Compilar la solución en modo `Release` usando MSBuild:
   ```powershell
   & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" AOCR.sln /p:Configuration=Release /t:Clean,Build
   ```
2. Ejecutar los scripts de migración de base de datos en el servidor PostgreSQL correspondiente.
3. Detener el Application Pool en IIS (`Stop-WebAppPool "AOCR_Pool"`).
4. Copiar los archivos binarios compilados y vistas a la carpeta destino del sitio web.
5. Iniciar el Application Pool (`Start-WebAppPool "AOCR_Pool"`).
6. Verificar el log de inicialización en `CapaPresentacion/App_Data/Logs/AOCR_YYYYMMDD.log`.

---
*Documento técnico elaborado y mantenido por la Dirección de Tecnologías de la Información y Comunicación (DTIC) — DGAC Ecuador.*
