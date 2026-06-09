# REPORTE_PDF_SOLICITUD_INSPECCIONES_BRANDING

Fecha: 2026-06-03

## 1. Diagnostico

El PDF de `Solicitud de Inspecciones` se generaba sin cabecera ni pie institucional. El archivo validado:

- `Solicitud_Inspecciones_DGAC_OR_2026_AOCR005_1432766_ONTARIO_INC_20260603100238.pdf`

tenia `images 0`, por lo que no contenia ningun recurso grafico embebido. La hoja oficial de referencia:

- `Hoja membretada DGAC - 2025.pdf`

tenia `images 5` y mostraba la linea grafica completa: Republica del Ecuador, Direccion General de Aviacion Civil, barra superior, datos de pie y `El Nuevo Ecuador`.

Causa raiz final: para esta generacion no fue suficiente depender de `--header-html`, `--footer-html` ni de imagenes en el layout Razor. La salida activa podia terminar sin imagenes aun cuando el contenido textual se actualizaba. Por eso la solucion definitiva se movio al post-proceso del PDF final.

## 2. Correccion Definitiva

Se incorporo la hoja membretada oficial al proyecto:

- `CapaPresentacion/Content/imganes/hoja/Hoja_membretada_DGAC_2025.pdf`

Despues de generar el PDF de contenido con Rotativa, `BuildSolicitudInspeccionPdfBytes` llama a `PdfBrandingHelper.ApplyLetterheadBackground(...)`.

Ese metodo:

- abre el PDF generado;
- abre la hoja membretada oficial;
- importa la pagina 1 de la hoja oficial;
- la escala a la pagina A4 del PDF generado;
- la estampa sobre cada pagina con iTextSharp;
- conserva el nombre de archivo, flujo, estados, firmas y datos dinamicos existentes.

La vista `SolicitudInspeccionesPdf.cshtml` reserva espacio institucional con:

- `padding-top: 42mm`
- `padding-bottom: 34mm`

Esto evita que la cabecera oficial cubra el titulo o que el pie institucional cubra contenido.

## 3. Archivos Modificados

- `CapaNegocio/Helpers/PdfBrandingHelper.cs`
- `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
- `CapaPresentacion/Views/OrdenRecaudacion/SolicitudInspeccionesPdf.cshtml`
- `CapaPresentacion/CapaPresentacion.csproj`
- `CapaPresentacion/Content/imganes/hoja/Hoja_membretada_DGAC_2025.pdf`

Cambios relevantes:

- Se agrego `LetterheadVirtualPath`.
- Se agrego `ApplyLetterheadBackground(...)`.
- Se registra log `[PDF-BRANDING]` con ruta fisica, `Exists`, tamano y bytes finales.
- `CapaPresentacion.csproj` incluye la hoja membretada como `Content` con `PreserveNewest`.
- Se corrigieron tildes y textos pegados del bloque legal.

## 4. Evidencia Visual

Se renderizo el PDF con problema:

- `TestResults/pdf-branding-check/Solicitud_Inspecciones_DGAC_OR_2026_AOCR005_1432766_ONTARIO_INC_20260603100238_page1.png`

Resultado: sin cabecera ni pie institucional.

Se aplico la hoja membretada oficial al mismo PDF:

- `TestResults/pdf-branding-check/Solicitud_Inspecciones_20260603100238_con_hoja_membretada.pdf`

Ese PDF contiene `images 5`.

Se valido ademas el espaciado final con el contenido desplazado para no tapar el titulo:

- `TestResults/pdf-branding-check/Solicitud_Inspecciones_letterhead_spacing_check.pdf`
- `TestResults/pdf-branding-check/Solicitud_Inspecciones_letterhead_spacing_check_page1.png`

Resultado observado:

- aparece el logo Republica del Ecuador;
- aparece `Direccion General de Aviacion Civil`;
- aparece la barra superior;
- aparece el pie institucional;
- aparece `El Nuevo Ecuador`;
- el titulo y tablas no quedan tapados;
- no aparecen recuadros vacios.

## 5. Validacion

Comandos ejecutados:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' 'AOCR.Tests\AOCR.Tests.csproj' /t:Build /p:Configuration=Debug /v:m /nr:false
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' 'CapaPresentacion\CapaPresentacion.csproj' /t:Build /p:Configuration=Debug /v:m /nr:false
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' 'AOCR.Tests\bin\Debug\AOCR.Tests.dll' /Logger:Console
```

Resultado:

- Build `AOCR.Tests`: correcto.
- Build `CapaPresentacion`: correcto.
- Pruebas: 159 totales, 158 correctas, 1 omitida.
- Advertencia conocida: binding redirect de `Microsoft.Bcl.AsyncInterfaces`.

## 6. Revision Operativa

Al regenerar desde la UI, el log debe contener:

- `[PDF-BRANDING] ... Hoja membretada PDF ... Exists=True`
- `[PDF-BRANDING] ... Hoja membretada aplicada correctamente ...`

Si la hoja oficial no se encuentra en publicado, el log dejara la ruta fisica resuelta y el PDF se devolvera sin romper el flujo.
