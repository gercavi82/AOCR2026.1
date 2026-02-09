# PRÓXIMOS PASOS - Módulo OrdenRecaudacion

**Fecha:** 7 de febrero de 2026  
**Estado actual:** Fixes críticos P0 completados + Tarifas parametrizadas

---

## ✅ COMPLETADO HASTA AHORA

### Fase 1: Análisis ✅
- [x] Auditoría completa de 290 archivos
- [x] Identificación de 5 bugs críticos (P0)
- [x] Identificación de 3 bugs alta prioridad (P1)
- [x] Documentación detallada generada

### Fase 2: Fixes Críticos P0 ✅
- [x] **P0-1:** Validar columnas `aocr_or_orden_detalle` (12 columnas existen)
- [x] **P0-2:** Validar columnas `aocr_or_orden` (16 columnas existen)
- [x] **P0-3:** Convertir OrdenRecaudacionBL a async/await
- [x] **P0-4:** Eliminar antipatrón `.Result` en OrdenRecaudacionBL
- [x] **P0-5:** DOCUMENTADO (requiere config manual DI)

### Fase 3: Mejoras P1 ✅
- [x] **P1-1:** Crear tabla `parametros` (estructura creada)
- [x] **P1-1:** Insertar 11 parámetros de tarifas (completado)
- [x] **Parametrización:** Modificar `AsegurarConceptosBasicos()` para usar BD
- [x] **Helpers:** Crear métodos `ObtenerTarifaConfigurable()` y `ObtenerPorcentajeConfigurable()`

---

## 🔄 PENDIENTE: Alta Prioridad

### 1. Actualizar Controller a Async/Await ⏳

**Impacto:** Alto - Mejora rendimiento y previene deadlocks en producción  
**Complejidad:** Media-Alta (3-4 horas)  
**Prioridad:** ⭐⭐⭐⭐

#### Métodos a actualizar:

##### 1.1 Index() - Línea 75
```csharp
// ANTES
public ActionResult Index(string estado)
{
    var ordenes = _dao.ListarPorUsuarioModel(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();
    return View(ordenes);
}

// DESPUÉS
public async Task<ActionResult> Index(string estado)
{
    var ordenes = await _dao.ListarPorUsuarioModelAsync(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();
    return View(ordenes);
}
```
**Nota:** Requiere crear `ListarPorUsuarioModelAsync()` en OrdenRecaudacionDAO si no existe.

---

##### 1.2 Nueva() POST - Línea 132
```csharp
// ANTES
public ActionResult Nueva(OrdenRecaudacionNuevaVM model)
{
    var numeroOrden = GenerarNumeroOrden(); // Usa .Result internamente
    var ordenId = _dao.Insertar(orden);
    _dao.CrearDetalleAsync(detalle).Wait(); // ❌ BLOQUEO
}

// DESPUÉS
public async Task<ActionResult> Nueva(OrdenRecaudacionNuevaVM model)
{
    var numeroOrden = await GenerarNumeroOrdenAsync();
    var ordenId = await _dao.InsertarAsync(orden);
    await _dao.CrearDetalleAsync(detalle); // ✅ ASYNC
}
```
**Cambios necesarios:**
- Crear `GenerarNumeroOrdenAsync()` que use `await _dao.ObtenerConsecutivoDiarioAsync(fecha)`
- Cambiar `_dao.Insertar()` por `_dao.InsertarAsync()` o `_dao.CrearAsync()`

---

##### 1.3 Detalles() - Línea 307
```csharp
// ANTES
public ActionResult Detalles(int id)
{
    var orden = _dao.ObtenerOrdenPorIdModel(id);
    ViewBag.Pagos = _dao.ObtenerPagosPorOrden(id);
    return View(orden);
}

// DESPUÉS
public async Task<ActionResult> Detalles(int id)
{
    var orden = await _dao.ObtenerOrdenPorIdModelAsync(id);
    ViewBag.Pagos = await _dao.ObtenerPagosPorOrdenAsync(id);
    return View(orden);
}
```
**Nota:** Crear métodos async en DAO si no existen.

---

