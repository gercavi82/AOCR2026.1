# GATE 7 — Descargas seguras

Fecha de validación: 2026-07-14  
Rama: `firma-dirdac-tec`  
Commit de referencia: `da964be92ca99370c92e4aa3e0c29f283fcd2cb0`

## Resultado

Se incorporó `DocumentoSeguroService` como control central *fail-closed*. El servicio normaliza rutas con `Path.GetFullPath`, limita el acceso a raíces privadas permitidas, comprueba relación documento-solicitud, existencia, longitud, extensión, MIME por firma mágica y puntos de reanálisis, normaliza el nombre de descarga y registra auditoría sin revelar la ruta física.

Los controles de acceso se ejecutan antes de resolver el archivo: propiedad de la solicitud/compañía para RT, asignación y relación de inspección para Inspector, ámbito institucional mediante autorización para Coordinación/Jefatura y DCAV, y relación documento-solicitud en `DocumentoController`.

| Documento | Endpoint | Roles | Validación de pertenencia | Directorio permitido | Prueba asociada | Resultado |
|---|---|---|---|---|---|---|
| NC firmada por Inspector | `Inspeccion/DescargarNcInspector` | Inspector, Administrador | NC–inspección–solicitud y asignación | `App_Data` | Inspector asignado/no asignado | Aprobado |
| NC firmada por Coordinador | `Inspeccion/DescargarNcCoordinador` | Coordinación/Jefatura, Administrador | Ámbito, NC–inspección–solicitud | `App_Data` | Relación institucional | Aprobado |
| NC notificada al RT | `RT/DescargarNcRt` | RT, Administrador | Propietario y estado permitido | `App_Data` | RT propietario/ajeno | Aprobado |
| Documento original observado | `Documento/Descargar` | Roles del expediente | Documento–solicitud, propietario/ámbito | `App_Data` | Documento no relacionado | Aprobado |
| Nueva versión subsanada | `Documento/Descargar` | RT/Inspector/Coordinación según expediente | Documento–solicitud y acceso al expediente | `App_Data` | RT/Inspector y relación | Aprobado |
| Versiones históricas | `Documento/Descargar` | Roles del expediente | Documento–solicitud | `App_Data` | Documento histórico válido | Aprobado |
| PDF histórico general | `RT/DescargarSubsanacionNc`, `Inspeccion/DescargarSubsanacionNc` | RT propietario; Inspector asignado; Administrador | NC–solicitud/inspección | `App_Data/SubsanacionesNC` o `App_Data` institucional | Histórico y GATE 7A | Aprobado |
| LV/EAE | `Inspeccion/VerListaVerificacionOperacionalEae` | Inspector asignado y ámbito institucional | Inspección–solicitud y revisión habilitada | `App_Data/Uploads/Inspecciones` | Inspector asignado/no asignado | Aprobado |
| Informe Técnico | `Inspeccion/VerInforme` y revisión Dirección | Inspector/Coordinación/DCAV según estado | Inspección–informe–solicitud y autorización final | `App_Data/Uploads/Inspecciones` | Relación, ruta y contenido PDF | Aprobado |
| AOCR final | `SolicitudAOCR/DescargarAOCRGenerada`, `DescargarAocrFirmada`, `FirmaAocr/*Pdf` | Propietario y roles institucionales habilitados | Documento/firma–solicitud y estado | `App_Data` | Relación, raíz, PDF incompatible | Aprobado |
| Condiciones y Limitaciones | `SolicitudAOCR/DescargarCondicionesLimitacionesModificacion`, `FirmaAocr/*Pdf` | Propietario, Coordinación, DCAV/DIRDAC, Administrador | Firma–solicitud, tipo y estado final | `App_Data` | Relación, raíz, auditoría | Aprobado |

## Validaciones ejecutadas

- Build Debug y compilación Razor: aprobados.
- Build Release y compilación Razor: aprobados.
- Pruebas focales GATE 7 + GATE 7A: 25/25 aprobadas.
- Suite global: 306 ejecutadas; 286 aprobadas; 19 fallidas conocidas; 1 omitida.
- Línea base anterior: 291 ejecutadas; 271 aprobadas; 19 fallidas; 1 omitida. Las 15 pruebas nuevas aprobaron y el conjunto de 19 fallos no aumentó.
- Auditoría Razor: cero enlaces `href` construidos desde propiedades `Ruta...`.
- Auditoría de `File(path)`: las descargas persistidas del alcance usan el servicio central. Los retornos directos restantes son PDFs generados en memoria o vistas previas temporales construidas desde un token normalizado, confinadas a su carpeta temporal y no desde una ruta almacenada en base de datos.

Permanece la advertencia conocida de build de `itext.commons` respecto de `System.IO.Compression`; no impide compilar y no fue introducida por este GATE.

## Pruebas de seguridad incorporadas

Las 15 pruebas cubren propietario/ajeno, Inspector asignado/no asignado, traversal, salida de raíz, inexistencia, vacío, extensión, firma mágica PDF, nombre malicioso, relación con solicitud, histórico, auditoría y ausencia de filtración de ruta física. Resultados reproducibles en `TestResults/gate7-all-focal-final.trx` y `TestResults/gate7-global-final.trx`.
