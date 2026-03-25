# Fase 2 - Tablero de Ejecucion (Implementacion Incremental y Reversible)

Fecha: 2026-03-23
Objetivo: Ejecutar la consolidacion tecnica del workflow AOCR (solicitud + inspeccion + hallazgos/NC) sin ruptura de modulos legacy.

## Actualizacion 2026-03-24 - Modulo de Inspeccion

Se agrego un entregable especifico para reestructuracion BPMN del modulo de inspeccion:

- BPMN_INSPECCION_OPTIMIZADO.md

Resumen ejecutivo:

- Se definio un flujo BPMN por swimlanes: Sistema AOCR, Inspector, Coordinador, RT y DIRDAC.
- Se aislo un ciclo unico de NC: generar, validar, subsanar y revalidar.
- Se definio una capa de estados core de negocio compatible con los estados persistidos actuales.
- Se identificaron gateways documentales, de aprobacion y de habilitacion OR.
- Se dejo un flujo tecnico de implementacion incremental sobre InspeccionController, InspeccionService, ValidacionDocumentalService y AuditoriaService.

Backlog incremental recomendado para inspeccion:

- P0: Estado core + mapper de compatibilidad.
- P0: Unificacion de transiciones duplicadas entre controller y service.
- P1: Ciclo NC unico y gates documentales centralizados.
- P1: Formalizacion de envio y devolucion DIRDAC.
- P2: Idempotencia de notificaciones y endurecimiento de auditoria.

## 1) Roadmap de Sprints

- Sprint 1 (P0): Maquina de estados canonicos de Solicitud + mapeo legacy/canonico.
- Sprint 2 (P0/P1): Trazabilidad obligatoria de transiciones + eventos de notificacion.
- Sprint 3 (P1/P2): Formalizacion NC/Hallazgo + ajustes minimos de UI por estado/rol.

## 2) Historias Tecnicas Priorizadas

## HT-01 - Servicio central de transicion de Solicitud AOCR
- Prioridad: P0
- Estimacion: M
- Dependencias: Ninguna
- Componentes impactados:
  - CapaNegocio (nuevo servicio de transicion)
  - CapaPresentacion/Controllers/SolicitudAOCRController.cs
  - CapaDatos/Constants/EstadoConstants.cs
  - CapaDatos/Constants/EstadosSolicitudAOCR.cs
- Alcance:
  - Crear servicio de negocio unico para validar/aplicar transiciones de estado de solicitud.
  - Mover validaciones de transicion dispersas en controlador hacia capa de negocio.
  - Dejar endpoints y rutas intactos.
- Criterios de aceptacion:
  - Toda transicion de solicitud se ejecuta via servicio central.
  - Transicion invalida devuelve error funcional controlado.
  - Transiciones existentes siguen funcionando con estados legacy normalizados.

Checklist de pruebas HT-01:
- [ ] Transicion valida: RECEPCIONADO -> ANALISIS_REQUISITOS.
- [ ] Transicion valida: SUBSANACION -> SUBSANADO.
- [ ] Transicion invalida: RECEPCIONADO -> APROBADO (debe fallar).
- [ ] Estado legacy "En Revision" se normaliza y transiciona correctamente.
- [ ] No cambia la firma publica de acciones del controlador.

## HT-02 - Modulo de equivalencias legacy <-> canonico
- Prioridad: P0
- Estimacion: S
- Dependencias: HT-01 (recomendado)
- Componentes impactados:
  - CapaDatos/Constants/EstadoConstants.cs
  - CapaDatos/Constants/EstadosSolicitudAOCR.cs
  - CapaNegocio (adaptador de normalizacion)
- Alcance:
  - Consolidar tabla de equivalencias en un punto unico.
  - Reutilizar en validacion de transiciones, consultas y respuestas UI.
- Criterios de aceptacion:
  - Cualquier estado legacy soportado obtiene estado canonico valido.
  - Estados desconocidos se manejan sin romper flujo (fallback controlado).

Checklist de pruebas HT-02:
- [ ] "Pendiente" -> estado canonico esperado.
- [ ] "Inspeccion Programada" -> estado canonico esperado.
- [ ] "AOCR Emitido/Recibido" conserva equivalencia.
- [ ] Estado invalido produce fallback definido y log.

## HT-03 - Trazabilidad obligatoria por transicion
- Prioridad: P0
- Estimacion: M
- Dependencias: HT-01
- Componentes impactados:
  - CapaNegocio (hook de auditoria/historial)
  - CapaDatos/DAOs/InspeccionHistorialDAO.cs
  - CapaNegocio/AuditoriaBL.cs
  - CapaDatos/DAOs/AuditoriaDAO.cs
  - CapaPresentacion/Filters/AuditActionFilter.cs
