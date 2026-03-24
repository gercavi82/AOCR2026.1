# Fase 3 - Implementacion Controlada, RACI y Salida a Produccion

Fecha: 2026-03-23
Objetivo: ejecutar la implementacion incremental del workflow AOCR con control operativo, trazabilidad completa y minimo riesgo de ruptura.

## 1) Objetivo de Fase 3

- Implementar en codigo los cambios definidos en Fase 2 por lotes pequenos.
- Operar bajo feature toggles para habilitacion progresiva.
- Cerrar brechas de gobernanza: actor responsable por transicion, auditoria obligatoria, evidencia de pruebas.

## 2) Alcance exacto

Incluye:
- Activacion tecnica de HT-01 a HT-06 en orden controlado.
- Matriz RACI por transicion critica.
- Plan de pruebas de regresion por estado/rol.
- Plan de despliegue por ambiente (DEV, QA, PROD).

No incluye:
- Rediseno UI completo.
- Refactor masivo cross-cutting.
- Cambios destructivos de base de datos.

## 3) Secuencia de implementacion (lotes)

Lote 3.1 - Motor de transicion y equivalencias
- Historias: HT-01, HT-02
- Resultado esperado: Solicitud AOCR con transicion canonica centralizada y compatibilidad legacy.

Lote 3.2 - Trazabilidad dura
- Historias: HT-03
- Resultado esperado: cada transicion critica deja historial + auditoria correlacionable.

Lote 3.3 - Notificaciones y NC/Hallazgos
- Historias: HT-04, HT-05
- Resultado esperado: eventos sin duplicidad + ciclo de vida de hallazgo operativo.

Lote 3.4 - Ajustes minimos de UI
- Historias: HT-06
- Resultado esperado: acciones por estado/rol sin ruptura de rutas existentes.

## 4) Matriz RACI por transicion critica

Roles:
- Solicitante
- Inspector
- Coordinador
- Director
- Sistema (automatica)

RACI:

1. RECEPCIONADO -> ANALISIS_REQUISITOS
- R: Sistema
- A: Coordinador
- C: Solicitante
- I: Inspector

2. ANALISIS_REQUISITOS -> SUBSANACION
- R: Coordinador
- A: Coordinador
- C: Inspector
- I: Solicitante

3. SUBSANACION -> SUBSANADO
- R: Solicitante
- A: Solicitante
- C: Coordinador
- I: Inspector

4. SUBSANADO -> ANALISIS_REQUISITOS
- R: Sistema
- A: Coordinador
- C: Inspector
- I: Solicitante

5. ANALISIS_REQUISITOS -> EN_EVALUACION_TECNICA
- R: Coordinador
- A: Coordinador
- C: Inspector
- I: Solicitante

6. EN_EVALUACION_TECNICA -> EN_EVALUACION_LEGAL
- R: Inspector
- A: Coordinador
- C: Legal
- I: Solicitante

7. EN_EVALUACION_LEGAL -> EN_EVALUACION_FINANCIERA
- R: Coordinador
- A: Coordinador
- C: Financiero
- I: Solicitante

8. EN_EVALUACION_FINANCIERA -> EN_APROBACION_COORDINADOR
- R: Coordinador
- A: Coordinador
- C: Inspector
- I: Solicitante

9. EN_APROBACION_COORDINADOR -> EN_APROBACION_DIRECTOR
- R: Coordinador
- A: Coordinador
- C: Director
- I: Solicitante

10. EN_APROBACION_DIRECTOR -> APROBADO
- R: Director
- A: Director
- C: Coordinador
- I: Solicitante

11. APROBADO -> AOCR_EMITIDO
- R: Sistema
- A: Coordinador
- C: Director
- I: Solicitante

12. AOCR_EMITIDO -> AOCR_ENTREGADO
- R: Coordinador
- A: Coordinador
- C: Solicitante
- I: Inspector

13. Cualquier estado -> RECHAZADO
- R: Coordinador o Director (segun nivel)
- A: Director (rechazo final), Coordinador (rechazo tecnico/documental)
- C: Inspector
- I: Solicitante

## 5) Reglas operativas obligatorias

