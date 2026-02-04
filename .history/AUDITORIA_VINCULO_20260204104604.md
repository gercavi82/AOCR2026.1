# AUDITORIA_VINCULO

## Proyecto AOCR (ASP.NET MVC5 + Dapper + PostgreSQL)
**Fecha de auditoria:** 3 de febrero de 2026

---

# 1) Resumen Ejecutivo (m�ximo 10 bullets)
- **P0:** `aocr_or_orden` en BD real solo tiene 8 columnas, pero el DAO inserta/actualiza/lee columnas inexistentes (`observacion`, `subtotal`, `admin`, `lugar_emision`, `correo`, `telefono`, `concepto_id`). Esto rompe INSERT/UPDATE/SELECT. (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:263-312, 399-413, 601-620)
- **P0:** `aocr_or_orden_detalle` en BD real no tiene `concepto_codigo`, `descripcion`, `porcentaje_admin`, `subtotal`, `admin`. El DAO inserta y mapea esas columnas ? error de columna inexistente. (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:363-382, 650-657)
- **P0:** `aocr_tbsolicitud` en BD real no tiene `tipo_solicitud`, `ciudad`, `provincia`, `pais`, `codigo_tecnico`, `created_at`, `updated_at`, `deleted_at`, `observaciones_generales`, etc. Los DAOs hacen SELECT/INSERT/UPDATE con esos nombres ? rompe consultas. (CapaDatos/DAOs/SolicitudDAO.cs:23-31; CapaDatos/DAOs/SolicitudAOCRDAO.cs:100-117)
- **P0:** `PagoDAO` usa tablas `pagos` y `ordenes_recaudacion` que **no existen** en el script SQL entregado. (CapaDatos/DAOs/PagoDAO.cs:25-33, 106-109)
- **P1:** Tipos inconsistentes: `CodigoUsuario`/`CodigoSolicitud` en entidad `OrdenRecaudacion` son `string`, pero BD los define como `INTEGER`. Se hacen conversiones manuales que pueden guardar NULL si no parsea. (CapaDatos/Entidades/OrdenRecaudacion.cs:27-33; CapaDatos/DAOs/OrdenRecaudacionDAO.cs:275-299)
- **P1:** En el JSON de `FormularioCompleto`, se usan propiedades de `SolicitudAOCR` que no existen en el esquema real (`ObservacionesGenerales`, `TipoSolicitud`, `Ciudad`, etc.). Se perder�n datos o fallar� si el DAO intenta persistirlos. (CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml:1182-1197; CapaDatos/DAOs/SolicitudAOCRDAO.cs:100-117)
- **P1:** `SolicitudDAO`/`SolicitudAOCRDAO` filtran por `deleted_at` que no existe en el script de BD real ? consultas fallan. (CapaDatos/DAOs/SolicitudAOCRDAO.cs:24, 47, 74-76)
- **P2:** JavaScript con cierre extra `};` puede romper ejecuci�n de scripts y causar �Unexpected token '}'�. (CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml:946-948)
- **P2:** Uso de operador de propagaci�n nula `?.` en Razor con C# 5 produce error de compilaci�n. (CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml:1177-1178)

---

# 2) Matriz de V�nculo C�digo ? DB (tabla)

> **Fuente DB:** Script SQL entregado por el usuario (chat), sin archivo en repo.

