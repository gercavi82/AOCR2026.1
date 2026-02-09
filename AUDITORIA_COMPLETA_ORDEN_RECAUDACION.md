# Auditoría Completa - Módulo Órdenes de Recaudación

**Fecha:** 5 de febrero de 2026  
**Alcance:** 290 archivos relacionados con OrdenRecaudacion  
**Prioridad:** **CRÍTICO (P0)** - Módulo con múltiples errores bloqueantes

---

## 🔴 RESUMEN EJECUTIVO

El módulo de Órdenes de Recaudación tiene **5 problemas críticos (P0)** y **3 problemas altos (P1)** que impiden su funcionamiento correcto:

### Problemas Críticos (P0)
1. **Antipatrón Async/Sync** - Bloqueo de threads con `.Result` (riesgo de deadlock)
2. **Columnas inexistentes en INSERT** - `aocr_or_orden_detalle` (7 existentes vs 11 usadas)
3. **Columnas inexistentes en UPDATE** - `aocr_or_orden` (9 existentes vs 16 usadas)
4. **Métodos pseudo-async** - `Task.FromResult()` no proporciona beneficio real
5. **Constructores duales** - Controller con DI y sin DI causando confusión

### Problemas Altos (P1)
1. **Valores hardcodeados** - Tarifas en `AsegurarConceptosBasicos()` (ya existe solución sin aplicar)
2. **Inconsistencia de tipos** - `CodigoUsuario`/`CodigoSolicitud` son int en BD pero string en código
3. **Dependencias directas** - `new OrdenRecaudacionDAO()` en múltiples lugares

---

## 📊 ANÁLISIS POR CAPA

### 1. CAPA DE DATOS (CapaDatos/DAOs/OrdenRecaudacionDAO.cs)

**Archivo:** 2038 líneas  
**Estado:** ⚠️ CRÍTICO - SQL con columnas inexistentes

#### 🔴 P0-1: INSERT DetalleLíneas 375-378** - SQL intentando insertar 11 columnas:
```sql
INSERT INTO aocr_or_orden_detalle 
(orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion, 
 cantidad, valor_unitario, porcentaje_admin, subtotal, admin, total_linea)
```

**REALIDAD:** La tabla solo tiene 7 columnas:
- ✅ orden_id
- ✅ concepto_id
- ✅ concepto_nombre
- ✅ cantidad
- ✅ valor_unitario
- ✅ total_linea
- ❌ concepto_codigo (NO EXISTE)
- ❌ descripcion (NO EXISTE)
- ❌ porcentaje_admin (NO EXISTE)
- ❌ subtotal (NO EXISTE)
- ❌ admin (NO EXISTE)

**Error producido:** `42703: column "concepto_codigo" does not exist`

#### 🔴 P0-2: UPDATE con columnas inexistentes

**Líneas 410-423** - SQL intentando actualizar 16 campos:
```sql
UPDATE aocr_or_orden SET
    codigo_usuario = @codigoUsuario,
    codigo_solicitud = @codigoSolicitud,
    numero_orden = @numeroOrden,
    estado = @estado,
    observacion = @observacion,        -- ❌ NO EXISTE
    subtotal = @subtotal,              -- ❌ NO EXISTE
    admin = @admin,                    -- ❌ NO EXISTE
    total = @total,
    lugar_emision = @lugarEmision,     -- ❌ NO EXISTE
    compania = @compania,
    ruc_cedula = @rucCedula,
    correo = @correo,                  -- ❌ NO EXISTE
    telefono = @telefono,              -- ❌ NO EXISTE
    concepto_id = @conceptoId          -- ❌ NO EXISTE
WHERE id = @id
```

**REALIDAD:** La tabla `aocr_or_orden` solo tiene 9 columnas:
- ✅ id
- ✅ codigo_usuario
- ✅ codigo_solicitud
- ✅ numero_orden
- ✅ fecha_creacion
- ✅ estado
- ✅ compania
- ✅ ruc_cedula
- ✅ total

#### 🔴 P0-3: Métodos Pseudo-Async

