# GATE F - Subsanación documental de NC (SIN_INSPECCION)

Se implementa un único PDF general de subsanación vinculado a la versión vigente de `NoConformidad`.

Flujo: `FIRMADA_COORDINADOR/EN_SUBSANACION -> SUBSANADA_RT -> CERRADA`. Si el inspector devuelve el documento, la versión revisada queda en `SUBSANACION_DEVUELTA` y se crea una versión N+1 en `EN_SUBSANACION`.

El RT solo puede operar solicitudes propias. El PDF se limita a 10 MB, se valida por extensión y cabecera `%PDF-`, y se almacena en `App_Data`. Las descargas pasan por acciones autorizadas que validan propiedad o asignación de inspector.

Aplicar `scripts/sql/011_no_conformidades.sql` antes del despliegue. El rollback está en `scripts/sql/011_gate_f_subsanacion_rollback.sql` y elimina los metadatos de subsanación.