## Tabla: `aocr_tbsolicitud`
| Tabla DB | Columna DB (snake_case) | Tipo DB | Propiedad C# encontrada | Tipo C# | Evidencia | Estado | Correcci�n sugerida |
|---|---|---|---|---|---|---|---|
| aocr_tbsolicitud | codigo_solicitud | SERIAL PK | SolicitudAOCR.CodigoSolicitud | int | CapaModelo/SolicitudAOCR.cs:8 | OK | - |
| aocr_tbsolicitud | numero_solicitud | varchar(30) | SolicitudAOCR.NumeroSolicitud | string | CapaModelo/SolicitudAOCR.cs:9 | OK | - |
| aocr_tbsolicitud | fecha_solicitud | timestamp | SolicitudAOCR.FechaSolicitud | DateTime? | CapaModelo/SolicitudAOCR.cs:10 | OK | - |
| aocr_tbsolicitud | nombre_operador | varchar(100) | SolicitudAOCR.NombreOperador | string | CapaModelo/SolicitudAOCR.cs:14 | OK | - |
| aocr_tbsolicitud | ruc | varchar(20) | SolicitudAOCR.Ruc | string | CapaModelo/SolicitudAOCR.cs:15 | OK | - |
| aocr_tbsolicitud | razon_social | varchar(150) | SolicitudAOCR.RazonSocial | string | CapaModelo/SolicitudAOCR.cs:16 | OK | - |
| aocr_tbsolicitud | email | varchar(100) | SolicitudAOCR.Email | string | CapaModelo/SolicitudAOCR.cs:18 | OK | - |
| aocr_tbsolicitud | telefono | varchar(20) | SolicitudAOCR.Telefono | string | CapaModelo/SolicitudAOCR.cs:19 | OK | - |
| aocr_tbsolicitud | direccion | text | SolicitudAOCR.Direccion | string | CapaModelo/SolicitudAOCR.cs:20 | OK | - |
| aocr_tbsolicitud | representante_legal | varchar(100) | SolicitudAOCR.RepresentanteLegal | string | CapaModelo/SolicitudAOCR.cs:25 | OK | - |
| aocr_tbsolicitud | cedula_representante | varchar(20) | SolicitudAOCR.CedulaRepresentante | string | CapaModelo/SolicitudAOCR.cs:26 | OK | - |
| aocr_tbsolicitud | tipo_operacion | varchar(50) | SolicitudAOCR.TipoOperacion | string | CapaModelo/SolicitudAOCR.cs:28 | OK | - |
| aocr_tbsolicitud | descripcion_operacion | text | SolicitudAOCR.DescripcionOperacion | string | CapaModelo/SolicitudAOCR.cs:29 | OK | - |
| aocr_tbsolicitud | observaciones | text | SolicitudAOCR.Observaciones | string | CapaModelo/SolicitudAOCR.cs:30 | OK | - |
| aocr_tbsolicitud | estado | varchar(20) | SolicitudAOCR.Estado | string | CapaModelo/SolicitudAOCR.cs:12 | OK | - |
| aocr_tbsolicitud | codigo_usuario | integer | SolicitudAOCR.CodigoUsuario | int | CapaModelo/SolicitudAOCR.cs:32 | OK | - |
| aocr_tbsolicitud | tipo_solicitud | (NO EXISTE) | SolicitudAOCR.TipoSolicitud | int? | CapaModelo/SolicitudAOCR.cs:11 | ERROR CR�TICO | Quitar columna en DAO o agregar en BD |
| aocr_tbsolicitud | ciudad/provincia/pais | (NO EXISTE) | SolicitudAOCR.Ciudad/Provincia/Pais | string | CapaModelo/SolicitudAOCR.cs:21-23 | ERROR CR�TICO | Quitar del SQL/DAO o agregar en BD |
| aocr_tbsolicitud | codigo_tecnico/created_at/... | (NO EXISTE) | SolicitudAOCR.CodigoTecnico/CreatedAt/... | varios | CapaModelo/SolicitudAOCR.cs:33-44 | ERROR CR�TICO | Quitar del SQL/DAO o agregar en BD |

**Evidencia SQL en DAO que usa columnas inexistentes:**
- `SolicitudDAO` SELECT con `tipo_solicitud`, `ciudad`, `created_at`, etc. (CapaDatos/DAOs/SolicitudDAO.cs:23-31)
- `SolicitudAOCRDAO` INSERT con `tipo_solicitud`, `ciudad`, `created_at`, etc. (CapaDatos/DAOs/SolicitudAOCRDAO.cs:100-117)