**Líneas 1568-1583** - "Async" que NO es async:
```csharp
public Task<OrdenRecaudacion> ObtenerPorIdAsync(int id)
{
    return Task.FromResult(ObtenerPorId(id));  // ❌ Bloquea thread síncronamente
}

public Task<IEnumerable<OrdenRecaudacion>> ObtenerTodosAsync()
{
    return Task.FromResult<IEnumerable<OrdenRecaudacion>>(ObtenerTodas());  // ❌ Síncro
}
```

**Problema:** Estos métodos envuelven operaciones síncronas en `Task.FromResult()`. NO aprovechan async/await, BLOQUEAN el thread igual que llamadas síncronas.

**Métodos afectados:**
- `ObtenerPorIdAsync(int id)` - línea 1568
- `ObtenerTodosAsync()` - línea 1573
- `ObtenerPorEstadoAsync(string estado)` - línea 1578
- `CrearAsync(OrdenRecaudacion orden)` - línea 1779
- `ActualizarAsync(OrdenRecaudacion orden)` - línea 1808
- `ActualizarEstadoAsync(int id, string nuevoEstado, string usuario)` - línea 1813
- `EliminarAsync(int id, string usuario)` - línea 1818

---

### 2. CAPA DE NEGOCIO (CapaNegocio/OrdenRecaudacionBL.cs)

**Archivo:** 53 líneas  
**Estado:** 🔴 CRÍTICO - Antipatrón sync-over-async en TODOS los métodos

#### 🔴 P0-4: Sync-over-Async Antipattern

**TODOS los métodos públicos bloquean async con `.Result`:**

```csharp
// Línea 24 - ListarPorUsuario
public List<OrdenRecaudacion> ListarPorUsuario(string usuario)
{
    var result = _dao.ObtenerTodosAsync().Result;  // ⚠️ DEADLOCK RISK
    return new List<OrdenRecaudacion>(result);
}

// Línea 30 - ObtenerPorId
public OrdenRecaudacion ObtenerPorId(int id)
{
    return _dao.ObtenerPorIdAsync(id).Result;  // ⚠️ BLOCKS THREAD
}

// Línea 35 - Insertar
public int Insertar(OrdenRecaudacion orden)
{
    return _dao.CrearAsync(orden).Result;  // ⚠️ DEADLOCK RISK
}

// Línea 40 - Actualizar
public bool Actualizar(OrdenRecaudacion orden)
{
    return _dao.ActualizarAsync(orden).Result;  // ⚠️ BLOCKS THREAD
}

// Línea 45 - CambiarEstado
public bool CambiarEstado(int id, string nuevoEstado, string observacion = null)
{
    return _dao.ActualizarEstadoAsync(...).Result;  // ⚠️ DEADLOCK RISK
}
```

**Impacto:**
- ⚠️ **Riesgo de deadlock** en ASP.NET (especialmente con `SynchronizationContext`)
- ⚠️ **Bloqueo de threads** del thread pool
- ⚠️ **Degradación de performance** en alta concurrencia
- ⚠️ **Timeout en IIS** bajo carga

**Evidencia:** Documentado en RCA_ORDENES.md y confirmado en auditoría

---

### 3. CAPA DE PRESENTACIÓN (CapaPresentacion/Controllers/OrdenRecaudacionController.cs)

**Archivo:** 1344 líneas  
**Estado:** ⚠️ ALTO - Valores hardcodeados y constructores duales

#### 🔴 P0-5: Constructores Inconsistentes

**Líneas 37-63** - DOS constructores contradictorios:

```csharp
// Constructor con DI (línea 37)
public OrdenRecaudacionController(IOrdenRecaudacionOrchestrator orchestrator)
{
    _orchestrator = orchestrator;
}

// Constructor sin parámetros (línea 43) - INSTANCIA DIRECTA
public OrdenRecaudacionController()
{
    try
    {
        _ordenDAO = new OrdenRecaudacionDAO();  // ❌ Hardcoded dependency
        ...
    }

    // Inicializa orchestrator con NEW (línea 58)
    _orchestrator = new OrdenRecaudacionOrchestrator(
        new OrdenRecaudacionDAO(),  // ❌ Hardcoded
        new PagoDAO(),               // ❌ Hardcoded
        null,
        null,
        new CapaNegocio.Services.EmailService(),  // ❌ Hardcoded
        null
    );
}
```