##### 1.4 Editar() GET y POST - Líneas 338 y 357
```csharp
// GET - ANTES
public ActionResult Editar(int id)
{
    var orden = _dao.ObtenerOrdenPorIdModel(id);
    return View(orden);
}

// GET - DESPUÉS
public async Task<ActionResult> Editar(int id)
{
    var orden = await _dao.ObtenerOrdenPorIdModelAsync(id);
    return View(orden);
}

// POST - ANTES
public ActionResult Editar(OrdenRecaudacionModel model)
{
    bool result = _dao.ActualizarOrdenModel(model);
    return RedirectToAction("Index");
}

// POST - DESPUÉS
public async Task<ActionResult> Editar(OrdenRecaudacionModel model)
{
    bool result = await _dao.ActualizarOrdenModelAsync(model);
    return RedirectToAction("Index");
}
```

---

##### 1.5 RegistrarPago() - Línea 726
```csharp
// ANTES
public ActionResult RegistrarPago(int id, string Monto, ...)
{
    var orden = _dao.ObtenerOrdenPorIdModel(id);
    // ... lógica de pago
    _dao.ActualizarOrdenModel(orden);
}

// DESPUÉS
public async Task<ActionResult> RegistrarPago(int id, string Monto, ...)
{
    var orden = await _dao.ObtenerOrdenPorIdModelAsync(id);
    // ... lógica de pago
    await _dao.ActualizarOrdenModelAsync(orden);
}
```

---

##### 1.6 Generar() - Línea 432
```csharp
// ANTES
public ActionResult Generar(int id)
{
    var orden = _dao.ObtenerOrdenPorIdModel(id);
    _dao.CambiarEstadoOrden(id, "GENERADA");
    return RedirectToAction("Detalles", new { id });
}

// DESPUÉS
public async Task<ActionResult> Generar(int id)
{
    var orden = await _dao.ObtenerOrdenPorIdModelAsync(id);
    await _dao.CambiarEstadoOrdenAsync(id, "GENERADA");
    return RedirectToAction("Detalles", new { id });
}
```

---

##### 1.7 Anular() - Líneas 405 y 891
```csharp
// JSON - ANTES
public JsonResult Anular(int id)
{
    var orden = _dao.ObtenerOrdenPorIdModel(id);
    bool result = _dao.CambiarEstadoOrden(id, "ANULADA");
    return Json(new { success = result });
}

// JSON - DESPUÉS
public async Task<JsonResult> Anular(int id)
{
    var orden = await _dao.ObtenerOrdenPorIdModelAsync(id);
    bool result = await _dao.CambiarEstadoOrdenAsync(id, "ANULADA");
    return Json(new { success = result });
}

// POST - Similar al JSON
[HttpPost]
public async Task<ActionResult> Anular(int id, string motivo)
{
    // ... mismo patrón
}
```

---

#### Lista completa de métodos a convertir:

| # | Método | Línea | Llamadas a convertir | Estimado |
|---|--------|-------|---------------------|----------|
| 1 | `Index(string estado)` | 75 | `ListarPorUsuarioModel()` | 15 min |
| 2 | `Nueva() POST` | 132 | `GenerarNumeroOrden()`, `Insertar()`, `CrearDetalleAsync()` | 30 min |
| 3 | `Detalles(int id)` | 307 | `ObtenerOrdenPorIdModel()`, `ObtenerPagosPorOrden()` | 20 min |
| 4 | `Editar(int id) GET` | 338 | `ObtenerOrdenPorIdModel()` | 10 min |
| 5 | `Editar() POST` | 357 | `ActualizarOrdenModel()` | 15 min |
| 6 | `Anular() JSON` | 405 | `ObtenerOrdenPorIdModel()`, `CambiarEstadoOrden()` | 15 min |
| 7 | `Generar(int id)` | 432 | `ObtenerOrdenPorIdModel()`, `CambiarEstadoOrden()` | 15 min |
| 8 | `Enviar(int id)` | 483 | `ObtenerOrdenPorIdModel()` | 20 min |
| 9 | `RegistrarPago() GET` | 522 | `ObtenerOrdenPorIdModel()` | 10 min |
| 10 | `RegistrarPago() POST` | 726 | Multiple DAO calls | 40 min |
| 11 | `Anular() POST` | 891 | `ObtenerOrdenPorIdModel()`, `CambiarEstadoOrden()` | 15 min |
| 12 | `DescargarPdf(int id)` | 946 | `ObtenerOrdenPorIdModel()` | 15 min |
| 13 | `ValidarPago()` | 1291 | `ObtenerOrdenPorIdModel()` | 20 min |
| 14 | `RechazarPago()` | 1320 | `ObtenerOrdenPorIdModel()` | 20 min |

