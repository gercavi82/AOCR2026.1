# Variables de Entorno por Ambiente - AOCR

## Resumen

| Variable | DEV | QA | PROD |
|----------|-----|----|----- |
| `AOCR_CONNSTR_POSTGRESQL` | localhost | qa-db.interno | prod-db.interno |
| `AOCR_AS400_SERVER` | as400-dev | as400-qa | as400.prod |
| `AOCR_EMAIL_SERVER` | smtp-dev | smtp-qa | smtp.prod |

---

## Desarrollo (DEV)

```bash
# Base de datos
AOCR_CONNSTR_POSTGRESQL=Host=localhost;Database=aocr_dev;Username=aocr_dev;Password=dev123;Pooling=true

# AS400 (opcional en dev)
AOCR_AS400_SERVER=as400-dev.interno
AOCR_AS400_DATABASE=LIBDEV
AOCR_AS400_USERID=AOCRDEV
AOCR_AS400_PASSWORD=devpass123

# Email (usar MailHog o similar)
AOCR_EMAIL_SERVER=localhost
AOCR_EMAIL_PORT=1025
AOCR_EMAIL_USESSL=false
AOCR_EMAIL_FROM=dev@localhost
```

---

## QA

```bash
# Base de datos
AOCR_CONNSTR_POSTGRESQL=Host=qa-db.interno;Database=aocr_qa;Username=aocr_qa;Password=******;Pooling=true;Min Pool Size=2;Max Pool Size=50

# AS400
AOCR_AS400_SERVER=as400-qa.interno
AOCR_AS400_DATABASE=LIBQA
AOCR_AS400_USERID=AOCRQA
AOCR_AS400_PASSWORD=******

# Email (servidor de pruebas)
AOCR_EMAIL_SERVER=smtp-qa.interno
AOCR_EMAIL_PORT=587
AOCR_EMAIL_USERNAME=aocr-qa@aviacioncivil.gob.ec
AOCR_EMAIL_PASSWORD=******
AOCR_EMAIL_USESSL=true
AOCR_EMAIL_FROM=aocr-qa@aviacioncivil.gob.ec
AOCR_EMAIL_FROMNAME=AOCR QA
```

---

## Producción (PROD)

```bash
# Base de datos
AOCR_CONNSTR_POSTGRESQL=Host=prod-db.interno;Database=aocr;Username=aocr_app;Password=******;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Idle Lifetime=300

# AS400
AOCR_AS400_SERVER=as400.aviacioncivil.gob.ec
AOCR_AS400_DATABASE=LIBPROD
AOCR_AS400_USERID=AOCRPROD
AOCR_AS400_PASSWORD=******
AOCR_AS400_LIBRARY=DATAPROD

# Email
AOCR_EMAIL_SERVER=smtp.aviacioncivil.gob.ec
AOCR_EMAIL_PORT=587
AOCR_EMAIL_USERNAME=no_reply@aviacioncivil.gob.ec
AOCR_EMAIL_PASSWORD=******
AOCR_EMAIL_USESSL=true
AOCR_EMAIL_FROM=no_reply@aviacioncivil.gob.ec
AOCR_EMAIL_FROMNAME=Sistema AOCR
```

---

## Configurar en Windows

### Por usuario (desarrollo)
```powershell
[Environment]::SetEnvironmentVariable("AOCR_CONNSTR_POSTGRESQL", "...", "User")
```

### Por máquina (servidores)
```powershell
[Environment]::SetEnvironmentVariable("AOCR_CONNSTR_POSTGRESQL", "...", "Machine")
```

### En IIS (Application Pool)
1. Abrir IIS Manager
2. Application Pools → AOCR → Advanced Settings
3. Environment Variables → Agregar cada variable

---

## Validar Configuración

```powershell
# Verificar que las variables están configuradas
Get-ChildItem Env:AOCR_*

# Test de conexión PostgreSQL
$env:AOCR_CONNSTR_POSTGRESQL
psql "$env:AOCR_CONNSTR_POSTGRESQL" -c "SELECT 1"
```
