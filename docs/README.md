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