**TOTAL ESTIMADO:** 3-4 horas

---

### 2. Verificar/Crear Métodos Async en DAO ⏳

**Archivo:** `CapaDatos\DAOs\OrdenRecaudacionDAO.cs`

#### Métodos que deben existir:

```csharp
// Verificar que existan estos métodos async en DAO
public async Task<List<OrdenRecaudacionModel>> ListarPorUsuarioModelAsync(int codigoUsuario, string estado)
public async Task<OrdenRecaudacionModel> ObtenerOrdenPorIdModelAsync(int id)
public async Task<int> InsertarAsync(OrdenRecaudacion orden) // Ya existe línea 1717
public async Task<bool> ActualizarOrdenModelAsync(OrdenRecaudacionModel model)
public async Task<bool> CambiarEstadoOrdenAsync(int id, string nuevoEstado)
public async Task<List<PagoModel>> ObtenerPagosPorOrdenAsync(int ordenId)
```

**Acción requerida:**
1. Abrir `OrdenRecaudacionDAO.cs`
2. Buscar cada método (usar Ctrl+F)
3. Si NO existe, crear versión async:
   ```csharp
   public Task<T> MetodoAsync(...)
   {
       return Task.FromResult(MetodoSync(...)); // Temporal
   }
   ```
4. (Opcional futuro) Reemplazar `Task.FromResult()` con implementación truly async

---

### 3. Configurar Dependency Injection (P0-5) ⏳

**Impacto:** Medio - Permite testing unitario y mejor arquitectura  
**Complejidad:** Baja (30 minutos)  
**Prioridad:** ⭐⭐⭐

#### Paso 1: Eliminar constructor sin parámetros

**Archivo:** `CapaPresentacion\Controllers\OrdenRecaudacionController.cs`

```csharp
// ELIMINAR líneas 43-65 (aproximadamente)
public OrdenRecaudacionController()
{
    try
    {
        _ordenDAO = new OrdenRecaudacionDAO();
        // ...
    }
    catch (Exception ex)
    {
        // ...
    }

    _orchestrator = new OrdenRecaudacionOrchestrator(
        new OrdenRecaudacionDAO(),
        new PagoDAO(),
        null,
        null,
        new CapaNegocio.Services.EmailService(),
        null
    );
}
```

#### Paso 2: Crear/Actualizar UnityConfig.cs

**Archivo:** `App_Start\UnityConfig.cs` (crear si no existe)

```csharp
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Interfaces;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;

namespace CapaPresentacion
{
    public static class UnityConfig
    {
        public static void RegisterComponents()
        {
            var container = new UnityContainer();

            // Registrar DAOs
            container.RegisterType<OrdenRecaudacionDAO>(new PerRequestLifetimeManager());
            container.RegisterType<ConceptoDAO>(new PerRequestLifetimeManager());
            container.RegisterType<SolicitudAOCRDAO>(new PerRequestLifetimeManager());
            container.RegisterType<BancoP9DAO>(new PerRequestLifetimeManager());
            container.RegisterType<PagoDAO>(new PerRequestLifetimeManager());
            container.RegisterType<ParametroDAO>(new PerRequestLifetimeManager());

            // Registrar servicios
            container.RegisterType<IEmailService, EmailService>(new PerRequestLifetimeManager());

            // Registrar orchestrator
            container.RegisterType<IOrdenRecaudacionOrchestrator, OrdenRecaudacionOrchestrator>(
                new PerRequestLifetimeManager()
            );

            DependencyResolver.SetResolver(new UnityDependencyResolver(container));
        }
    }
}
```

#### Paso 3: Llamar desde Global.asax.cs

**Archivo:** `Global.asax.cs`