**Problemas:**
- ❌ DI inútil si siempre se llama constructor sin parámetros
- ❌ Imposible hacer testing unitario efectivo
- ❌ Violación de principio de Inversión de Dependencias
- ❌ Acoplamiento fuerte con implementaciones concretas

#### 🟡 P1-1: Valores Hardcodeados en Tarifas

**Líneas 578-593** - Método `AsegurarConceptosBasicos()`:

```csharp
private void AsegurarConceptosBasicos()
{
    var conceptos = new List<CapaDatos.Models.ConceptoModel>
    {
        new ConceptoModel { 
            Codigo = "EMI_AOCR", 
            ValorBase = 3300m,      // ❌ HARDCODED
            PorcentajeAdmin = 0m,   // ❌ HARDCODED
            ...
        },
        new ConceptoModel { 
            Codigo = "INSPECCION_EXT", 
            ValorBase = 500m,       // ❌ HARDCODED
            ...
        },
        new ConceptoModel { 
            Codigo = "VIATICOS_INSPECTOR", 
            ValorBase = 80m,        // ❌ HARDCODED
            PorcentajeAdmin = 8m,   // ❌ HARDCODED
            ...
        }
    };
}
```

**Tarifas hardcodeadas encontradas:**
- EMI_AOCR: $3,300.00
- REN_AOCR: $3,300.00
- MOD_AOCR_INC: $1,600.00
- MOD_AOCR_SIN_INC: $80.00
- INSPECCION_EXT: $500.00 por estación
- VIATICOS_INSPECTOR: $80.00 por día + 8% admin

**NOTA:** Ya existe una solución documentada en `SOLUCION_TARIFAS_CONFIGURABLES.md` con métodos `ObtenerTarifaConfigurable()` y `ObtenerPorcentajeConfigurable()`, pero NO está aplicada en el código actual.

**Línea 987** - También tiene hardcoded:
```csharp
CodigoUsuario = 1, // Use hardcoded user for test
```

---

### 4. MODELOS (CapaModelo/OrdenRecaudacion/OrdenRecaudacion.cs)

**Archivo:** 28 líneas  
**Estado:** ⚠️ MEDIO - Inconsistencia de tipos

#### 🟡 P1-2: Tipos Inconsistentes

```csharp
public class OrdenRecaudacion
{
    public int Id { get; set; }                         
    public int CodigoUsuario { get; set; }              // ✅ CORRECTO (int)
    public string CodigoSolicitud { get; set; }         // ❌ DEBERÍA SER int
    public string NumeroOrden { get; set; }             // ✅ CORRECTO
    public DateTime FechaCreacion { get; set; }         
    public string Estado { get; set; }                  
    
    // Propiedades que NO existen en BD:
    public string Observacion { get; set; }             // ❌ NO EXISTE EN BD
    public decimal Subtotal { get; set; }               // ❌ NO EXISTE EN BD
    public decimal Admin { get; set; }                  // ❌ NO EXISTE EN BD
    public string LugarEmision { get; set; }            // ❌ NO EXISTE EN BD
    public string Correo { get; set; }                  // ❌ NO EXISTE EN BD
    public string Telefono { get; set; }                // ❌ NO EXISTE EN BD
    public int? ConceptoId { get; set; }                // ❌ NO EXISTE EN BD
    
    // Propiedades que SÍ existen:
    public string Compania { get; set; }                // ✅ EXISTE
    public string RucCedula { get; set; }               // ✅ EXISTE (corregido de "Ruc")
    public decimal Total { get; set; }                  // ✅ EXISTE
}
```

**Problema:** El modelo tiene propiedades que NO existen en la tabla real, causando errores en INSERT/UPDATE.

---

## 🎯 PLAN DE REPARACIÓN PRIORIZADO

### Fase 1: Fixes Críticos (P0) - OBLIGATORIO

