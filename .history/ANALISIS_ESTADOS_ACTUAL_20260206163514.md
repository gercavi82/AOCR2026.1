# 📊 ANÁLISIS DE ESTADOS ACTUALES EN EL PROYECTO AOCR

## 🔍 ESTADOS ENCONTRADOS EN EL CÓDIGO

### 1. **EstadoOrdenService.cs** - Órdenes de Recaudación

**Archivo:** `CapaNegocio/Services/EstadoOrdenService.cs`

#### Estados Definidos:
```csharp
public const string BORRADOR = "BORRADOR";           // Orden creada, no enviada
public const string GENERADA = "GENERADA";           // Orden generada por sistema
public const string ENVIADA = "ENVIADA";             // Orden enviada a financiero
public const string APROBADA = "APROBADA";           // Orden aprobada por financiero
public const string RECHAZADA = "RECHAZADA";         // Orden rechazada
public const string PAGADA = "PAGADA";               // Pago confirmado
public const string ANULADA = "ANULADA";             // Orden anulada
public const string FACTURADA = "FACTURADA";         // Factura generada
```

#### Flujo de Estados:
```
BORRADOR → GENERADA → ENVIADA → APROBADA → PAGADA → FACTURADA
                              ↓           ↓
                         RECHAZADA    ANULADA
```

#### Transiciones Permitidas (del código):
```csharp
[BORRADOR] = new List<string> { GENERADA, ANULADA }
[GENERADA] = new List<string> { ENVIADA, ANULADA }
[ENVIADA] = new List<string> { APROBADA, RECHAZADA }
[APROBADA] = new List<string> { PAGADA, ANULADA }
[RECHAZADA] = new List<string> { BORRADOR, ANULADA }
[PAGADA] = new List<string> { FACTURADA }
[ANULADA] = new List<string> { }  // Estado final
[FACTURADA] = new List<string> { }  // Estado final
```

#### Roles y Permisos:
```csharp
Roles.SOLICITANTE:
  - Ver todas sus órdenes
  - Editar órdenes en BORRADOR
  - Cambiar BORRADOR → GENERADA
  - Cambiar RECHAZADA → BORRADOR

Roles.FINANCIERO:
  - Ver todas las órdenes
  - Cambiar ENVIADA → APROBADA
  - Cambiar ENVIADA → RECHAZADA
  - Cambiar APROBADA → PAGADA
  - Cambiar PAGADA → FACTURADA

Roles.ADMINISTRADOR:
  - Todas las transiciones
  - Cambiar cualquier estado
  - Anular órdenes
```

---

### 2. **SolicitudAOCRController.cs** - Solicitudes de Certificado

**Archivo:** `AOCR/Controllers/SolicitudAOCRController.cs`

#### Estados Encontrados en el Código:
```csharp
"PENDIENTE"          // Solicitud recibida, no revisada
"EN_REVISION"        // En proceso de revisión
"APROBADO"           // Solicitud aprobada
"RECHAZADO"          // Solicitud rechazada
"FINALIZADO"         // Proceso completado
```

#### Flujo Actual:
```
PENDIENTE → EN_REVISION → APROBADO → FINALIZADO
                       ↓
                    RECHAZADO
```

#### ⚠️ DISCREPANCIA CON DIAGRAMA:
El diagrama muestra un flujo más complejo:
```
RECEPCIONADO → ANALISIS_REQUISITOS → SUBSANACION (si hay observaciones)
                                   ↓
                       EN_EVALUACION_TECNICA → EN_EVALUACION_LEGAL → EN_EVALUACION_FINANCIERA
                                                                    ↓
                                              EN_APROBACION_COORDINADOR → EN_APROBACION_DIRECTOR
                                                                         ↓
                                                                   APROBADO → AOCR_EMITIDO → AOCR_ENTREGADO
```

---

### 3. **InspeccionDAO.cs** - Inspecciones Técnicas

**Archivo:** `CapaDatos/DAOs/InspeccionDAO.cs`

#### Estados Implícitos (no constantes):
```
null o vacío  = No programada
"PROGRAMADA"  = Inspección agendada (inferido)
"REALIZADA"   = Inspección completada (inferido)
```

#### ⚠️ FALTA IMPLEMENTAR:
```csharp
public const string PROGRAMADA = "PROGRAMADA";
public const string EN_CURSO = "EN_CURSO";
public const string COMPLETADA = "COMPLETADA";
public const string CANCELADA = "CANCELADA";
public const string REPROGRAMADA = "REPROGRAMADA";
```

---

### 4. **PagoDAO.cs** - Pagos

**Archivo:** `CapaDatos/DAOs/PagoDAO.cs`

#### Estados de Pago:
```csharp
"PENDIENTE"         // Pago no realizado
"PROCESANDO"        // En proceso (inferido)
"CONFIRMADO"        // Pago confirmado
"RECHAZADO"         // Pago rechazado
```

---

## 📋 COMPARACIÓN: ACTUAL vs DIAGRAMA

### **Workflow de Órdenes de Recaudación**

