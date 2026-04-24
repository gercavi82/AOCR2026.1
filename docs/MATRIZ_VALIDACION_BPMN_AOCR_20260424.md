# Matriz de validacion BPMN AOCR - 2026-04-24

## Objetivo
Ejecutar la opcion 1: validacion funcional del flujo BPMN AOCR con evidencia real (tests + datos de BD) y dejar cobertura trazable de lo validado hoy.

## Evidencia ejecutada hoy

### 1) Pruebas automaticas
- Build AOCR.Tests: OK (MSBuild VS 2022)
- Matriz dirigida de transiciones BPMN (VSTest): 29/29 OK
  - TRX: TestResults/german.cajas_DESKTOP-S4GE44J_2026-04-24_13_05_35.trx
- Integracion flujo completo (placeholder): 1 omitida
  - TRX: TestResults/german.cajas_DESKTOP-S4GE44J_2026-04-24_13_06_51.trx
- Suite completa AOCR.Tests: 56 total, 55 OK, 1 omitida
  - TRX: TestResults/german.cajas_DESKTOP-S4GE44J_2026-04-24_13_07_33.trx

### 2) Datos reales en BD (dgac_des)
- Estados actuales en aocr_tbsolicitud:
  - Pendiente: 165
  - En Inspeccion: 26
  - Observada: 6
  - AOCR Legalizado: 2

- Readiness por estado BPMN canonico:
  - Solicitud Creada: 0
  - Documentacion Pendiente: 0
  - Observada: 6
  - Subsanada: 0
  - Aceptacion Documental: 0
  - Pendiente Asignacion RT: 0
  - En Inspeccion: 26
  - AOCR En Elaboracion: 0
  - AOCR En Revision: 0
  - AOCR Validado: 0
  - AOCR Legalizado: 2
  - AOCR Emitido/Recibido: 0

- Readiness por roles target:
  - ADMINISTRADOR: 2
  - COORDINACIONLEGAL: 1
  - COORDINADORLEGAL: 1
  - DIRECCION: 1
  - JEFATURATECNICA: 1
  - DIRDAC: 0
  - DIRECTORGENERAL: 0

## Matriz de validacion (backend + ejecucion)

