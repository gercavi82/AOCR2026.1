# Flujo Completo de Carga de CSV y Guardado en Base de Datos

## Estado Actual ✅
- Archivo _FormularioEmisionAOCR.cshtml: **Limpio de caracteres especiales**
- Boton "Cargar CSV": **Funcional**
- Flujo de guardado: **Completamente integrado con BD**

## Como Funciona

### 1. Cargar Archivo CSV
1. Click en boton "Cargar CSV" en la pestaña "Informacion de Aeronaves"
2. Se abre dialogo de seleccion de archivo
3. Selecciona archivo .csv con las 8 columnas requeridas
4. El archivo se procesa localmente (sin enviar al servidor aun)
5. Se cargan las aeronaves en la tabla

### 2. Validaciones Implementadas
```
Validacion 1: Formato de archivo
- Solo .csv permitido
- Deteccion automatica de delimitador (; o ,)

Validacion 2: Estructura del CSV
- Exactamente 8 columnas por fila
- Encabezado obligatorio
- Lineas vacias ignoradas automaticamente

Validacion 3: Datos obligatorios
- Matricula NO puede estar vacia
- Se filtra automaticamente si la matricula falta

Validacion 4: Cantidad minima
- Minimo 1 aeronave para guardar solicitud
- Alerta si intenta guardar sin aeronaves
```

## Estructura del CSV Requerida

### Columnas (en este orden exacto)
1. Fabricante (ej: Boeing)
2. Modelo (ej: 737)
3. Serie (ej: MSN1234)
4. Matricula (ej: N12345) - OBLIGATORIO
5. Configuracion (ej: Pasajeros)
6. EtapaRuido (ej: Stage 4)
7. Peso (ej: 73500)
8. DesignadorOASI (ej: B737)

### Delimitadores Soportados
- ; (punto y coma) - Recomendado
- , (coma)
El sistema detecta automaticamente cual usar

### Ejemplo Valido
```csv
Fabricante;Modelo;Serie;Matricula;Configuracion;EtapaRuido;Peso;DesignadorOASI
Boeing;737;MSN1234;N12345;Pasajeros;Stage 4;73500;B737
Boeing;737;MSN5678;N54321;Carga;Stage 4;73500;B737
Airbus;A320;MSN9999;N99999;Pasajeros;Stage 4;78000;A320
```

## Flujo de Guardado en Base de Datos

### 1. Click en "Guardar Solicitud"
- Sistema valida que haya minimo 1 aeronave
- Si no hay, muestra alerta y vuelve sin enviar

### 2. Datos que se Envian
```javascript
POST /SolicitudAOCR/FormularioCompleto

{
  Solicitud: {
    CodigoSolicitud: 0,           // 0 = nuevo, >0 = actualizar
    NombreOperador: "...",        // Obligatorio
    RepresentanteLegal: "...",
    CedulaRepresentante: "...",
    Direccion: "...",
    Telefono: "...",
    Email: "...",
    Ruc: "...",
    RazonSocial: "...",
    TipoOperacion: "...",
    DescripcionOperacion: "...",
    ObservacionesGenerales: "...",
    Estado: "BORRADOR",
    TipoSolicitud: 1
  },
  Banco: "...",
  NumeroComprobante: "...",
  Aeronaves: [
    {
      Fabricante: "Boeing",
      Modelo: "737",
      Serie: "MSN1234",
      Matricula: "N12345",
      Configuracion: "Pasajeros",
      EtapaRuido: "Stage 4",
      Peso: "73500",
      DesignadorOASI: "B737"
    }
    // ... mas aeronaves
  ]
}
```

### 3. Procesamiento en Servidor

#### Paso 1: Guardar Solicitud
- Inserta/Actualiza en tabla aocr_tbsolicitud
- Retorna CodigoSolicitud (ID generado)

#### Paso 2: Guardar Aeronaves
- Limpia aeronaves previas de esa solicitud
- Inserta todas las nuevas en aocr_tbaeronave_solicitud
- Filtra automaticamente sin matricula

#### Paso 3: Guardar Pago (si aplica)
- Si Banco o NumeroComprobante estan rellenos:
  - Inserta en aocr_tbpago
  - Estado: "REGISTRADO"
  - Fecha automatica: hoy

#### Paso 4: Procesar Documentos (si aplica)
- Crea carpeta /Uploads/AOCR/{id}/
- Guarda archivos
- Registra en aocr_tbdocumento

### 4. Respuesta al Cliente
```json
EXITO:
{
  "success": true,
  "mensaje": "Solicitud AOCR registrada correctamente.",
  "id": 123
}

ERROR:
{
  "success": false,
  "mensaje": "Descripcion del error"
}
```

### 5. Redireccion
- Si success = true:
  - Si id > 0: Redirige a /SolicitudAOCR/Detalle/{id}
  - Si id = 0: Redirige a /SolicitudAOCR/MisSolicitudes

## Tablas de BD Involucradas

### aocr_tbsolicitud
Campos guardados desde vm.Solicitud:
- codigo_solicitud (PK)
- nombre_operador
- representante_legal
- cedula_representante
- direccion
- telefono
- email
- ruc
- razon_social
- tipo_operacion
- descripcion_operacion
- observaciones_generales
- estado
- tipo_solicitud
- codigo_usuario
- fecha_creacion

### aocr_tbaeronave_solicitud
Campos guardados desde cada item de vm.Aeronaves:
- codigo_solicitud (FK)
- fabricante
- modelo
- serie
- matricula
- configuracion
- etapa_ruido
- peso
- designador_oasi

### aocr_tbpago
Campos guardados si vm.Banco o vm.NumeroComprobante:
- codigo_solicitud (FK)
- metodo_pago (banco)
- numero_comprobante
- estado ("REGISTRADO")
- fecha_pago (DateTime.Now)

## Depuracion y Logs

### Console.log en JavaScript
```javascript
console.log('Delimitador detectado:', delimitador);
console.log(`Aeronaves cargadas: ${contadorAeronaves}`);
console.log('Enviando datos:', vm);
console.log('Total de aeronaves:', aeronaves.length);
```

### Ver en Browser Console (F12 -> Console)
- Delimitador usado
- Cantidad de aeronaves cargadas
- Estructura del JSON enviado

### Respuesta del Servidor
- Abre F12 -> Network -> busca FormularioCompleto
- Click para ver respuesta JSON completa
- Verifica si success = true o false

## Casos de Error Comun

| Situacion | Solucion |
|-----------|----------|
| "No se cargaron aeronaves validas" | Verificar que CSV tiene 8 columnas exactas |
| "Fila X: Matricula vacia" | Completar matricula en esa fila |
| "Se esperaban 8 columnas pero..." | Contar columnas, revisar delimitador |
| "Por favor agregue al menos una aeronave" | Cargar CSV o agregar manualmente |
| "Sesion expirada" | Volver a iniciar sesion |
| "No tiene permisos..." | No es dueno de la solicitud |

## Checklist Final

✅ Archivo limpio de caracteres especiales
✅ Boton "Cargar CSV" abre dialogo correctamente
✅ Validacion de formato CSV robusta
✅ Tabla se llena correctamente con datos CSV
✅ Aeronaves se pueden agregar manualmente tambien
✅ Validacion previa al guardar (minimo 1 aeronave)
✅ Datos se envian al servidor por JSON POST
✅ Controlador recibe y procesa correctamente
✅ Todos los datos se guardan en BD
✅ Se retorna ID de solicitud creada
✅ Redireccion a pagina detalle correcta