#### Fix 1: Corregir SQL de INSERT DetalleLíneas 375-378)

**ANTES:**
```sql
INSERT INTO aocr_or_orden_detalle 
(orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion, 
 cantidad, valor_unitario, porcentaje_admin, subtotal, admin, total_linea)
VALUES (@ordenId, @conceptoId, @conceptoCodigo, @conceptoNombre, @descripcion,
        @cantidad, @valorUnitario, @porcentajeAdmin, @subtotal, @admin, @totalLinea)
```

**DESPUÉS:**
```sql
INSERT INTO aocr_or_orden_detalle 
(orden_id, concepto_id, concepto_nombre, cantidad, valor_unitario, total_linea)
VALUES (@ordenId, @conceptoId, @conceptoNombre, @cantidad, @valorUnitario, @totalLinea)
```

**Archivos:** `CapaDatos/DAOs/OrdenRecaudacionDAO.cs`

---

#### Fix 2: Corregir SQL de UPDATE (Líneas 410-423)

**ANTES:**
```sql
UPDATE aocr_or_orden SET
    codigo_usuario = @codigoUsuario,
    codigo_solicitud = @codigoSolicitud,
    numero_orden = @numeroOrden,
    estado = @estado,
    observacion = @observacion,         -- QUITAR
    subtotal = @subtotal,               -- QUITAR
    admin = @admin,                     -- QUITAR
    total = @total,
    lugar_emision = @lugarEmision,      -- QUITAR
    compania = @compania,
    ruc_cedula = @rucCedula,
    correo = @correo,                   -- QUITAR
    telefono = @telefono,               -- QUITAR
    concepto_id = @conceptoId           -- QUITAR
WHERE id = @id
```

**DESPUÉS:**
```sql
UPDATE aocr_or_orden SET
    codigo_usuario = @codigoUsuario,
    codigo_solicitud = @codigoSolicitud,
    numero_orden = @numeroOrden,
    estado = @estado,
    compania = @compania,
    ruc_cedula = @rucCedula,
    total = @total
WHERE id = @id
```

**Archivos:** `CapaDatos/DAOs/OrdenRecaudacionDAO.cs`

---

#### Fix 3: Eliminar Antipatrón Sync-over-Async

**Opción A - Convertir BL a Async (RECOMENDADO):**
```csharp
public class OrdenRecaudacionBL
{
    private readonly OrdenRecaudacionDAO _dao;
    
    // ✅ ASYNC
    public async Task<List<OrdenRecaudacion>> ListarPorUsuarioAsync(string usuario)
    {
        var result = await _dao.ObtenerTodosAsync();  // Ahora sí async real
        return new List<OrdenRecaudacion>(result);
    }
    
    public async Task<OrdenRecaudacion> ObtenerPorIdAsync(int id)
    {
        return await _dao.ObtenerPorIdAsync(id);
    }
    
    public async Task<int> InsertarAsync(OrdenRecaudacion orden)
    {
        return await _dao.CrearAsync(orden);
    }
    
    public async Task<bool> ActualizarAsync(OrdenRecaudacion orden)
    {
        return await _dao.ActualizarAsync(orden);
    }
    
    public async Task<bool> CambiarEstadoAsync(int id, string nuevoEstado, string observacion = null)
    {
        return await _dao.ActualizarEstadoAsync(id, nuevoEstado, "SYSTEM");
    }
}
```

**Opción B - Hacer DAO completamente Síncrono (si no se necesita async):**
```csharp
public class OrdenRecaudacionBL
{
    // Usar métodos síncronos directamente (ObtenerPorId, Insertar, etc.)
    // Eliminar referencias a *Async
}
```

**Impacto:** Requiere cambiar Controller a async también (ActionResult → async Task<ActionResult>)

**Archivos:** 
- `CapaNegocio/OrdenRecaudacionBL.cs`
- `CapaPresentacion/Controllers/OrdenRecaudacionController.cs` (si se elige Opción A)

---

#### Fix 4: Implementar DAO Async Real (si se elige Opción A)

**Reemplazar `Task.FromResult()` con operaciones async reales:**