## Tabla: `aocr_or_orden`
| Tabla DB | Columna DB (snake_case) | Tipo DB | Propiedad C# encontrada | Tipo C# | Evidencia | Estado | Correcci�n sugerida |
|---|---|---|---|---|---|---|---|
| aocr_or_orden | id | serial | OrdenRecaudacion.Id | int | CapaDatos/Entidades/OrdenRecaudacion.cs:23-25 | OK | - |
| aocr_or_orden | codigo_usuario | integer | OrdenRecaudacion.CodigoUsuario | string | CapaDatos/Entidades/OrdenRecaudacion.cs:27-29 | Riesgo | Cambiar a int o usar conversi�n consistente |
| aocr_or_orden | codigo_solicitud | integer | OrdenRecaudacion.CodigoSolicitud | string | CapaDatos/Entidades/OrdenRecaudacion.cs:31-33 | Riesgo | Cambiar a int o usar conversi�n consistente |
| aocr_or_orden | numero_orden | varchar(30) | OrdenRecaudacion.NumeroOrden | string | CapaDatos/Entidades/OrdenRecaudacion.cs:35-37 | OK | - |
| aocr_or_orden | fecha_creacion | timestamp | OrdenRecaudacion.FechaCreacion | DateTime | CapaDatos/Entidades/OrdenRecaudacion.cs:39-40 | OK | - |
| aocr_or_orden | estado | varchar(20) | OrdenRecaudacion.Estado | string | CapaDatos/Entidades/OrdenRecaudacion.cs:42-44 | OK | - |
| aocr_or_orden | compania | varchar(100) | OrdenRecaudacion.Compania | string | CapaDatos/Entidades/OrdenRecaudacion.cs:63-65 | OK | - |
| aocr_or_orden | ruc_cedula | varchar(20) | OrdenRecaudacion.RucCedula | string | CapaDatos/Entidades/OrdenRecaudacion.cs:67-69 | OK | - |
| aocr_or_orden | total | numeric(18,2) | OrdenRecaudacion.Total | decimal? | CapaDatos/Entidades/OrdenRecaudacion.cs:56-57 | OK | - |
| aocr_or_orden | observacion | (NO EXISTE) | OrdenRecaudacion.Observacion | string | CapaDatos/Entidades/OrdenRecaudacion.cs:46-48 | ERROR CR�TICO | Agregar columna en BD o eliminar en DAO |
| aocr_or_orden | subtotal/admin/lugar_emision/correo/telefono/concepto_id | (NO EXISTE) | OrdenRecaudacion.Subtotal/Admin/... | varios | CapaDatos/Entidades/OrdenRecaudacion.cs:50-80 | ERROR CR�TICO | Agregar columnas en BD o ajustar DAO |

**Evidencia SQL en DAO que usa columnas inexistentes:**
- INSERT con `observacion, subtotal, admin, lugar_emision, correo, telefono, concepto_id` (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:263-312)
- UPDATE con mismas columnas (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:399-413)
- MapearOrden lee esas columnas (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:611-620)

## Tabla: `aocr_or_orden_detalle`
| Tabla DB | Columna DB (snake_case) | Tipo DB | Propiedad C# encontrada | Tipo C# | Evidencia | Estado | Correcci�n sugerida |
|---|---|---|---|---|---|---|---|
| aocr_or_orden_detalle | id | serial | DetalleOrden.Id | int | CapaDatos/Entidades/DetalleOrden.cs:13-15 | OK | - |
| aocr_or_orden_detalle | orden_id | integer | DetalleOrden.OrdenId | int | CapaDatos/Entidades/DetalleOrden.cs:17-18 | OK | - |
| aocr_or_orden_detalle | concepto_id | integer | DetalleOrden.ConceptoId | int? | CapaDatos/Entidades/DetalleOrden.cs:20-21 | OK | - |
| aocr_or_orden_detalle | concepto_nombre | varchar(150) | DetalleOrden.ConceptoNombre | string | CapaDatos/Entidades/DetalleOrden.cs:27-29 | OK | - |
| aocr_or_orden_detalle | cantidad | integer | DetalleOrden.Cantidad | int | CapaDatos/Entidades/DetalleOrden.cs:35-36 | OK | - |
| aocr_or_orden_detalle | valor_unitario | numeric(18,2) | DetalleOrden.ValorUnitario | decimal | CapaDatos/Entidades/DetalleOrden.cs:38-39 | OK | - |
| aocr_or_orden_detalle | total_linea | numeric(18,2) | DetalleOrden.TotalLinea | decimal | CapaDatos/Entidades/DetalleOrden.cs:61-62 | OK | - |
| aocr_or_orden_detalle | concepto_codigo/descripcion/porcentaje_admin/subtotal/admin | (NO EXISTE) | DetalleOrden.ConceptoCodigo/Descripcion/PorcentajeAdmin/... | varios | CapaDatos/Entidades/DetalleOrden.cs:23-59 | ERROR CR�TICO | Agregar columnas o ajustar DAO |

