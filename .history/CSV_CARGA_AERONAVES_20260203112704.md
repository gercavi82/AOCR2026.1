# Guía: Cargar Aeronaves mediante CSV

## Problema Resuelto
✅ Agregada validación mejorada para carga de CSV  
✅ Mejores mensajes de error cuando faltan datos  
✅ Prevención de envío sin aeronaves  

## Requisitos del Archivo CSV

### Formato
- **Extensión:** `.csv`
- **Delimitador:** `;` (punto y coma) o `,` (coma)
- **Codificación:** UTF-8
- **Encabezado:** Obligatorio (primera fila)

### Estructura (8 columnas exactas)

```
Fabricante;Modelo;Serie;Matricula;Configuracion;EtapaRuido;Peso;DesignadorOASI
Boeing;737;MSN1234;N12345;Pasajeros;Stage 4;73500;B737
Airbus;A320;MSN5678;N54321;Carga;Stage 4;78000;A320
```

### Validaciones
1. **Exactamente 8 columnas** por fila
2. **Matrícula obligatoria** - no puede estar vacía
3. **Sin líneas vacías** entre datos
4. **Mínimo 1 aeronave** (además del encabezado)

### Ejemplo Correcto

```csv
Fabricante;Modelo;Serie;Matricula;Configuracion;EtapaRuido;Peso;DesignadorOASI
Boeing;737-800;25000;EC-LUN;Pasajeros;Stage 3;79000;B738
Boeing;737-700;28000;EC-LOI;Pasajeros;Stage 3;70080;B737
Airbus;A380;1;F-WWOW;Pasajeros;Stage 4;593000;A380
```

### Ejemplo Incorrecto ❌

```csv
Fabricante;Modelo;Serie;Matricula;Configuracion;EtapaRuido;Peso
Boeing;737;MSN1234;N12345;Pasajeros;Stage 4;73500  <- FALTA OASI
;;
Boeing;747;MSN5678;;Carga;Stage 4;310000           <- MATRÍCULA VACÍA
```

## Pasos para Cargar

1. **Abrir formulario** → Pestaña "Flota/Aeronaves"
2. **Descargar formato** → Click en botón "Descargar Formato"
3. **Completar datos** → Rellenar con aeronaves
4. **Cargar CSV** → Click en "Cargar CSV"
5. **Verificar** → Tabla se llena automáticamente
6. **Guardar** → Click en "Guardar Solicitud"

## Mensajes de Error y Soluciones

### "No contiene datos de aeronaves"
- [ ] Verificar que archivo tiene encabezado + datos
- [ ] Eliminar líneas vacías al final

### "Se esperaban 8 columnas pero se encontraron X"
- [ ] Contar columnas en archivo
- [ ] Verificar delimitador (`;` vs `,`)
- [ ] Eliminar espacios extras

### "La matrícula no puede estar vacía"
- [ ] Revisar fila sin matrícula
- [ ] Todas las filas deben tener matrícula

### Tabla vacía después de cargar
- [ ] Verificar formato del CSV
- [ ] Probar descargando formato incluido
- [ ] Abrir en Excel y resguardar como CSV

## Características Mejoradas

✅ **Validación antes de envío**  
- Aviso si no hay aeronaves en tabla  
- Indica cantidad exacta cargada  

✅ **Mejor detección de errores**  
- Salta líneas vacías automáticamente  
- Especifica qué líneas tiene errores  
- Permite continuar con datos válidos  

✅ **Reinicio del selector**  
- Puede cargar múltiples veces  
- Nuevo CSV borra anterior  

## Troubleshooting

| Problema | Solución |
|----------|----------|
| Archivo no se selecciona | Verificar permisos, tamaño < 5MB |
| Datos cargados pero tabla vacía | Refrescar página, reintentar |
| "undefined" en tabla | CSV con caracteres especiales, guardar como UTF-8 |
| Validación falla después de agregar manualmente | Asegurarse que todos campos tienen valor |

## Tecnología

- **FileReader API** para lectura local del CSV
- **Detección automática** de delimitador (`;` o `,`)
- **Validación robusta** antes de envío al servidor
- **Feedback inmediato** al usuario