```csharp
// ANTES (pseudo-async)
public Task<OrdenRecaudacion> ObtenerPorIdAsync(int id)
{
    return Task.FromResult(ObtenerPorId(id));  // ❌ Bloquea
}

// DESPUÉS (async real con Dapper)
public async Task<OrdenRecaudacion> ObtenerPorIdAsync(int id)
{
    const string sql = @"
        SELECT o.*, c.nombre as concepto_nombre 
        FROM aocr_or_orden o 
        LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id 
        WHERE o.id = @id";
    
    using (var conn = new NpgsqlConnection(_connectionString))
    {
        await conn.OpenAsync();
        return await conn.QueryFirstOrDefaultAsync<OrdenRecaudacion>(sql, new { id });
    }
}

public async Task<IEnumerable<OrdenRecaudacion>> ObtenerTodosAsync()
{
    const string sql = @"
        SELECT o.*, c.nombre as concepto_nombre 
        FROM aocr_or_orden o 
        LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id";
    
    using (var conn = new NpgsqlConnection(_connectionString))
    {
        await conn.OpenAsync();
        return await conn.QueryAsync<OrdenRecaudacion>(sql);
    }
}

public async Task<int> CrearAsync(OrdenRecaudacion orden)
{
    const string sql = @"
        INSERT INTO aocr_or_orden (
            codigo_usuario, codigo_solicitud, numero_orden,
            fecha_creacion, estado, compania, ruc_cedula, total
        ) VALUES (
            @CodigoUsuario, @CodigoSolicitud, @NumeroOrden,
            @FechaCreacion, @Estado, @Compania, @RucCedula, @Total
        ) RETURNING id";
    
    using (var conn = new NpgsqlConnection(_connectionString))
    {
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, orden);
    }
}
```

**Archivos:** `CapaDatos/DAOs/OrdenRecaudacionDAO.cs`

---

#### Fix 5: Resolver Constructores Duales en Controller

**ANTES:**
```csharp
private OrdenRecaudacionDAO _ordenDAO;
private readonly OrdenRecaudacionDAO _dao = new OrdenRecaudacionDAO();  // ❌
private readonly OrdenRecaudacionBL _bl = new OrdenRecaudacionBL();     // ❌
private readonly IOrdenRecaudacionOrchestrator _orchestrator;

public OrdenRecaudacionController(IOrdenRecaudacionOrchestrator orchestrator)
{
    _orchestrator = orchestrator;
}

public OrdenRecaudacionController()  // ❌ Este se llama siempre
{
    _ordenDAO = new OrdenRecaudacionDAO();
    // ...instancias hardcoded...
}
```

**DESPUÉS:**
```csharp
private readonly IOrdenRecaudacionRepository _dao;
private readonly IOrdenRecaudacionOrchestrator _orchestrator;
private readonly IConceptoDAO _conceptoDao;
private readonly ISolicitudAOCRDAO _solicitudDao;
private readonly IBancoP9DAO _bancoDao;

// UN SOLO constructor con DI
public OrdenRecaudacionController(
    IOrdenRecaudacionRepository dao,
    IOrdenRecaudacionOrchestrator orchestrator,
    IConceptoDAO conceptoDao,
    ISolicitudAOCRDAO solicitudDao,
    IBancoP9DAO bancoDao)
{
    _dao = dao ?? throw new ArgumentNullException(nameof(dao));
    _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    _conceptoDao = conceptoDao ?? throw new ArgumentNullException(nameof(conceptoDao));
    _solicitudDao = solicitudDao ?? throw new ArgumentNullException(nameof(solicitudDao));
    _bancoDao = bancoDao ?? throw new ArgumentNullException(nameof(bancoDao));
}
```

**Configurar DI en App_Start/UnityConfig.cs o Startup.cs:**
```csharp
container.RegisterType<IOrdenRecaudacionRepository, OrdenRecaudacionDAO>();
container.RegisterType<IOrdenRecaudacionOrchestrator, OrdenRecaudacionOrchestrator>();
container.RegisterType<IConceptoDAO, ConceptoDAO>();
// etc.
```