**Evidencia SQL en DAO que usa columnas inexistentes:**
- INSERT incluye `concepto_codigo, descripcion, porcentaje_admin, subtotal, admin` (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:363-382)
- MapearDetalle lee esas columnas (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:650-657)

## Tabla: `aocr_tbdocumento`
| Tabla DB | Columna DB (snake_case) | Tipo DB | Propiedad C# encontrada | Tipo C# | Evidencia | Estado | Correcci�n sugerida |
|---|---|---|---|---|---|---|---|
| aocr_tbdocumento | codigo_documento | serial | **Pendiente** (falta clase Documento) | - | Pendiente: falta `CapaModelo/Documento.cs` | Riesgo | Enviar modelo/DAO |
| aocr_tbdocumento | codigo_solicitud | integer | Documento.CodigoSolicitud (supuesto) | int | Pendiente | Riesgo | Verificar modelo |
| aocr_tbdocumento | tipo_documento | varchar(50) | Documento.TipoDocumento (supuesto) | string | Pendiente | Riesgo | Verificar modelo |
| aocr_tbdocumento | nombre_archivo | varchar(255) | Documento.NombreArchivo (usado por reflexi�n) | string | CapaPresentacion/Controllers/SolicitudAOCRController.cs (ProcesarArchivos) | Riesgo | Verificar modelo/DAO |
| aocr_tbdocumento | ruta_guardada | text | Documento.RutaGuardada/RutaArchivo (usado por reflexi�n) | string | CapaPresentacion/Controllers/SolicitudAOCRController.cs (ProcesarArchivos) | Riesgo | Verificar modelo/DAO |
| aocr_tbdocumento | estado | varchar(20) | Documento.Estado (usado por reflexi�n) | string | CapaPresentacion/Controllers/SolicitudAOCRController.cs (ProcesarArchivos) | Riesgo | Verificar modelo/DAO |

## Tabla: `email_queue`
| Tabla DB | Columna DB (snake_case) | Tipo DB | Propiedad C# encontrada | Tipo C# | Evidencia | Estado | Correcci�n sugerida |
|---|---|---|---|---|---|---|---|
| email_queue | id | serial | EmailQueueItem.Id | int | CapaDatos/Services/EmailQueueService.cs:231-236 | OK | - |
| email_queue | to_address | varchar(255) | EmailQueueItem.Para | string | CapaDatos/Services/EmailQueueService.cs:93-110 | OK | - |
| email_queue | subject | varchar(255) | EmailQueueItem.Asunto | string | CapaDatos/Services/EmailQueueService.cs:93-110 | OK | - |
| email_queue | body | text | EmailQueueItem.Cuerpo | string | CapaDatos/Services/EmailQueueService.cs:93-110 | OK | - |
| email_queue | status | varchar(20) | EmailQueueItem.Estado | string | CapaDatos/Services/EmailQueueService.cs:93-110 | OK | - |
| email_queue | solicitud_id | integer | EmailQueueItem.OrdenId | int? | CapaDatos/Services/EmailQueueService.cs:106-110, 242 | Riesgo | Renombrar a SolicitudId o cambiar columna a orden_id |
| email_queue | proximo_intento | timestamp | EmailQueueItem.ProximoIntento | DateTime? | CapaDatos/Services/EmailQueueService.cs:96, 241 | OK | - |
| email_queue | created_at | timestamp | EmailQueueItem.FechaCreacion | DateTime | CapaDatos/Services/EmailQueueService.cs:96, 240 | OK | - |

---

# 3) Auditor�a Dapper/PostgreSQL (cr�tico)

