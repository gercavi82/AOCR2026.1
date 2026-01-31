# Arquitectura de Datos - AOCR

## Resumen de Bases de Datos

El sistema AOCR utiliza dos bases de datos con propósitos diferentes:

| Base de Datos | Tecnología | Propósito |
|---------------|------------|-----------|
| **PostgreSQL** | PostgreSQL 12+ | Base principal del sistema AOCR |
| **DB2/AS400** | IBM i (iSeries) | Sistema legacy de empresas/contribuyentes |

---

## PostgreSQL - Base Principal

### Ubicación
- **Servidor**: Configurado en `AOCR_CONNSTR_POSTGRESQL`
- **Acceso**: Via Npgsql (Entity Framework / ADO.NET)

### Entidades

| Tabla | Descripción | DAO |
|-------|-------------|-----|
| `ordenes_recaudacion` | Órdenes de recaudación | `OrdenRecaudacionDAO` |
| `detalles_orden` | Líneas de detalle de órdenes | `OrdenRecaudacionDAO` |
| `pagos` | Registros de pagos y comprobantes | `PagoDAO` |
| `historial_estados_orden` | Auditoría de cambios de estado | `OrdenRecaudacionDAO` |
| `conceptos` | Catálogo de conceptos de cobro | `ConceptoDAO` |
| `contribuyentes` | Copia local de contribuyentes | `ContribuyenteDAO` |
| `solicitudes` | Solicitudes de trámite | `SolicitudDAO` |
| `usuarios` | Usuarios del sistema | `UsuarioDAO` |
| `roles` | Roles y permisos | `RolDAO` |
| `archivos_subidos` | Metadatos de archivos | `ArchivoDAO` |
| `notificaciones` | Log de notificaciones enviadas | `NotificacionDAO` |

### Características
- Transacciones ACID completas
- Índices optimizados para búsquedas frecuentes
- Triggers para auditoría automática
- Pooling de conexiones habilitado

---

## DB2/AS400 - Sistema Legacy

### Ubicación
- **Servidor**: Configurado en `AOCR_AS400_SERVER`
- **Acceso**: Via ODBC (IBM i Access ODBC Driver)

### Entidades (Solo Lectura)

| Tabla/Vista | Descripción | DAO |
|-------------|-------------|-----|
| `EMPRESAS` | Catálogo maestro de empresas | `EmpresaAS400DAO` |
| `CLIENTES` | Datos de clientes/contribuyentes | `EmpresaAS400DAO` |
| `FACTURAS` | Facturas históricas | `FacturaAS400DAO` (futuro) |

### Características
- **Solo lectura** desde AOCR (no se modifican datos)
- Consultas optimizadas con campos específicos
- Timeout extendido (60 segundos)
- Sincronización periódica a PostgreSQL para consultas frecuentes

---

## Flujo de Datos

```
┌─────────────────────────────────────────────────────────────┐
│                        AOCR Web App                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Capa de Negocio                          │
│              (OrdenRecaudacionOrchestrator)                  │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌─────────────────────────┐     ┌─────────────────────────┐
│    CapaDatos (PG)       │     │   CapaDatos (AS400)     │
│  ─────────────────────  │     │  ─────────────────────  │
│  OrdenRecaudacionDAO    │     │  EmpresaAS400DAO        │
│  PagoDAO                │     │  (Solo lectura)         │
│  ConceptoDAO            │     │                         │
│  ContribuyenteDAO       │     │                         │
└─────────────────────────┘     └─────────────────────────┘
              │                               │
              ▼                               ▼
┌─────────────────────────┐     ┌─────────────────────────┐
│      PostgreSQL         │     │       DB2/AS400         │
│   (Transaccional)       │     │   (Solo consulta)       │
└─────────────────────────┘     └─────────────────────────┘
```

---

## Sincronización de Datos

### Contribuyentes (AS400 → PostgreSQL)

Para evitar consultas frecuentes al AS400, los datos de contribuyentes se sincronizan:

1. **Trigger**: Al crear una orden, si el contribuyente no existe localmente, se consulta AS400
2. **Batch nocturno**: Sincronización completa diaria (opcional)
3. **Cache local**: Datos básicos en PostgreSQL para consultas rápidas

```sql
-- Tabla local de contribuyentes (PostgreSQL)
CREATE TABLE contribuyentes (
    id SERIAL PRIMARY KEY,
    ruc_cedula VARCHAR(20) UNIQUE NOT NULL,
    nombre VARCHAR(200) NOT NULL,
    direccion TEXT,
    telefono VARCHAR(50),
    correo VARCHAR(100),
    -- Metadata de sincronización
    origen VARCHAR(20) DEFAULT 'AS400',
    fecha_sync TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    activo BOOLEAN DEFAULT true
);
```

---

## Manejo de Transacciones

### Operaciones Multi-Tabla (PostgreSQL)

```csharp
// Usar UnitOfWork para operaciones que afectan múltiples tablas
using (var uow = _unitOfWorkFactory.Create())
{
    uow.Begin();
    try
    {
        // 1. Crear orden
        var ordenId = _ordenDAO.Crear(orden, uow.Connection, uow.Transaction);
        
        // 2. Crear detalles
        foreach (var detalle in detalles)
        {
            _ordenDAO.CrearDetalle(detalle, uow.Connection, uow.Transaction);
        }
        
        // 3. Registrar historial
        _ordenDAO.RegistrarHistorial(ordenId, estado, uow.Connection, uow.Transaction);
        
        uow.Commit();
    }
    catch
    {
        uow.Rollback();
        throw;
    }
}
```

### Consultas AS400 (Solo Lectura)

```csharp
// Las consultas AS400 no requieren transacciones
// Usar conexión con timeout apropiado
public Empresa ObtenerEmpresa(string ruc)
{
    return ExecuteWithConnection(conn =>
    {
        // Consulta simple, sin transacción
        using (var cmd = CreateCommand(conn, "SELECT * FROM EMPRESAS WHERE RUC = ?"))
        {
            AddParameter(cmd, ruc, OdbcType.VarChar);
            // ...
        }
    });
}
```

---

## Consideraciones de Rendimiento

### PostgreSQL
- Pooling: `Min Pool Size=5; Max Pool Size=100`
- Timeout de conexión: 15 segundos
- Timeout de comando: 30 segundos

### AS400
- Sin pooling (conexiones bajo demanda)
- Timeout de conexión: 30 segundos
- Timeout de comando: 60 segundos
- Limitar campos en SELECT (evitar SELECT *)

---

## Variables de Entorno

```bash
# PostgreSQL
AOCR_CONNSTR_POSTGRESQL=Host=servidor;Database=aocr;Username=app;Password=***;Pooling=true;Min Pool Size=5;Max Pool Size=100

# AS400
AOCR_AS400_SERVER=as400.empresa.local
AOCR_AS400_DATABASE=LIBPROD
AOCR_AS400_USERID=AOCRUSER
AOCR_AS400_PASSWORD=***
AOCR_AS400_LIBRARY=DATAPROD
```