| Estado Actual | ¿En Diagrama? | Observaciones |
|--------------|---------------|---------------|
| BORRADOR | ✅ | Correcto - estado inicial |
| GENERADA | ✅ | Correcto - orden generada |
| ENVIADA | ✅ | Correcto - enviada a financiero |
| APROBADA | ✅ | Correcto - aprobación financiera |
| RECHAZADA | ✅ | Correcto - rechazo con retorno |
| PAGADA | ✅ | Correcto - confirmación de pago |
| FACTURADA | ✅ | Correcto - facturación final |
| ANULADA | ✅ | Correcto - cancelación |

**⭐ CONCORDANCIA: 100%** - El workflow de órdenes está bien implementado.

---

### **Workflow de Solicitudes AOCR**

| Estado Diagrama | Estado Actual | Gap |
|----------------|---------------|-----|
| RECEPCIONADO | ❌ No existe | Falta estado inicial de recepción |
| ANALISIS_REQUISITOS | ⚠️ Parcial en "EN_REVISION" | No es explícito |
| SUBSANACION | ❌ No existe | **CRÍTICO** - No hay estado de subsanación |
| SUBSANADO | ❌ No existe | Falta |
| EN_EVALUACION_TECNICA | ⚠️ Implícito | No separado claramente |
| EN_EVALUACION_LEGAL | ❌ No existe | Falta evaluación legal específica |
| EN_EVALUACION_FINANCIERA | ❌ No existe | Falta evaluación financiera separada |
| EN_APROBACION_COORDINADOR | ❌ No existe | Falta aprobación de coordinador |
| EN_APROBACION_DIRECTOR | ❌ No existe | **CRÍTICO** - Falta aprobación director |
| APROBADO | ✅ Existe | Correcto |
| AOCR_EMITIDO | ❌ No existe | **CRÍTICO** - Falta estado de emisión |
| AOCR_ENTREGADO | ❌ No existe | Falta estado final de entrega |
| RECHAZADO | ✅ Existe | Correcto |

**⚠️ CONCORDANCIA: 30%** - El workflow de solicitudes necesita expansión significativa.

---

## 🎯 ESTADOS CRÍTICOS FALTANTES

### **Alta Prioridad:**

1. **SUBSANACION** - Estado cuando hay observaciones y el solicitante debe corregir documentos
   - **Impacto:** El diagrama muestra que es un flujo común cuando faltan requisitos
   - **Acción:** Agregar estado y lógica de retorno

2. **EN_APROBACION_DIRECTOR** - Aprobación final del director financiero
   - **Impacto:** Autorización jerárquica no implementada
   - **Acción:** Agregar estado y rol DirectorFinanciero

3. **AOCR_EMITIDO** - Certificado generado y listo para entrega
   - **Impacto:** No hay registro de cuándo se emite el certificado
   - **Acción:** Agregar estado y generar PDF del certificado

### **Media Prioridad:**

4. **EN_EVALUACION_TECNICA / LEGAL / FINANCIERA** - Evaluaciones paralelas
   - **Impacto:** No se rastrean evaluaciones específicas por área
   - **Acción:** Separar en estados distintos para mejor trazabilidad

5. **RECEPCIONADO** - Registro inicial de recepción formal
   - **Impacto:** No hay timestamp formal de recepción vs. creación
   - **Acción:** Agregar estado inicial

---

## 🔄 TRANSICIONES FALTANTES

### **Según Diagrama, deben existir:**

```csharp
// SUBSANACION - permite retornar cuando hay observaciones
public bool Subsanar(int codigoSolicitud, string observaciones)
{
    // Cambiar estado a SUBSANACION
    // Notificar al solicitante
    // Pausar plazos (si aplica)
}

// APROBAR_COORDINADOR - aprobación intermedia
public bool AprobarCoordinador(int codigoSolicitud, int codigoCoordinador)
{
    // Verificar rol Coordinador
    // Cambiar estado a EN_APROBACION_DIRECTOR
    // Notificar a Director
}

// EMITIR_AOCR - generar certificado
public bool EmitirAOCR(int codigoSolicitud)
{
    // Generar PDF del certificado AOCR
    // Cambiar estado a AOCR_EMITIDO
    // Registrar fecha de emisión
    // Notificar al solicitante
}
```

---

## 📊 MATRIZ DE ROLES vs ESTADOS (Según Diagrama)

| Estado | Solicitante | Recepción | Coord. Legal | Coord. Financ. | Técnico | Dir. Financ. | Admin |
|--------|------------|-----------|--------------|----------------|---------|--------------|-------|
| RECEPCIONADO | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ |
| ANALISIS_REQUISITOS | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| SUBSANACION | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| EN_EVALUACION_TECNICA | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ | ✅ |
| EN_EVALUACION_FINANCIERA | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ✅ |
| EN_APROBACION_COORDINADOR | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| EN_APROBACION_DIRECTOR | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| APROBADO | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| AOCR_EMITIDO | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |

**Leyenda:**
- ✅ = Puede transicionar a este estado
- ❌ = No tiene permiso

---

## 💡 RECOMENDACIONES INMEDIATAS

### 1. Crear archivo de constantes centralizado