- **Dapper MatchNamesWithUnderscores** est� activo, por lo que `snake_case` deber�a mapear a `PascalCase`. (CapaPresentacion/Global.asax.cs:22-23)
- **Errores cr�ticos de nombres**: los DAOs usan columnas/tablas que **no existen** en el script SQL entregado:
  - `OrdenRecaudacionDAO` usa columnas inexistentes en `aocr_or_orden` (observacion, subtotal, admin, lugar_emision, correo, telefono, concepto_id). (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:263-312, 399-413)
  - `OrdenRecaudacionDAO` usa columnas inexistentes en `aocr_or_orden_detalle` (concepto_codigo, descripcion, porcentaje_admin, subtotal, admin). (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:363-382)
  - `SolicitudDAO`/`SolicitudAOCRDAO` seleccionan e insertan columnas inexistentes en `aocr_tbsolicitud`. (CapaDatos/DAOs/SolicitudDAO.cs:23-31; CapaDatos/DAOs/SolicitudAOCRDAO.cs:100-117)
  - `PagoDAO` usa tablas **no listadas** en el script SQL: `pagos`, `ordenes_recaudacion`. (CapaDatos/DAOs/PagoDAO.cs:25-33, 106-109)

---

# 4) Consistencia de Tipos (cr�tico)

| Columna DB | Tipo DB | Propiedad C# | Tipo C# | Evidencia | Estado | Riesgo |
|---|---|---|---|---|---|---|
| aocr_or_orden.codigo_usuario | integer | OrdenRecaudacion.CodigoUsuario | string | CapaDatos/Entidades/OrdenRecaudacion.cs:27-29 | Riesgo | Conversi�n manual, puede guardar NULL |
| aocr_or_orden.codigo_solicitud | integer | OrdenRecaudacion.CodigoSolicitud | string | CapaDatos/Entidades/OrdenRecaudacion.cs:31-33 | Riesgo | Conversi�n manual, posible p�rdida |
| aocr_or_orden.total | numeric(18,2) | OrdenRecaudacion.Total | decimal? | CapaDatos/Entidades/OrdenRecaudacion.cs:56-57 | OK | - |
| aocr_or_orden_detalle.cantidad | integer | DetalleOrden.Cantidad | int | CapaDatos/Entidades/DetalleOrden.cs:35-36 | OK | - |
| aocr_or_orden_detalle.valor_unitario | numeric(18,2) | DetalleOrden.ValorUnitario | decimal | CapaDatos/Entidades/DetalleOrden.cs:38-39 | OK | - |
| aocr_tbsolicitud.codigo_usuario | integer | SolicitudAOCR.CodigoUsuario | int | CapaModelo/SolicitudAOCR.cs:32 | OK | - |

---

# 5) V�nculo AJAX ? Controller ? ViewModel (cr�tico)

## 5.1 Formulario AOCR (JSON)
**Vista:** `CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml`
- Envia JSON con `contentType: application/json` y URL `FormularioCompleto`. (l�neas 1211-1216)
- Incluye `Solicitud`, `Banco`, `NumeroComprobante`, `Aeronaves`. (l�neas 1182-1202)

**Controller:** `SolicitudAOCRController.FormularioCompleto(SolicitudAOCRViewModel vm)`
- Recibe el modelo esperado sin `[FromBody]`. En MVC5 el JSON puede bindearse si est� habilitado el JSON Value Provider.

**ViewModel:** `CapaPresentacion/Models/SolicitudAOCRViewModel.cs`
- Propiedades coinciden: `Solicitud`, `Aeronaves`, `Banco`, `NumeroComprobante`. (l�neas 7-19)

**Estado:** OK **si** JSONValueProvider est� habilitado. Si fue removido, fallar� el binding.

**Pendiente:** falta el modelo `AeronaveSolicitud` para verificar el mapeo de propiedades (`Fabricante`, `Modelo`, `Matricula`, etc.).

## 5.2 Orden de Recaudaci�n (Form POST)
**Pendiente:** falta `CapaPresentacion/Views/OrdenRecaudacion/Nueva.cshtml` para validar inputs y `DetallesJson`.

---

# 6) Flujo de identidad / llaves (cr�tico)