**Archivos:** 
- `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
- `App_Start/UnityConfig.cs` o `Startup.cs`

---

### Fase 2: Fixes Alta Prioridad (P1)

#### Fix 6: Implementar Tarifas Configurables

**Aplicar solución de SOLUCION_TARIFAS_CONFIGURABLES.md:**

```csharp
private void AsegurarConceptosBasicos()
{
    var conceptos = new List<CapaDatos.Models.ConceptoModel>
    {
        new ConceptoModel { 
            Codigo = "EMI_AOCR", 
            Nombre = "Emisión AOCR",
            TipoCalculo = "FIJO",
            ValorBase = ObtenerTarifaConfigurable("TARIFA_EMI_AOCR", 3300m),  // ✅ DESDE BD
            PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_EMI_AOCR", 0m),
            Activo = true,
            Orden = 1,
            ...
        },
        new ConceptoModel { 
            Codigo = "VIATICOS_INSPECTOR", 
            Nombre = "Viáticos a Sres. Inspectores",
            TipoCalculo = "POR_DIA",
            ValorBase = ObtenerTarifaConfigurable("TARIFA_VIATICOS_INSPECTOR", 80m),
            PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_VIATICOS", 8m),
            Activo = true,
            Orden = 6,
            EsViatico = true
        }
    };

    foreach (var c in conceptos)
    {
        _conceptoDao.Upsert(c);
    }
}

