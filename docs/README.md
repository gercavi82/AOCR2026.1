# AOCR - Sistema de Órdenes de Recaudación

## Descripción
Sistema web para gestión de órdenes de recaudación de la Autoridad de Aviación Civil.

## Requisitos
- .NET Framework 4.8
- Visual Studio 2019/2022
- PostgreSQL 12+
- IIS 10+
- IBM i Access ODBC Driver (para AS400)

## Estructura del Proyecto
```
AOCR/
├── CapaPresentacion/     # MVC Web Application
├── CapaNegocio/          # Business Logic Layer
├── CapaDatos/            # Data Access Layer
├── AOCR.Tests/           # Unit Tests
├── docs/                 # Documentación
└── scripts/              # Scripts de BD y deployment
```

## Configuración Rápida

### 1. Clonar y restaurar
```bash
git clone [repo-url]
cd AOCR
nuget restore AOCR.sln
```

### 2. Configurar variables de entorno (desarrollo)
```bash
# PowerShell
$env:AOCR_CONNSTR_POSTGRESQL = "Host=localhost;Database=aocr;Username=dev;Password=xxx"
```

### 3. Ejecutar migraciones de BD
```bash
psql -h localhost -U dev -d aocr -f scripts/sql/schema.sql
psql -h localhost -U dev -d aocr -f scripts/sql/audit_tables.sql
psql -h localhost -U dev -d aocr -f scripts/sql/email_pdf_tables.sql
```

### 4. Compilar y ejecutar
```bash
msbuild AOCR.sln /p:Configuration=Debug
# O abrir en Visual Studio y F5
```

## Documentación Adicional

### Manuales principales
- [**Hoja de ruta publicación**](HOJA_RUTA_PUBLICACION.md) — gates A–E + go-live
- [**Seguimiento Excel/CSV**](export/editable/SEGUIMIENTO_PUBLICACION_AOCR.csv) — plantilla semanal (Responsable / Fecha / Estado)
- [Manual de usuario](MANUAL_USUARIO_AOCR.md) — flujos por rol, pantallas, checklist operativo
- [**Flujo completo RT → AOCR**](MANUAL_FLUJO_RT_A_AOCR.md) — 16 fases, todos los roles hasta emisión
- [**Guía visual flujo RT → AOCR**](GUIA_VISUAL_FLUJO_RT_AOCR.md) — 42 capturas, textos UI por fase
- [**Checklist documentación 100%**](CHECKLIST_DOCUMENTACION_100.md) — artefactos, PNG, E2E, export
- [Manual técnico](MANUAL_TECNICO_AOCR.md) — arquitectura, §16 LV, §17 Informe, §18 Mod. tipo 3
- [Guía visual por rol](GUIA_VISUAL_POR_ROL.md) — matrices por rol ampliadas
- [Capturas PNG](images/README.md) — convención y tabla de 42 archivos
- [Exportar PDF / Word](export/README.md) — `npm run build` en `export/`

### Matrices y guías de validación
- [Flujo integral y matrices](AOCR_FLUJO_INTEGRAL_MATRICES.md)
- [Guía pruebas post-republicación](GUIA_PRUEBAS_POST_REPUBLICACION.md)
- [Guía inspector solicitud #12](GUIA_INSPECTOR_SOLICITUD_12.md)
- [Plan de cierre por rol](PLAN_CIERRE_POR_ROL.md)

### Operación e infraestructura
- [Checklist de Producción](CHECKLIST_PRODUCCION.md)
- [Checklist de Despliegue IIS](CHECKLIST_IIS.md)
- [Arquitectura de Datos](ARQUITECTURA_DATOS.md)
- [Checklist de Seguridad](CHECKLIST_SEGURIDAD.md)

## Health Checks
- `/health` - Estado básico
- `/health/details` - Estado detallado con dependencias
- `/health/live` - Liveness probe
- `/health/ready` - Readiness probe

## Tests
```bash
vstest.console AOCR.Tests\bin\Release\AOCR.Tests.dll
```

## Contacto
- Equipo de Desarrollo: desarrollo@aviacioncivil.gob.ec