- **aocr_tbsolicitud:** `SolicitudAOCRDAO.InsertarConReturn` usa `RETURNING codigo_solicitud`, pero incluye columnas inexistentes (`tipo_solicitud`, `ciudad`, `created_at`, etc.). La inserci�n fallar� antes de retornar el ID. (CapaDatos/DAOs/SolicitudAOCRDAO.cs:100-117)
- **aocr_or_orden:** `OrdenRecaudacionDAO.Insertar` usa `RETURNING id`, pero incluye columnas inexistentes en el esquema entregado. (CapaDatos/DAOs/OrdenRecaudacionDAO.cs:263-312)
- **email_queue:** `EmailQueueService` s� coincide con el esquema entregado y retorna `id`. (CapaDatos/Services/EmailQueueService.cs:93-110)

---

# 7) Errores de sintaxis en Vistas (JS)

## 7.1 Cierre extra en script
**Archivo:** `CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml`
- L�nea 946-948:
  ```js
  // Funci�n gen�rica para guardar formularios (demo)
  };
  ```
  Esto cierra de m�s el bloque de `$(document).ready`, puede generar `Unexpected token '}'`.

## 7.2 Operador de propagaci�n nula en Razor (C# 5)
**Archivo:** `CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml` l�neas 1177-1178:
```csharp
} else if (@(Model?.Solicitud?.CodigoSolicitud ?? 0) > 0) {
    codigoSolicitud = @(Model?.Solicitud?.CodigoSolicitud ?? 0);
}
```
En C# 5 esto genera `CS8026`.

---

# 8) Lista de fixes m�nimos (ordenados)

## P0 (rompe integraci�n)
1) `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` � Ajustar SQL de `aocr_or_orden` a columnas reales (id, codigo_usuario, codigo_solicitud, numero_orden, fecha_creacion, estado, compania, ruc_cedula, total).
   - Verificaci�n: crear orden y confirmar que INSERT/SELECT no arroja �column does not exist�.
2) `CapaDatos/DAOs/OrdenRecaudacionDAO.cs` � Ajustar SQL de `aocr_or_orden_detalle` al esquema real (id, orden_id, concepto_id, concepto_nombre, cantidad, valor_unitario, total_linea).
   - Verificaci�n: insertar detalle y leerlo sin excepci�n.
3) `CapaDatos/DAOs/SolicitudDAO.cs` y `CapaDatos/DAOs/SolicitudAOCRDAO.cs` � Eliminar columnas inexistentes de SELECT/INSERT/UPDATE o migrar BD para incluirlas.
   - Verificaci�n: listar/crear solicitud sin error de columna.
4) `CapaDatos/DAOs/PagoDAO.cs` � Cambiar tablas `pagos`/`ordenes_recaudacion` a las reales (probablemente `aocr_tbpago`/`aocr_or_orden`).
   - Verificaci�n: registrar pago y listar pendientes.

## P1 (alto riesgo)
5) `CapaDatos/Entidades/OrdenRecaudacion.cs` � Cambiar `CodigoUsuario` y `CodigoSolicitud` a `int?` (o normalizar en DAO). Evitar guardar NULL silencioso. (l�neas 27-33)
   - Verificaci�n: insertar orden con usuario v�lido y revisar que se guarda el ID.
6) `CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml` � Quitar operador `?.` y reescribir con ternarios C#5.
   - Verificaci�n: compilar sin `CS8026`.

## P2 (estabilidad/JS)
7) `CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml` � Eliminar el cierre extra `};` (l�neas 946-948).
   - Verificaci�n: no aparece `Unexpected token '}'` y JS ejecuta.

---

# Pendientes expl�citos (archivos faltantes)
- **Pendiente:** `CapaModelo/Documento.cs` y `CapaDatos/DAOs/DocumentoDAO.cs` para validar `aocr_tbdocumento`.
- **Pendiente:** modelo `AeronaveSolicitud` y su mapeo real (tabla `aocr_tbaeronave_solicitud`).
- **Pendiente:** vista `CapaPresentacion/Views/OrdenRecaudacion/Nueva.cshtml` para validar binding y JS de detalles.
- **Pendiente:** script SQL real de `aocr_tbpago` y `aocr_or_concepto` (no incluidos en el SQL entregado).