- Ninguna transicion sin validacion del motor canonico.
- Ninguna transicion sin registro de auditoria e historial.
- Ninguna notificacion sin event_key y correlation id.
- Ningun cierre de inspeccion si hay hallazgos abiertos cuando la politica lo exija.
- Ninguna accion de UI fuera del set permitido por estado y rol.

## 6) Plan de pruebas de regresion (por lote)

Lote 3.1 (HT-01, HT-02)
- [ ] Pruebas de transicion valida e invalida.
- [ ] Pruebas de normalizacion legacy a canonico.
- [ ] Pruebas de compatibilidad de endpoints existentes.

Lote 3.2 (HT-03)
- [ ] Prueba de auditoria en exito.
- [ ] Prueba de auditoria en error.
- [ ] Prueba de historial con estado anterior/nuevo.

Lote 3.3 (HT-04, HT-05)
- [ ] Prueba de no duplicidad funcional de eventos.
- [ ] Prueba de reintento SMTP.
- [ ] Prueba de bloqueo de cierre por hallazgo abierto.

Lote 3.4 (HT-06)
- [ ] Prueba de visibilidad por rol.
- [ ] Prueba de acciones habilitadas por estado.
- [ ] Prueba de navegacion legacy sin cambios de ruta.

## 7) Calidad de datos y BD

- Aplicar scripts idempotentes primero en DEV, luego QA, luego PROD.
- Verificar constraints de estado antes de activar toggle canonico.
- No eliminar columnas legacy durante Fase 3.
- Registrar evidencias de pre y post despliegue por ambiente.

## 8) Feature toggles recomendados

- toggle.aocr.solicitud.transicionCanonica
- toggle.aocr.auditoria.transicionObligatoria
- toggle.aocr.notificaciones.eventosCanonicos
- toggle.aocr.hallazgos.bloqueoCierre
- toggle.aocr.ui.accionesPorEstadoRol

Estrategia:
- Inicialmente OFF en PROD.
- Activar por lotes en QA.
- Activar en PROD en ventana controlada y monitoreada.

## 9) Monitoreo y alertas

Indicadores minimos:
- Tasa de transiciones fallidas por estado.
- Tiempo promedio de subsanacion.
- Numero de hallazgos abiertos > umbral.
- Duplicidad de notificaciones por evento.
- Errores por constraint de estado en BD.

Alertas sugeridas:
- Incremento abrupto de rechazos post activacion.
- Errores de transicion en endpoints criticos.
- Cola de correo con reintentos por encima del umbral.

## 10) Plan de despliegue por ambiente

DEV:
- Activar todos los toggles y ejecutar pruebas tecnicas.

QA:
- Activar por lotes 3.1 -> 3.2 -> 3.3 -> 3.4.
- Cerrar acta de regresion por lote.

PROD:
- Activacion gradual:
  - Semana 1: lote 3.1
  - Semana 2: lote 3.2
  - Semana 3: lote 3.3
  - Semana 4: lote 3.4

## 11) Criterios de Go/No-Go

Go:
- 0 errores bloqueantes en regresion.
- 0 rupturas de endpoint/route legacy.
- Evidencia de auditoria/historial en transiciones criticas.
- Duplicidad de notificaciones = 0 funcional.

No-Go:
- Fallo de compatibilidad con estados legacy.
- Tasa de error de transicion sobre umbral pactado.
- Inconsistencias de estado entre UI y BD.

## 12) Plan de rollback operativo

- Desactivar toggles del lote desplegado.
- Mantener esquema BD (sin rollback destructivo).
- Reencolar notificaciones fallidas si aplica.
- Ejecutar checklist de salud post rollback:
  - [ ] Endpoints operativos
  - [ ] Creacion/consulta solicitud
  - [ ] Flujo inspeccion basico
  - [ ] Auditoria activa

## 13) Entregables de salida de Fase 3

- Documento de evidencia de pruebas por lote.
- Registro de toggles y fechas de activacion.
- Matriz de incidentes y acciones correctivas.
- Lecciones aprendidas para fase siguiente (optimizacion y deuda tecnica residual).