- Alcance:
  - Toda transicion critica registra historial y auditoria tecnica.
  - Propagar correlation id entre capa web y negocio.
- Criterios de aceptacion:
  - En cada transicion critica se registra estado anterior y nuevo.
  - Errores de transicion generan auditoria de fallo.

Checklist de pruebas HT-03:
- [ ] Transicion exitosa deja registro en historial.
- [ ] Transicion fallida deja registro de auditoria con error.
- [ ] Correlation id es consistente entre logs relacionados.
- [ ] Sin duplicidad de registro para una misma transicion.

## HT-04 - Estandarizacion de eventos de notificacion
- Prioridad: P1
- Estimacion: M
- Dependencias: HT-01, HT-03
- Componentes impactados:
  - CapaNegocio/Services/NotificacionService.cs
  - CapaDatos/Services/EmailQueueService.cs
- Alcance:
  - Definir catalogo minimo de eventos por transicion critica.
  - Encolar con llave de idempotencia (event_key + correlation id).
  - Mantener mecanismo actual de reintentos.
- Criterios de aceptacion:
  - Cada transicion critica encola solo un evento.
  - Si ocurre reintento tecnico, no duplica notificacion funcional.

Checklist de pruebas HT-04:
- [ ] Evento de "observada" se encola una sola vez.
- [ ] Evento de "subsanada" se encola una sola vez.
- [ ] Falla SMTP activa reintento sin duplicar funcionalmente.
- [ ] Se conserva compatibilidad de esquema email_queue.

## HT-05 - Formalizacion de NC como Hallazgo trazable
- Prioridad: P1
- Estimacion: M
- Dependencias: HT-03
- Componentes impactados:
  - CapaDatos/DAOs/HallazgoDAO.cs
  - CapaPresentacion/Controllers/InspeccionController.cs
  - CapaNegocio (reglas de cierre con hallazgos)
- Alcance:
  - Definir ciclo de vida de hallazgo (abierto/en seguimiento/cerrado).
  - Reglas de bloqueo de cierre de inspeccion segun politica acordada.
- Criterios de aceptacion:
  - Se puede crear y cerrar hallazgo con traza completa.
  - Cierre de inspeccion respeta regla de hallazgos abiertos.

Checklist de pruebas HT-05:
- [ ] Crear hallazgo durante inspeccion en curso.
- [ ] Cerrar hallazgo y verificar trazabilidad.
- [ ] Intentar cerrar inspeccion con hallazgo abierto (debe bloquear si politica lo exige).
- [ ] Reporte de hallazgos por inspeccion retorna datos consistentes.

## HT-06 - Ajustes minimos de UI por estado y rol
- Prioridad: P2
- Estimacion: S
- Dependencias: HT-01
- Componentes impactados:
  - Vistas de Solicitud e Inspeccion
  - Controladores existentes (sin cambiar rutas)
- Alcance:
  - Mostrar solo acciones permitidas por estado/rol.
  - Mejorar mensajes de observacion/subsanacion sin rediseno global.
- Criterios de aceptacion:
  - Botones invalidos no se muestran.
  - Mensajeria coherente con estado canonico.

Checklist de pruebas HT-06:
- [ ] Usuario solicitante no ve acciones de coordinador.
- [ ] Inspector ve acciones solo en estados habilitados.
- [ ] En estado observado se muestran instrucciones de subsanacion.
- [ ] No hay ruptura de navegacion ni de rutas actuales.

## 3) Plan de despliegue seguro

- Fase A: Activar HT-01 y HT-02 con feature toggle de validacion canonica.
- Fase B: Activar HT-03 (auditoria/historial estricto).
- Fase C: Activar HT-04 y HT-05.
- Fase D: Activar HT-06.

Regla de rollback funcional:
- Desactivar toggle de transicion canonica para volver al comportamiento legacy controlado.
- Mantener scripts BD idempotentes (sin borrar columnas ni constraints activas en caliente).

## 4) Criterios de salida de Fase 2

- [ ] 100% de transiciones de Solicitud pasan por validador central.
- [ ] Trazabilidad completa (historial + auditoria) en transiciones criticas.
- [ ] Notificaciones sin duplicidad funcional.
- [ ] Flujo de hallazgos/NC operativo con reglas de cierre definidas.
- [ ] Cero cambios breaking en rutas y endpoints actuales.

## 5) Estimacion global de Fase 2

- Capacidad sugerida: 2 a 3 sprints cortos.
- Esfuerzo relativo:
  - P0: 45%
  - P1: 40%
  - P2: 15%
- Riesgo residual tras Fase 2: Medio-bajo (si se respeta activacion por toggles y pruebas de regresion por estado).