private decimal ObtenerTarifaConfigurable(string clave, decimal valorPorDefecto)
{
    try
    {
        var parametro = _parametroDao.ObtenerPorClave(clave);
        if (parametro != null && !string.IsNullOrEmpty(parametro.Valor))
        {
            if (decimal.TryParse(parametro.Valor, out decimal valor))
            {
                return valor;
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error obteniendo tarifa configurable '{clave}'");
    }
    
    return valorPorDefecto;  // Fallback
}

private decimal ObtenerPorcentajeConfigurable(string clave, decimal valorPorDefecto)
{
    return ObtenerTarifaConfigurable(clave, valorPorDefecto);
}
```

**Ejecutar script SQL:**
```sql
INSERT INTO parametros (clave, valor, descripcion, tipo, activo) VALUES
('TARIFA_EMI_AOCR', '3300.00', 'Tarifa emisión AOCR', 'DECIMAL', true),
('TARIFA_REN_AOCR', '3300.00', 'Tarifa renovación AOCR', 'DECIMAL', true),
('TARIFA_MOD_AOCR_INC', '1600.00', 'Tarifa modificación con inclusión', 'DECIMAL', true),
('TARIFA_MOD_AOCR_SIN_INC', '80.00', 'Tarifa modificación sin incremento', 'DECIMAL', true),
('TARIFA_INSPECCION_EXT', '500.00', 'Tarifa inspección por estación', 'DECIMAL', true),
('TARIFA_VIATICOS_INSPECTOR', '80.00', 'Viáticos por día', 'DECIMAL', true),
('PORCENTAJE_ADMIN_VIATICOS', '8.00', 'Porcentaje admin sobre viáticos', 'DECIMAL', true)
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    fecha_modificacion = NOW();
```

**Archivos:**
- `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
- `scripts/insert_parametros_tarifas.sql`

---

#### Fix 7: Limpiar Modelo de Propiedades Inexistentes

**Opción A - Eliminar propiedades sin tabla (RECOMENDADO):**
```csharp
public class OrdenRecaudacion
{
    // Propiedades que SÍ existen en BD
    public int Id { get; set; }
    public int CodigoUsuario { get; set; }              // ✅ Corregido a int
    public int? CodigoSolicitud { get; set; }           // ✅ Corregido a int?
    public string NumeroOrden { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string Estado { get; set; }
    public string Compania { get; set; }
    public string RucCedula { get; set; }
    public decimal Total { get; set; }
    
    // Lista de detalles (relación)
    public List<OrdenRecaudacionDetalle> Detalles { get; set; } = new List<OrdenRecaudacionDetalle>();
    
    // ELIMINADAS: Observacion, Subtotal, Admin, LugarEmision, Correo, Telefono, ConceptoId
}
```

**Opción B - Agregar columnas a BD (si se necesitan):**
```sql
ALTER TABLE aocr_or_orden
ADD COLUMN observacion TEXT,
ADD COLUMN subtotal NUMERIC(18,2),
ADD COLUMN admin NUMERIC(18,2),
ADD COLUMN lugar_emision VARCHAR(100),
ADD COLUMN correo VARCHAR(100),
ADD COLUMN telefono VARCHAR(20),
ADD COLUMN concepto_id INTEGER REFERENCES aocr_or_concepto(id);
```

**Archivos:**
- `CapaModelo/OrdenRecaudacion/OrdenRecaudacion.cs`
- `scripts/alter_aocr_or_orden.sql` (si se elige Opción B)

---

### Fase 3: Testing y Validación

#### Test 1: Prueba de Insert/Update
```csharp
[TestMethod]
public async Task Insert_OrdenRecaudacion_SinErrorColumnas()
{
    // Arrange
    var dao = new OrdenRecaudacionDAO(connectionString);
    var orden = new OrdenRecaudacion
    {
        CodigoUsuario = 1,
        CodigoSolicitud = 100,
        NumeroOrden = "OR-2026-001",
        FechaCreacion = DateTime.Now,
        Estado = "BORRADOR",
        Compania = "Test SA",
        RucCedula = "1234567890",
        Total = 3300m
    };
    
    // Act
    int ordenId = await dao.CrearAsync(orden);
    
    // Assert
    Assert.IsTrue(ordenId > 0);
    
    var ordenRecuperada = await dao.ObtenerPorIdAsync(ordenId);
    Assert.IsNotNull(ordenRecuperada);
    Assert.AreEqual("OR-2026-001", ordenRecuperada.NumeroOrden);
}
```

#### Test 2: Prueba de Async Pattern
```csharp
[TestMethod]
public async Task BL_ListarPorUsuario_NoDeadlock()
{
    var bl = new OrdenRecaudacionBL();
    
    // Simular alta concurrencia (10 requests simultáneos)
    var tasks = Enumerable.Range(0, 10).Select(async i => 
    {
        return await bl.ListarPorUsuarioAsync("test@dgac.gob.ec");
    });
    
    var results = await Task.WhenAll(tasks);
    
    // Si hay deadlock, este test timeout
    Assert.AreEqual(10, results.Length);
}
```

#### Test 3: Validar Tarifas Configurables
```csharp
[TestMethod]
public void AsegurarConceptosBasicos_UsaTarifasConfigurables()
{
    // Arrange: Insertar parámetro de prueba
    _parametroDao.Insertar(new Parametro 
    { 
        Clave = "TARIFA_TEST", 
        Valor = "5000.00",
        Tipo = "DECIMAL",
        Activo = true
    });
    
    var controller = new OrdenRecaudacionController(/* inyectar dependencias */);
    
    // Act
    decimal tarifa = controller.ObtenerTarifaConfigurable("TARIFA_TEST", 3300m);
    
    // Assert
    Assert.AreEqual(5000m, tarifa, "Debe leer desde BD, no usar default");
}
```

---

## 📈 IMPACTO ESTIMADO

### Antes de los Fixes
- ❌ INSERT/UPDATE fallan con errores 42703 (columna inexistente)
- ❌ Riesgo de deadlock en producción (sync-over-async)
- ❌ Impossible cambiar tarifas sin recompilar código
- ❌ Testing unitario imposible (dependencias hardcoded)
- ❌ Performance degradada (bloqueo de threads)

### Después de los Fixes
- ✅ INSERT/UPDATE funcionan correctamente
- ✅ Async/await real sin riesgo de deadlock
- ✅ Tarifas configurables en BD sin recompilación
- ✅ Testing unitario habilitado (DI correcto)
- ✅ Performance mejorada (threads no bloqueados)

---

## 🚀 ESTRATEGIA DE IMPLEMENTACIÓN

### Paso 1: Ambiente de Pruebas
```powershell
# Crear branch para fixes
git checkout -b fix/orden-recaudacion-module

# Backup de BD de pruebas
pg_dump -h 172.20.16.55 -U postgres -d aocr_db > backup_pre_fixes.sql
```

### Paso 2: Aplicar Fixes P0 (uno por uno)
```powershell
# Fix 1: SQL INSERT detalle
# Editar OrdenRecaudacionDAO.cs líneas 375-378

# Fix 2: SQL UPDATE orden
# Editar OrdenRecaudacionDAO.cs líneas 410-423

# Fix 3 y 4: Async pattern
# Refactorizar BL y DAO a async real

# Fix 5: Constructor único
# Refactorizar Controller + configurar DI

# COMPILAR Y PROBAR después de cada fix
dotnet build
dotnet test
```

### Paso 3: Aplicar Fixes P1
```powershell
# Fix 6: Tarifas configurables
# Ejecutar insert_parametros_tarifas.sql
psql -h 172.20.16.55 -U postgres -d aocr_db -f scripts/insert_parametros_tarifas.sql

# Modificar AsegurarConceptosBasicos()

# Fix 7: Limpiar modelo
# Elegir Opción A o B y aplicar
```

### Paso 4: Testing Integral
```powershell
# Ejecutar suite de tests
dotnet test --verbosity detailed

# Pruebas manuales en QA
# - Crear orden nueva
# - Actualizar orden existente
# - Generar PDF
# - Registrar pago
# - Cambiar estados
```

### Paso 5: Deploy a Producción
```powershell
# Merge a develop
git checkout develop
git merge fix/orden-recaudacion-module

# Deploy con Azure Pipelines
# (ver azure-pipelines.yml)
```

---

## 📋 CHECKLIST DE VALIDACIÓN

### Pre-Deploy
- [ ] Compilación sin errores ni warnings
- [ ] Todos los tests unitarios pasan
- [ ] Tests de integración con BD de pruebas pasan
- [ ] Code review aprobado
- [ ] Documentación actualizada
- [ ] Backup de BD de producción creado

### Post-Deploy
- [ ] Verificar INSERT orden funciona (POST /OrdenRecaudacion/Nueva)
- [ ] Verificar UPDATE orden funciona (PUT /OrdenRecaudacion/Editar/{id})
- [ ] Verificar listado funciona (GET /OrdenRecaudacion)
- [ ] Verificar generación PDF funciona (GET /OrdenRecaudacion/GenerarPDF/{id})
- [ ] Verificar cambio de estados funciona (POST /OrdenRecaudacion/CambiarEstado)
- [ ] Monitorear logs de IIS por 24 horas (buscar deadlocks o timeouts)
- [ ] Verificar performance (tiempo de respuesta < 2 segundos)

---

## 🔍 CONCLUSIONES

El módulo de Órdenes de Recaudación requiere **reparación urgente (P0)** antes de uso en producción. Los 5 problemas críticos identificados causan:

1. **Errores de SQL** (INSERT/UPDATE fallan)
2. **Riesgo de deadlock** (antipatrón sync-over-async)
3. **Mantenibilidad baja** (valores hardcoded)
4. **Testing imposible** (dependencias acopladas)
5. **Arquitectura inconsistente** (constructores duales)

**Tiempo estimado de fixes:** 3-5 días de desarrollo + 2 días de testing

**Riesgo de NO arreglar:** ALTO - Módulo no funcional en producción, posibles caídas de aplicación por deadlocks.

**Recomendación:** Aplicar TODOS los fixes P0 antes de habilitar módulo en producción. Fixes P1 pueden posponerse pero se recomienda aplicarlos en siguiente sprint.

---

## 📚 REFERENCIAS

- [SOLUCION_TARIFAS_CONFIGURABLES.md](SOLUCION_TARIFAS_CONFIGURABLES.md)
- [RCA_ORDENES.md](RCA_ORDENES.md)
- [AUDITORIA_VINCULO.md](AUDITORIA_VINCULO.md)
- [Don't Block on Async Code - Stephen Cleary](https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html)
- [Async/Await Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

---

**Próximo paso:** Decidir si reparar o deshabilitar temporalmente el módulo hasta que se apliquen los fixes.