```csharp
protected void Application_Start()
{
    AreaRegistration.RegisterAllAreas();
    FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
    RouteConfig.RegisterRoutes(RouteTable.Routes);
    BundleConfig.RegisterBundles(BundleTable.Bundles);
    
    // AGREGAR ESTA LÍNEA
    UnityConfig.RegisterComponents();
}
```

---

## 🧪 PENDIENTE: Testing

### Testing Manual

**Checklist de pruebas funcionales:**

```
[ ] 1. GET /OrdenRecaudacion - Listar órdenes
    [ ] Filtro por estado funciona
    [ ] Estadísticas mostradas correctamente
    [ ] Paginación (si existe)

[ ] 2. GET /OrdenRecaudacion/Nueva - Formulario nueva orden
    [ ] Conceptos cargados con tarifas de BD
    [ ] Valores correctos (3300, 1600, 80, 500)

[ ] 3. POST /OrdenRecaudacion/Nueva - Crear orden
    [ ] Orden se crea con número único
    [ ] Detalles insertados correctamente
    [ ] Totales calculados bien
    [ ] Columnas nuevas (subtotal, admin, etc.) guardadas

[ ] 4. GET /OrdenRecaudacion/Detalles/5 - Ver orden
    [ ] Datos completos
    [ ] Pagos listados
    [ ] PDF descargable

[ ] 5. POST /OrdenRecaudacion/RegistrarPago - Subir comprobante
    [ ] Archivo se sube
    [ ] Estado cambia a PENDIENTE
    [ ] Validación de monto

[ ] 6. POST /OrdenRecaudacion/Generar - Generar orden
    [ ] Estado cambia a GENERADA
    [ ] Email enviado (si aplica)

[ ] 7. POST /OrdenRecaudacion/Anular - Anular orden
    [ ] Estado cambia a ANULADA
    [ ] Motivo guardado
```

### Testing de Tarifas Configurables

**Checklist específico:**

```sql
-- 1. Cambiar tarifa EMI_AOCR a $4000
UPDATE parametros SET valor = '4000.00' WHERE clave = 'TARIFA_EMI_AOCR';

-- 2. Crear nueva orden y verificar precio es $4000 (no $3300)

-- 3. Cambiar porcentaje admin viáticos a 12%
UPDATE parametros SET valor = '12.00' WHERE clave = 'PORCENTAJE_ADMIN_VIATICOS';

-- 4. Crear orden con viáticos y verificar cálculo correcto

-- 5. Desactivar parámetro
UPDATE parametros SET activo = false WHERE clave = 'TARIFA_EMI_AOCR';

-- 6. Verificar que usa fallback ($3300 hardcoded)

-- 7. Reactivar
UPDATE parametros SET activo = true WHERE clave = 'TARIFA_EMI_AOCR';
```

### Testing Unitario (Opcional)

**Archivo:** `AOCR.Tests\Unit\OrdenRecaudacionControllerTests.cs`

```csharp
[TestClass]
public class OrdenRecaudacionControllerTests
{
    [TestMethod]
    public void ObtenerTarifaConfigurable_ConParametroDB_RetornaValorDB()
    {
        // Arrange
        var mockParametroDao = new Mock<ParametroDAO>();
        mockParametroDao.Setup(x => x.ObtenerPorClave("TARIFA_EMI_AOCR"))
            .Returns(new Parametro { Clave = "TARIFA_EMI_AOCR", Valor = "5000.00", Activo = true });

        // Act
        var controller = new OrdenRecaudacionController();
        var tarifa = controller.ObtenerTarifaConfigurable("TARIFA_EMI_AOCR", 3300m);

        // Assert
        Assert.AreEqual(5000m, tarifa);
    }

    [TestMethod]
    public void ObtenerTarifaConfigurable_ErrorBD_RetornaFallback()
    {
        // Arrange
        var mockParametroDao = new Mock<ParametroDAO>();
        mockParametroDao.Setup(x => x.ObtenerPorClave(It.IsAny<string>()))
            .Throws(new Exception("Error BD"));

        // Act
        var controller = new OrdenRecaudacionController();
        var tarifa = controller.ObtenerTarifaConfigurable("TARIFA_EMI_AOCR", 3300m);

        // Assert
        Assert.AreEqual(3300m, tarifa); // Fallback
    }
}
```