| ID | Etapa / Regla BPMN | Evidencia de codigo | Evidencia ejecutada hoy | Estado |
|---|---|---|---|---|
| 1 | Matriz de transiciones validas AOCR permitida | AOCR.Tests/Unit/EstadoSolicitudTransitionMatrixTests.cs:21 | 11 casos OK (vstest dirigido + suite completa) | OK |
| 2 | Matriz de transiciones invalidas AOCR bloqueada | AOCR.Tests/Unit/EstadoSolicitudTransitionMatrixTests.cs:37 | 9 casos OK (vstest dirigido + suite completa) | OK |
| 3 | Normalizacion legacy -> transicion canonica valida | AOCR.Tests/Unit/EstadoSolicitudTransitionMatrixTests.cs:44 | 1 caso OK | OK |
| 4 | Transiciones BL validas/invalidas y legacy | AOCR.Tests/Unit/SolicitudEstadoTransitionBLTests.cs:20,33,43 | 17 casos OK | OK |
| 5 | Revision documental solo en etapas habilitadas | CapaPresentacion/Controllers/SolicitudAOCRController.cs:1850,3246 | Validado por inspeccion de codigo | OK-CODE |
| 6 | Accion masiva documental con reglas duras (decision/observacion completas) | CapaPresentacion/Controllers/SolicitudAOCRController.cs:1925 | Validado por inspeccion de codigo | OK-CODE |
| 7 | Cierre de revision documental bloquea faltantes | CapaPresentacion/Controllers/SolicitudAOCRController.cs:2139 | Validado por inspeccion de codigo | OK-CODE |
| 8 | Subsanacion solo sobre docs observados/devueltos y cobertura total | CapaPresentacion/Controllers/SolicitudAOCRController.cs:2384 | Validado por inspeccion de codigo | OK-CODE |
| 9 | Solicitar inspeccion exige estado Pendiente Asignacion RT | CapaPresentacion/Controllers/SolicitudAOCRController.cs:2942 | Validado por inspeccion de codigo | OK-CODE |
| 10 | Solicitar inspeccion exige inspector asignado | CapaPresentacion/Controllers/SolicitudAOCRController.cs:2942,3271 | Validado por inspeccion de codigo | OK-CODE |
| 11 | Solicitar inspeccion exige aprobacion financiera | CapaPresentacion/Controllers/SolicitudAOCRController.cs:2942,3254 + CapaDatos/DAOs/OrdenRecaudacionDAO.cs:1424 | Validado por inspeccion de codigo | OK-CODE |
| 12 | Solicitar inspeccion exige inspeccion registrada | CapaPresentacion/Controllers/SolicitudAOCRController.cs:2942 | Validado por inspeccion de codigo | OK-CODE |
| 13 | Enviar AOCR a revision exige AOCR generado | CapaPresentacion/Controllers/SolicitudAOCRController.cs:3017 | Validado por inspeccion de codigo | OK-CODE |
| 14 | Legalizar exige AOCR generado | CapaPresentacion/Controllers/SolicitudAOCRController.cs:2888 | Validado por inspeccion de codigo | OK-CODE |
| 15 | Emitir AOCR exige inspeccion satisfactoria + AOCR generado | CapaPresentacion/Controllers/SolicitudAOCRController.cs:3042,3073 | Validado por inspeccion de codigo | OK-CODE |
| 16 | Inspeccion: bloquear resultado satisfactorio con NC abiertas | CapaNegocio/Services/InspeccionWorkflowService.cs:41,68,72 | Validado por inspeccion de codigo | OK-CODE |
| 17 | Inspeccion: bloquear cierre con NC abiertas | CapaNegocio/Services/InspeccionWorkflowService.cs:296,328,332 | Validado por inspeccion de codigo | OK-CODE |
| 18 | Generacion AOCR exige revision institucional del informe | CapaNegocio/Services/GeneracionAOCRService.cs:59,176 | Validado por inspeccion de codigo | OK-CODE |

## Cobertura real posible hoy (roles + datos)

### Casos que SI se pueden ejecutar ya en ambiente real
- Flujo en Observada -> Subsanacion -> Revision documental (hay 6 solicitudes observadas)
- Flujo En Inspeccion con reglas de cierre/resultado (hay 26 solicitudes en inspeccion)
- Flujo desde AOCR Legalizado (hay 2 solicitudes en ese estado)

### Casos bloqueados por disponibilidad actual del ambiente
- Pruebas operativas de bandeja/acciones con rol DIRDAC (0 usuarios)
- Pruebas operativas de bandeja/acciones con rol DIRECTORGENERAL (0 usuarios)
- Tramos con estados sin data activa hoy: Solicitud Creada, Documentacion Pendiente, Subsanada, Aceptacion Documental, Pendiente Asignacion RT, AOCR En Elaboracion, AOCR En Revision, AOCR Validado, AOCR Emitido/Recibido

## Conclusiones de esta corrida
- La matriz automatica de transiciones AOCR esta estable (29/29 dirigida, 55/56 suite completa, 1 omitida de integracion placeholder).
- Los gates backend criticos del flujo BPMN AOCR/Inspecciones estan presentes y trazables en codigo.
- La ejecucion funcional completa por roles reales no es 100% posible hoy por falta de usuarios DIRDAC/DIRECTORGENERAL y por ausencia de datos en varios estados intermedios.

## Siguiente paso recomendado para cerrar 100% opcion 1
1. Cargar data semilla para estados faltantes del BPMN (al menos 1 solicitud por estado).
2. Crear/habilitar al menos 1 usuario activo para DIRDAC y 1 para DIRECTORGENERAL.
3. Ejecutar ronda manual role-by-role con evidencia (captura de bloqueo/permiso + mensaje UX) sobre la misma matriz de 18 controles.