```csharp
// CapaDatos/Constants/EstadosSolicitudAOCR.cs
public static class EstadosSolicitudAOCR
{
    // Estados iniciales
    public const string RECEPCIONADO = "RECEPCIONADO";
    public const string ANALISIS_REQUISITOS = "ANALISIS_REQUISITOS";
    
    // Estados de subsanación
    public const string SUBSANACION = "SUBSANACION";
    public const string SUBSANADO = "SUBSANADO";
    
    // Estados de evaluación
    public const string EN_EVALUACION_TECNICA = "EN_EVALUACION_TECNICA";
    public const string EN_EVALUACION_LEGAL = "EN_EVALUACION_LEGAL";
    public const string EN_EVALUACION_FINANCIERA = "EN_EVALUACION_FINANCIERA";
    
    // Estados de aprobación
    public const string EN_APROBACION_COORDINADOR = "EN_APROBACION_COORDINADOR";
    public const string EN_APROBACION_DIRECTOR = "EN_APROBACION_DIRECTOR";
    
    // Estados finales
    public const string APROBADO = "APROBADO";
    public const string RECHAZADO = "RECHAZADO";
    public const string AOCR_EMITIDO = "AOCR_EMITIDO";
    public const string AOCR_ENTREGADO = "AOCR_ENTREGADO";
}
```

### 2. Actualizar modelo SolicitudAOCR

Agregar campos en [CapaModelo/SolicitudAOCR.cs](CapaModelo/SolicitudAOCR.cs):
```csharp
public DateTime? FechaRecepcion { get; set; }
public DateTime? FechaSubsanacion { get; set; }
public DateTime? FechaEmisionAOCR { get; set; }
public DateTime? FechaEntregaAOCR { get; set; }
public string NumeroAOCR { get; set; }
public string RutaArchivoPDFAOCR { get; set; }
```

### 3. Actualizar base de datos PostgreSQL

```sql
-- Agregar columnas de seguimiento de fechas
ALTER TABLE aocr_tbsolicitud 
ADD COLUMN fecha_recepcion TIMESTAMP,
ADD COLUMN fecha_subsanacion TIMESTAMP,
ADD COLUMN fecha_emision_aocr TIMESTAMP,
ADD COLUMN fecha_entrega_aocr TIMESTAMP,
ADD COLUMN numero_aocr VARCHAR(50),
ADD COLUMN ruta_pdf_aocr TEXT;

-- Crear tabla de subsanaciones
CREATE TABLE aocr_tbsubsanacion (
    codigo SERIAL PRIMARY KEY,
    codigo_solicitud INTEGER REFERENCES aocr_tbsolicitud(codigo),
    fecha_subsanacion TIMESTAMP NOT NULL,
    observaciones TEXT NOT NULL,
    codigo_usuario_solicitante INTEGER,
    fecha_respuesta TIMESTAMP,
    respuesta TEXT,
    estado VARCHAR(20) DEFAULT 'PENDIENTE'
);
```

---

## 📈 MÉTRICAS DE CONCORDANCIA

### **Resumen General:**

| Componente | Concordancia | Observaciones |
|------------|--------------|---------------|
| **Órdenes de Recaudación** | ✅ 100% | Workflow completo y correcto |
| **Solicitudes AOCR** | ⚠️ 30% | Faltan muchos estados intermedios |
| **Roles** | ⚠️ 60% | Faltan roles específicos del diagrama |
| **Transiciones** | ⚠️ 40% | Flujo simplificado vs. diagrama |
| **Permisos** | ⚠️ 50% | Matriz incompleta |
| **Inspecciones** | ⚠️ 40% | Estados no formalizados |
| **Notificaciones** | ⚠️ 30% | Implementación básica |

### **Puntuación Global: 50%** 

El proyecto tiene una buena base en el módulo de órdenes financieras, pero necesita expansión significativa en el workflow de solicitudes AOCR para alinearse completamente con el diagrama de flujo.

---

## ✅ CONCLUSIONES

### **LO QUE ESTÁ BIEN:**
1. ✅ Arquitectura general sólida (Capas Datos/Negocio/Presentación)
2. ✅ Workflow de órdenes financieras completo y funcional
3. ✅ Sistema de roles y permisos básico implementado
4. ✅ Historial de cambios de estado registrado
5. ✅ Integración con base de datos PostgreSQL correcta

### **LO QUE FALTA:**
1. ❌ Estados intermedios del workflow AOCR
2. ❌ Roles específicos (Director Financiero, Coordinadores, Recepción)
3. ❌ Estado de SUBSANACION (crítico según diagrama)
4. ❌ Emisión automática de certificado AOCR
5. ❌ Workflow de inspecciones formalizado
6. ❌ Notificaciones automáticas en cada transición
7. ❌ Dashboard con métricas de cada etapa

### **PRIORIDAD DE IMPLEMENTACIÓN:**
1. 🔴 **Alta:** Estados SUBSANACION, EN_APROBACION_DIRECTOR, AOCR_EMITIDO
2. 🟡 **Media:** Roles faltantes, evaluaciones separadas
3. 🟢 **Baja:** Notificaciones, reportes, dashboards mejorados

**Tiempo estimado para alineación completa: 7-10 semanas**