---

## 📋 PENDIENTE: Documentación

### 1. Manual de Usuario Final

**Título:** Cómo cambiar tarifas de órdenes de recaudación

**Audiencia:** Administradores financieros

**Contenido:**
1. Introducción a tabla `parametros`
2. Lista de tarifas configurables
3. Comandos SQL para modificar
4. Ejemplos prácticos
5. Cómo verificar cambios

### 2. Video Tutorial (Opcional)

**Duración:** 5 minutos

**Guion:**
1. Conectar a PostgreSQL con pgAdmin
2. Navegar a tabla `parametros`
3. Modificar valor de `TARIFA_EMI_AOCR`
4. Crear orden y verificar nuevo precio
5. Rollback si es necesario

---

## 🚀 DEPLOY

### Checklist Pre-Deploy

```
[ ] 1. Backup de BD producción
    [ ] pg_dump completo
    [ ] Backup tabla parametros específicamente

[ ] 2. Verificar parámetros en BD producción
    [ ] Ejecutar SELECT para validar 11 registros existen
    [ ] Si no existen, ejecutar insert_parametros_tarifas.sql

[ ] 3. Compilar solución completa
    [ ] 0 errores de compilación
    [ ] Warnings revisados

[ ] 4. Testing en QA
    [ ] Todos los tests manuales pasados
    [ ] Verificar tarifas configurables funcionan

[ ] 5. Deploy a producción
    [ ] Backup IIS application pool
    [ ] Copiar binarios
    [ ] Reciclar app pool
    [ ] Smoke test (crear una orden)

[ ] 6. Monitoreo post-deploy
    [ ] Revisar logs de IIS (primeras 2 horas)
    [ ] Verificar no hay errores 500
    [ ] Validar tiempos de respuesta normales
```

---

## 📊 MÉTRICAS DE ÉXITO

### Antes de los fixes:
- ❌ Riesgo de deadlock por `.Result` en BL
- ❌ Tarifas hardcodeadas en código
- ❌ Requiere recompilación para cambiar precios
- ❌ Sin auditabilidad de cambios de tarifas

### Después de los fixes:
- ✅ Async/await correcto en BL (P0-3, P0-4)
- ✅ Tarifas configurables desde BD (P1-1)
- ✅ Cambios sin recompilación
- ✅ Auditabilidad completa en tabla `parametros`
- ✅ Fallback seguro a valores por defecto

---

## 🔮 MEJORAS FUTURAS (Post-Implementación)

### 1. Interfaz Administrativa Web

**Controlador:** `ParametrosController.cs`
**Vista:** `Views/Parametros/Index.cshtml`

Permitir gestión de parámetros sin SQL.

### 2. Cache de Parámetros

Implementar cache en memoria para evitar consultas BD repetitivas:

```csharp
private static Dictionary<string, (decimal Valor, DateTime Expira)> _cacheParametros = 
    new Dictionary<string, (decimal, DateTime)>();

private decimal ObtenerTarifaConfigurableConCache(string clave, decimal valorPorDefecto)
{
    if (_cacheParametros.ContainsKey(clave) && _cacheParametros[clave].Expira > DateTime.Now)
    {
        return _cacheParametros[clave].Valor;
    }

    var valor = ObtenerTarifaConfigurable(clave, valorPorDefecto);
    _cacheParametros[clave] = (valor, DateTime.Now.AddMinutes(30)); // Cache 30 min
    return valor;
}
```

### 3. Historial de Cambios de Tarifas

Tabla nueva: `parametros_historial`

```sql
CREATE TABLE parametros_historial (
    id SERIAL PRIMARY KEY,
    parametro_id INT REFERENCES parametros(codigoparametro),
    valor_anterior TEXT,
    valor_nuevo TEXT,
    fecha_cambio TIMESTAMP DEFAULT NOW(),
    usuario_cambio VARCHAR(50),
    motivo TEXT
);
```

Trigger para registrar automáticamente cambios.

---

**FIN DEL DOCUMENTO** - Actualizado: 7 de febrero de 2026
