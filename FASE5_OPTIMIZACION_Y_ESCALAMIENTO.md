# Fase 5 - Optimizacion y Escalamiento

Fecha: 2026-03-23
Objetivo: llevar el flujo AOCR a un nivel de operacion madura con alto rendimiento, experiencia de usuario optimizada y aseguramiento continuo de calidad.

## 1) Objetivo de Fase 5

- Optimizar tiempos de respuesta y estabilidad bajo carga.
- Mejorar experiencia operativa de roles clave sin romper legado.
- Automatizar validacion funcional end-to-end para despliegues frecuentes.

## 2) Alcance

Incluye:
- Performance tuning de consultas, endpoints y procesos asincronos.
- Mejoras UX orientadas a productividad de Solicitante, Inspector y Coordinador.
- Automatizacion de pruebas E2E + regresion por state machine.
- Estandarizacion de release pipeline con quality gates.

No incluye:
- Reescritura total de frontend.
- Migracion de plataforma fuera de stack actual.
- Cambios disruptivos de modelo de datos.

## 3) Lineas de trabajo

Linea A - Performance y escalabilidad
Linea B - UX y productividad operativa
Linea C - Calidad automatizada (E2E + regresion)
Linea D - DevOps y gobierno de releases

## 4) Backlog priorizado (P0/P1/P2)

## F5-01 - Optimizar consultas y rutas criticas
- Prioridad: P0
- Estimacion: M
- Alcance:
  - Identificar top 10 consultas mas lentas en flujo AOCR.
  - Ajustar indices faltantes y consultas con full scan innecesario.
  - Reducir roundtrips entre capa negocio y datos.
- Criterios de aceptacion:
  - Reduccion >= 30% del P95 en endpoints criticos.
  - Reduccion de timeouts en operaciones de listado/seguimiento.

Checklist de pruebas F5-01:
- [ ] Benchmark antes/despues de endpoints criticos.
- [ ] Prueba de carga basica con concurrencia objetivo.
- [ ] Verificacion de planes de ejecucion en consultas ajustadas.

## F5-02 - Endurecer cola de notificaciones bajo picos
- Prioridad: P0
- Estimacion: M
- Alcance:
  - Ajustar tamano de lote y politica de reintentos.
  - Mejorar observabilidad de backlog y fallos recurrentes.
- Criterios de aceptacion:
  - Procesamiento estable sin crecimiento sostenido de backlog.
  - Cero duplicidad funcional por evento.

Checklist de pruebas F5-02:
- [ ] Prueba de pico de eventos x3.
- [ ] Validacion de retry sin duplicidad funcional.
- [ ] Verificacion de alertas por backlog y error rate.

## F5-03 - UX de estados y acciones guiadas por rol
- Prioridad: P1
- Estimacion: M
- Alcance:
  - Simplificar pantallas de seguimiento de solicitud/inspeccion.
  - Mostrar siguiente accion recomendada por estado.
  - Mejorar feedback de observacion/subsanacion.
- Criterios de aceptacion:
  - Reduccion de errores de operacion por accion invalida.
  - Menor tiempo promedio para completar tareas frecuentes.

Checklist de pruebas F5-03:
- [ ] Test de usabilidad con casos por rol.
- [ ] Validacion de visibilidad de acciones por estado.
- [ ] Regresion de navegacion sin romper rutas actuales.

## F5-04 - Suite E2E del flujo AOCR completo
- Prioridad: P0
- Estimacion: L
- Alcance:
  - Automatizar escenarios punta a punta desde recepcion hasta cierre.
  - Cubrir variantes: aprobacion, observacion/subsanacion, rechazo.
- Criterios de aceptacion:
  - Cobertura E2E de escenarios criticos >= 85% del flujo principal.
  - Ejecucion automatica en pipeline por cada release candidato.

Checklist de pruebas F5-04:
- [ ] E2E feliz: RECEPCIONADO -> AOCR_ENTREGADO.
- [ ] E2E con observacion y subsanacion multiple.
- [ ] E2E de rechazo en etapa de aprobacion.
- [ ] Validacion de trazabilidad (historial/auditoria/notificacion) en E2E.

## F5-05 - Pruebas de regresion de state machine
- Prioridad: P0
- Estimacion: M
- Alcance:
  - Construir matriz automatizada de transiciones validas/invalidas.
  - Incluir mapping legacy/canonico en pruebas.
- Criterios de aceptacion:
  - 100% de transiciones declaradas cubiertas por tests automatizados.
  - Bloqueo de merge si falla una transicion critica.

Checklist de pruebas F5-05:
- [ ] Transiciones validas por estado.
- [ ] Transiciones invalidas con error esperado.
- [ ] Estados legacy normalizados correctamente.

## F5-06 - Quality gates y gobierno de release
- Prioridad: P1
- Estimacion: S
- Alcance:
  - Definir gates minimos: unit, integracion, E2E, seguridad basica.
  - Reforzar checklist de despliegue y rollback por ambiente.
- Criterios de aceptacion:
  - Ningun release pasa sin gates en verde.
  - Evidencia automatica de cumplimiento por version.

Checklist de pruebas F5-06:
- [ ] Pipeline falla ante regresion critica.
- [ ] Pipeline bloquea despliegue si no hay evidencia de pruebas.
- [ ] Registro de aprobaciones y resultados por release.

## 5) Metas cuantitativas

- P95 endpoints criticos: mejora >= 30%.
- Incidentes P1 relacionados a AOCR: 0 por ciclo mensual.
- MTTR P2 AOCR: <= 8 horas.
- Fallos E2E en rama principal: <= 2% por mes.
- Duplicidad funcional de correos: 0.

## 6) Plan 30/60/90 dias

Dia 0-30:
- Implementar F5-01 y F5-02.
- Baseline de performance y capacidad.

Dia 31-60:
- Implementar F5-04 y F5-05.
- Habilitar quality gates de forma obligatoria.

Dia 61-90:
- Implementar F5-03 y F5-06.
- Cerrar deuda UX y gobernanza de release.

## 7) Riesgos y mitigacion

Riesgo 1: optimizaciones rompen compatibilidad legacy.
- Mitigacion: feature toggles, despliegue gradual y regresion automatizada.

Riesgo 2: flakiness en pruebas E2E.
- Mitigacion: datos de prueba aislados, retries controlados y limpieza por suite.

Riesgo 3: sobrecarga operativa por exceso de alertas.
- Mitigacion: umbrales por severidad y tuning de alertamiento.

Riesgo 4: deuda tecnica no priorizada compite con entregas.
- Mitigacion: reservar capacidad fija por sprint para deuda critica.

## 8) Criterios de cierre de Fase 5

- [ ] Metas de performance cumplidas por 2 ciclos consecutivos.
- [ ] Suite E2E estable integrada al pipeline principal.
- [ ] Regresion de state machine 100% automatizada.
- [ ] Quality gates aplicados en todos los releases.
- [ ] Evidencia de mejora UX en tareas operativas clave.

## 9) Entregables de Fase 5

- Informe de performance antes/despues.
- Suite E2E documentada y ejecutable en pipeline.
- Matriz automatizada de transiciones de estado.
- Guia de quality gates y proceso de release.
- Reporte de mejoras UX con indicadores operativos.
