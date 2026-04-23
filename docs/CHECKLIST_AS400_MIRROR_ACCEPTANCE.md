# Checklist de Aceptacion AS400 -> PostgreSQL Mirror

Fecha: 2026-04-23
Objetivo: Validar que AOCR puede operar PostgreSQL-first sin dependencia runtime de AS400 para tablas espejo criticas.

## 1) Pre-condiciones de ejecucion

- [ ] Scripts aplicados en orden: 001 -> 002 -> 003 -> 003b -> 003c.
- [ ] Al menos un ciclo de sync ejecutado para todas las tablas habilitadas.
- [ ] Conexion a PostgreSQL del ambiente objetivo disponible.
- [ ] Sin errores bloqueantes en `sync.batch_log` para la ultima corrida.

## 2) Cobertura de tablas AS400 consumidas por AOCR

Tablas que deben existir y tener datos en `mirror_raw`:

- [ ] `usuarc` (usuarios)
- [ ] `usuar1` (usuario adicional)
- [ ] `ciaarc` (companias)
- [ ] `opuarc01` (ubicacion por ciudad)
- [ ] `oidar2` (aeropuerto por ciudad)
- [ ] `opiar2` (inspectores)
- [ ] `txdgac` (listas de valores)
- [ ] `opsarc` (secuenciales FR3)
- [ ] `opcar5` (FR3 cabecera)
- [ ] `opcar6` (FR3 detalle)

Criterio de aceptacion:

- [ ] 10/10 tablas presentes.
- [ ] `COUNT(*) > 0` en tablas catalogo y operativas esperadas.

## 3) Integridad estructural (PK e indices minimos)

- [ ] PK `usuarc(usucod)`
- [ ] PK `usuar1(usuco8)`
- [ ] PK `ciaarc(ciacod)`
- [ ] PK `opuarc01(opucod)`
- [ ] PK `oidar2(oidco3,oidoi2)`
- [ ] PK `opiar2(opiced,opitip)`
- [ ] PK `txdgac(valdds,valval)`
- [ ] PK `opsarc(opsaer,opsano)`
- [ ] PK `opcar5(opcsec,opcaer,opcano)`
- [ ] PK `opcar6(opcse2,opcae1,opcan1,opcse1)`

Criterio de aceptacion:

- [ ] Sin duplicados por PK.
- [ ] Indices funcionales para busquedas usadas por AOCR (estado/tipo, factura, ciudad, campo lista).

## 4) Watermarks, lotes y calidad de sync

- [ ] `sync.watermark` con status `OK` para: USUARC, USUAR1, CIAARC, OPUARC01, OIDAR2, OPIAR2, TXDGAC, OPSARC, OPCAR5, OPCAR6.
- [ ] Ultimos lotes de `sync.batch_log` sin `ERROR` bloqueante.
- [ ] `sync.rejections` sin crecimiento anomalo.
- [ ] `sync.tombstones` pendientes = 0 (o justificados).

Criterio de aceptacion:

- [ ] Todos los estados de watermark en `OK`.
- [ ] Rechazos controlados y con remediacion.

## 5) Coherencia de datos funcionales AOCR

- [ ] Usuarios activos con ciudad (`usuarc.usuco5`) consultables.
- [ ] Companias activas (`ciaarc.ciaest='AC'`) disponibles.
- [ ] Lugar de emision por ciudad resoluble via `opuarc01/oidar2`.
- [ ] Inspectores activos OPS/AIR (`opiar2.opies1='AC'`).
- [ ] Listas de valores P9 (`txdgac`) para `OPCBAN`, `SOLFOR` y otras claves usadas.
- [ ] FR3 cabecera/detalle consistentes (`opcar5` vs `opcar6`).
- [ ] Secuenciales FR3 trazables (`opsarc`).

## 6) Criterios de Go/No-Go

Go a produccion si:

- [ ] 100% de checks estructurales en verde.
- [ ] 100% de watermarks en `OK`.
- [ ] 0 errores criticos en ultimas corridas de sync.
- [ ] Pruebas funcionales AOCR sin fallback runtime AS400 pasan.

No-Go si:

- [ ] Falta alguna tabla espejo critica.
- [ ] Hay duplicados por PK.
- [ ] Existe drift de datos funcionales que rompe flujo de negocio.

## 7) Evidencia minima a adjuntar en acta

- [ ] Salida de `scripts/mirror_sync/005_validate_full_mirror_sync.sql`.
- [ ] Salida de `scripts/mirror_sync/006_acceptance_gate.sql`.
- [ ] Captura de ultima ejecucion de sync (resumen lotes).
- [ ] Resultado de smoke tests de AOCR en flujo principal.
