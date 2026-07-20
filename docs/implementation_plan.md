# Implementación de la FASE 1: Infraestructura Aditiva FR3

Esta fase introduce la infraestructura base para el nuevo flujo asíncrono (Outbox) del proceso FR3, sin alterar la funcionalidad actual (Legacy).

## User Review Required

> [!IMPORTANT]  
> Esta fase es estrictamente aditiva. El flujo actual seguirá funcionando bajo el modo Legacy de manera predeterminada.
> 
> Por favor, revise el plan y confirme si está de acuerdo con los nombres de las tablas y las clases propuestas antes de que comience a escribir el código.

## Proposed Changes

---

### Base de Datos y Migraciones

#### [NEW] [20260719_aocr_fr3_outbox.sql](file:///c:/proyectos/AOCR/scripts/20260719_aocr_fr3_outbox.sql)
Creará la tabla PostgreSQL ocr_fr3_outbox.

#### [NEW] [20260719_aocr_fr3_outbox_rollback.sql](file:///c:/proyectos/AOCR/scripts/20260719_aocr_fr3_outbox_rollback.sql)
Script idempotente para hacer drop a la tabla ocr_fr3_outbox.

---

### Capa de Modelo y Entidades

#### [NEW] [Fr3ProcessingMode.cs](file:///c:/proyectos/AOCR/CapaModelo/Common/Fr3ProcessingMode.cs)
Enum: Legacy, Outbox, Disabled.

#### [NEW] [Fr3Configuration.cs](file:///c:/proyectos/AOCR/CapaModelo/Common/Fr3Configuration.cs)
Clase tipada para encapsular los settings.

#### [NEW] [Fr3OutboxEvent.cs](file:///c:/proyectos/AOCR/CapaDatos/Entidades/Fr3OutboxEvent.cs)
Entidad mapeada a la tabla ocr_fr3_outbox.

---

### Capa de Acceso a Datos (DAO)

#### [NEW] [IFr3OutboxDAO.cs](file:///c:/proyectos/AOCR/CapaDatos/Interfaces/IFr3OutboxDAO.cs)
Interfaz con métodos de inserción.

#### [NEW] [Fr3OutboxDAO.cs](file:///c:/proyectos/AOCR/CapaDatos/DAOs/Fr3OutboxDAO.cs)
Implementación parametrizada.

---

### Capa de Negocio (Servicios)

#### [NEW] [Fr3ConfigurationProvider.cs](file:///c:/proyectos/AOCR/CapaNegocio/Services/Fr3ConfigurationProvider.cs)
Servicio que lee de Web.config.

---

### Capa de Presentación

#### [MODIFY] [HealthController.cs](file:///c:/proyectos/AOCR/CapaPresentacion/Controllers/HealthController.cs)
Exponer configuración FR3.

---

### Pruebas (Tests)

#### [NEW] [Fr3ConfigurationTests.cs](file:///c:/proyectos/AOCR/AOCR.Tests/Unit/Fr3ConfigurationTests.cs)
#### [NEW] [Fr3OutboxIntegrationTests.cs](file:///c:/proyectos/AOCR/AOCR.Tests/Integration/Fr3OutboxIntegrationTests.cs)

---

## Verification Plan
Construir y ejecutar las pruebas en AOCR.Tests.
