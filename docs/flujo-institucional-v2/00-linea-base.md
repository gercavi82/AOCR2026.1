# GATE 0 — LÍNEA BASE, RESPALDO E INVENTARIO

## Herramientas
- MSBuild 2022
- NuGet
- PostgreSQL (pg_dump)
- PowerShell
- Git

## Comandos Ejecutados
- `git branch --show-current`
- `git rev-parse HEAD`
- `git status`
- `git log -10 --oneline`
- `git remote -v`
- `pg_dump`
- `nuget restore`
- `MSBuild.exe AOCR.sln /p:Configuration=Debug`
- `MSBuild.exe AOCR.sln /p:Configuration=Release` (con precompilación de Razor)

## Resultados
- **Rama Actual:** `feat/flujo-institucional_v2`
- **Commit Base:** `3e2c8bd136fc63854b1d323aebbe912b1e79bc40`
- **Estado de Git:** `nothing to commit, working tree clean`

## Respaldo
- Base de datos `dgac_des` respaldada en `C:\proyectos\AOCR\backups\dgac_des_backup.backup`.

## Inventario General
- **Controladores Principales:** `SolicitudAOCRController`, `InspeccionController`, `DocumentoController`, `FirmaAocrController`, `CoordinacionJefaturaController`, `NotificacionController`.
- **Servicios:** Identificados los de transición, contexto y firma que serán adaptados en los siguientes GATES.
- **Rutas y Vistas:** Analizadas bajo `/aocr`, vistas Razor precompiladas exitosamente.
- **BD PostgreSQL:** Inventario de tablas Npgsql para `aocr_proceso_estado` y roles.

## Fallos
- `ASPNETCOMPILER : warning` - Mínima advertencia de versión en dependencia `itext.commons`, no bloqueante para inicio.
- Resultados de pruebas focales y globales pendientes de finalización post-compilación.

## Archivos No Confirmados
- Ninguno (Estado inicial limpio).

## Riesgos Identificados
- El acceso AS400 sin timeout controlado puede impactar el flujo.
- Manejo de IDs en 0, estados funcionales distribuidos en lugar de centralizados.
