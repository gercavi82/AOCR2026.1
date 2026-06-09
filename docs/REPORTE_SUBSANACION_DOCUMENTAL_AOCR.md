# REPORTE_SUBSANACION_DOCUMENTAL_AOCR

Fecha: 2026-06-02

## 1. Alcance

Revision y correccion del flujo `SUBSANAR` / `SUBSANACION DOCUMENTAL` en AOCR, con foco en:

- mantener la version documental reenviada por el RT;
- devolver la solicitud a `Subsanada`;
- permitir que el inspector revise los documentos subsanados;
- evitar correos duplicados al inspector en `SubsanarPost`;
- no apagar globalmente el evento `SUBSANADA`, porque existe otra ruta a `Subsanada` sin la notificacion manual `DOCUMENTACION_SUBSANADA_RT`.

## 2. Diagnostico

El flujo de `SubsanarPost` ya conservaba el contrato documental principal:

1. la solicitud debe estar en `Observada`;
2. se obtienen los documentos pendientes mediante `RevisionDocumentalService.ObtenerDocumentosPendientesSubsanacion(...)`;
3. cada archivo reenviado se guarda como nueva version del documento;
4. el nuevo documento queda con estado `PENDIENTE_REVISION_SUBSANACION`;
5. se registra revision documental para que el inspector lo vuelva a evaluar;
6. la solicitud cambia a `Subsanada`;
7. se envia una notificacion especifica `DOCUMENTACION_SUBSANADA_RT` al inspector.

El problema encontrado estaba en la capa de cambio de estado. `SubsanarPost` usaba el cambio generico `CambiarEstadoConReglasAocr(...)`; al pasar a `Subsanada`, esa ruta podia generar tambien:

- correo generico `AOCR_CAMBIO_ESTADO`;
- correo workflow `SOLICITUD_SUBSANADA`;
- correo especifico del controlador `DOCUMENTACION_SUBSANADA_RT`.

Esto podia producir multiples correos para la misma subsanacion documental.

## 3. Correccion Aplicada

### `CapaNegocio/SolicitudEstadoTransitionBL.cs`

Se agrego un overload de `CambiarEstadoConReglasAocr(...)` con dos flags explicitos:

- `omitirCorreoGenericoCambioEstado`;
- `omitirCorreoWorkflowEstado`.

El overload anterior se mantiene intacto y delega con ambos flags en `false`, por lo que los demas callers conservan el comportamiento previo.

La notificacion interna y el historial de estado no se eliminan. Solo se permite que un caller especifico controle si debe emitir el correo generico y/o el correo workflow asociado al estado.

### `CapaPresentacion/Controllers/SolicitudAOCRController.cs`

`SubsanarPost` ahora cambia a `Subsanada` mediante:

```csharp
CambiarEstadoSubsanadaDesdeSubsanarPost(codigoSolicitud, observacionCambio, out mensajeCambio)
```

Ese helper llama al overload nuevo con:

```csharp
omitirCorreoGenericoCambioEstado: true
omitirCorreoWorkflowEstado: true
```

Despues de cambiar el estado, el controlador conserva la notificacion especifica:

```csharp
NotificarInspectorDocumentacionSubsanada(...)
```

Resultado esperado para `SubsanarPost`: se conserva `DOCUMENTACION_SUBSANADA_RT` y se evita duplicar con `AOCR_CAMBIO_ESTADO` / `SOLICITUD_SUBSANADA` en esa ruta.

## 4. Contratos Conservados

- No se cambio la carga de archivos.
- No se cambio la version de documentos reenviados.
- No se cambio el estado documental `PENDIENTE_REVISION_SUBSANACION`.
- No se cambio el filtro centralizado de documentos pendientes de subsanacion.
- No se cambio la matriz global de transiciones.
- No se suprimio `SUBSANADA` globalmente en `SolicitudEstadoTransitionBL`.
- No se tocaron descargas finales, legalizacion, estados finales ni vistas de cierre.

## 5. Pruebas

Se actualizo la prueba de caracterizacion:

- `SubsanacionRt_ShouldRemainVersionedAndReturnSolicitudToSubsanada`

Verifica que:

- `SubsanarPost` sigue creando documentos `PENDIENTE_REVISION_SUBSANACION`;
- el filtro documental sigue centralizado en `RevisionDocumentalService`;
- el cambio a `Subsanada` usa el helper especifico de `SubsanarPost`;
- el helper llama al overload con ambos flags `true`;
- se conserva `NotificarInspectorDocumentacionSubsanada`.

Validacion ejecutada:

```powershell
MSBuild.exe AOCR.Tests\AOCR.Tests.csproj /t:Build /p:Configuration=Debug /v:m /nr:false
vstest.console.exe AOCR.Tests\bin\Debug\AOCR.Tests.dll /Logger:Console
```

Resultado:

- Build: exitoso.
- Pruebas: 158 totales, 157 correctas, 1 omitida.
- Advertencia conocida: binding redirect de `Microsoft.Bcl.AsyncInterfaces`.

## 6. Riesgo Residual

No se ejecuto una prueba funcional con navegador ni base de datos real en este cierre. La evidencia automatizada cubre contratos de codigo y evita regresion estructural, pero la validacion operativa recomendada sigue siendo ejecutar una subsanacion real y confirmar en cola/campanas que solo queda el correo externo esperado para el inspector: `DOCUMENTACION_SUBSANADA_RT`.
