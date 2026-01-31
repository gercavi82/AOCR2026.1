/**
 * Modulo principal para el formulario de emision AOCR
 */
var FormularioAOCR = (function($) {
    'use strict';

    // ==========================================
    // CONFIGURACION
    // ==========================================
    var config = {
        maxFileSize: 2 * 1024 * 1024, // 2 MB
        urlGuardar: '',
        urlIndex: '',
        urlLogin: ''
    };

    // ==========================================
    // ESTADO
    // ==========================================
    var state = {
        companias: new Set(),
        aeronavesSeleccionadas: []
    };

    // ==========================================
    // MODULO: AERONAVES
    // ==========================================
    var Aeronaves = {
        actualizarContador: function() {
            var resumen = {};
            $('#tablaAeronaves tbody tr').each(function() {
                var fabricante = $(this).find('td').eq(0).text().trim();
                var modelo = $(this).find('td').eq(1).text().trim();
                var key = fabricante + ' - ' + modelo;
                resumen[key] = (resumen[key] || 0) + 1;
            });

            var texto = '';
            for (var key in resumen) {
                if (resumen.hasOwnProperty(key)) {
                    texto += key + ': ' + resumen[key] + ' aeronave(s)\n';
                }
            }
            $('#contadorAeronaves').val(texto.trim());
        },

        agregar: function(datos) {
            var fila = '<tr>' +
                '<td>' + datos.fabricante + '</td>' +
                '<td>' + datos.modelo + '</td>' +
                '<td>' + datos.serie + '</td>' +
                '<td>' + datos.matricula + '</td>' +
                '<td>' + datos.configuracion + '</td>' +
                '<td>' + datos.etapaRuido + '</td>' +
                '<td>' + datos.peso + '</td>' +
                '<td>' + datos.oaci + '</td>' +
                '<td class="text-center">' +
                '<button type="button" class="btn btn-outline-danger btn-sm btn-eliminar-aeronave" title="Eliminar" aria-label="Eliminar aeronave">' +
                '<i class="fas fa-trash-alt" aria-hidden="true"></i></button>' +
                '</td></tr>';
            $('#tablaAeronaves tbody').append(fila);
            this.actualizarContador();
        },

        eliminar: function(btn) {
            $(btn).closest('tr').remove();
            this.actualizarContador();
        },

        validarFormulario: function() {
            var fabricante = $('#fabricante').val().trim();
            var modelo = $('#modelo').val().trim();
            var serie = $('#serie').val().trim();
            var matricula = $('#matricula').val().trim();
            var configuracion = $('#configuracion').val().trim();

            if (!fabricante || !modelo || !serie || !matricula || !configuracion) {
                Utilidades.mostrarAlerta('warning', 'Campos requeridos', 'Por favor complete todos los campos obligatorios.');
                return null;
            }

            return {
                fabricante: fabricante,
                modelo: modelo,
                serie: serie,
                matricula: matricula,
                configuracion: configuracion,
                etapaRuido: $('#etapaRuido').val().trim(),
                peso: $('#peso').val().trim(),
                oaci: $('#designadorOASI').val().trim().toUpperCase()
            };
        },

        cargarCSV: function(archivo) {
            var self = this;
            if (!archivo || !archivo.name.endsWith('.csv')) {
                Utilidades.mostrarAlerta('error', 'Archivo invalido', 'Por favor seleccione un archivo .csv valido.');
                return;
            }

            var lector = new FileReader();
            lector.onload = function(e) {
                var lineas = e.target.result.trim().split('\n');
                if (lineas.length < 2) {
                    Utilidades.mostrarAlerta('error', 'Sin datos', 'El archivo CSV no contiene datos.');
                    return;
                }

                var delimitador = Utilidades.detectarDelimitador(lineas[0]);
                $('#tablaAeronaves tbody').empty();

                for (var i = 1; i < lineas.length; i++) {
                    var fila = lineas[i].split(delimitador);
                    if (fila.length !== 8) {
                        Utilidades.mostrarAlerta('error', 'Error en CSV', 'Error en la fila ' + (i + 1) + ': Se esperaban 8 columnas.');
                        $('#tablaAeronaves tbody').empty();
                        return;
                    }

                    self.agregar({
                        fabricante: fila[0].trim(),
                        modelo: fila[1].trim(),
                        serie: fila[2].trim(),
                        matricula: fila[3].trim(),
                        configuracion: fila[4].trim(),
                        etapaRuido: fila[5].trim(),
                        peso: fila[6].trim(),
                        oaci: fila[7].trim()
                    });
                }
                Utilidades.mostrarAlerta('success', 'Carga exitosa', 'Se cargaron ' + (lineas.length - 1) + ' aeronaves.');
            };
            lector.readAsText(archivo, 'UTF-8');
        },

        obtenerLista: function() {
            var lista = [];
            $('#tablaAeronaves tbody tr').each(function() {
                var celdas = $(this).find('td');
                lista.push({
                    Fabricante: celdas.eq(0).text().trim(),
                    Modelo: celdas.eq(1).text().trim(),
                    Serie: celdas.eq(2).text().trim(),
                    Matricula: celdas.eq(3).text().trim(),
                    Configuracion: celdas.eq(4).text().trim(),
                    EtapaRuido: celdas.eq(5).text().trim(),
                    Peso: celdas.eq(6).text().trim(),
                    DesignadorOACI: celdas.eq(7).text().trim()
                });
            });
            return lista;
        }
    };

    // ==========================================
    // MODULO: COMPANIAS
    // ==========================================
    var Companias = {
        agregar: function() {
            var select = document.getElementById('selectorCompanias');
            var contenedor = document.getElementById('companiasSeleccionadas');
            var seleccion = select.value;

            if (!seleccion) {
                Utilidades.mostrarAlerta('warning', 'Seleccion requerida', 'Por favor, seleccione una compania.');
                return;
            }

            if (state.companias.has(seleccion)) {
                Utilidades.mostrarAlerta('info', 'Ya existe', 'La compania ya ha sido agregada.');
                return;
            }

            state.companias.add(seleccion);

            var badge = document.createElement('span');
            badge.className = 'badge badge-info mr-1 p-2 mb-1';
            badge.innerHTML = '<i class="fas fa-building" aria-hidden="true"></i> ' + seleccion + ' <i class="fas fa-times ml-1" aria-hidden="true"></i>';
            badge.style.cursor = 'pointer';
            badge.title = 'Haz click para eliminar';
            badge.setAttribute('role', 'button');
            badge.setAttribute('tabindex', '0');
            badge.setAttribute('aria-label', 'Eliminar ' + seleccion);

            var eliminar = function() {
                state.companias.delete(seleccion);
                contenedor.removeChild(badge);
            };

            badge.onclick = eliminar;
            badge.onkeypress = function(e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    eliminar();
                }
            };

            contenedor.appendChild(badge);
            select.value = '';
        }
    };

    // ==========================================
    // MODULO: ARCHIVOS
    // ==========================================
    var Archivos = {
        validar: function(inputId) {
            var input = document.getElementById(inputId);
            var archivo = input.files[0];

            if (archivo && archivo.size > config.maxFileSize) {
                Utilidades.mostrarAlerta('warning', 'Archivo muy grande', 'El archivo supera los 2MB permitidos. Por favor suba una captura si aplica.');
                input.value = '';
                return false;
            }
            return true;
        },

        toggleCaptura: function(idArchivo, idCaptura, checkbox) {
            var inputArchivo = document.getElementById(idArchivo);
            var divCaptura = document.getElementById(idCaptura);

            if (checkbox.checked) {
                inputArchivo.value = '';
                inputArchivo.disabled = true;
                inputArchivo.removeAttribute('required');
                divCaptura.style.display = 'block';
            } else {
                inputArchivo.disabled = false;
                inputArchivo.setAttribute('required', 'required');
                divCaptura.style.display = 'none';
            }
        },

        mostrarVistaPrevia: function(inputId, vistaPreviaId) {
            var input = document.getElementById(inputId);
            var vistaPrevia = document.getElementById(vistaPreviaId);

            if (input.files && input.files[0]) {
                var file = input.files[0];
                var url = URL.createObjectURL(file);
                var enlace = vistaPrevia.querySelector('a');

                enlace.href = url;
                enlace.textContent = 'Visualizar archivo: ' + file.name;
                vistaPrevia.style.display = 'block';
            } else {
                vistaPrevia.style.display = 'none';
            }
        }
    };

    // ==========================================
    // MODULO: VALIDACIONES
    // ==========================================
    var Validaciones = {
        concepto: function(idTextarea) {
            var textarea = document.getElementById(idTextarea);
            var alertaId = 'alerta' + idTextarea.charAt(0).toUpperCase() + idTextarea.slice(1);
            var alerta = document.getElementById(alertaId);

            if (alerta) {
                if (textarea.value.trim() === '') {
                    alerta.style.display = 'block';
                    textarea.setAttribute('aria-invalid', 'true');
                    return false;
                } else {
                    alerta.style.display = 'none';
                    textarea.removeAttribute('aria-invalid');
                    return true;
                }
            }
            return true;
        },

        formularioCompleto: function() {
            var errores = [];

            if (!$('#nombreRepresentante').val().trim()) {
                errores.push('Nombre del Representante Legal es requerido');
            }
            if (!$('#direccionEcuador').val().trim()) {
                errores.push('Direccion en Ecuador es requerida');
            }
            if (!$('#nombreCompania').val().trim()) {
                errores.push('Nombre de la Compania es requerido');
            }

            if (errores.length > 0) {
                Utilidades.mostrarAlerta('warning', 'Campos requeridos', errores.join('<br>'));
                return false;
            }
            return true;
        }
    };

    // ==========================================
    // MODULO: UTILIDADES
    // ==========================================
    var Utilidades = {
        detectarDelimitador: function(linea) {
            var puntosComa = (linea.match(/;/g) || []).length;
            var comas = (linea.match(/,/g) || []).length;
            return puntosComa >= comas ? ';' : ',';
        },

        mostrarAlerta: function(tipo, titulo, mensaje) {
            if (typeof Swal !== 'undefined') {
                Swal.fire({
                    icon: tipo,
                    title: titulo,
                    html: mensaje
                });
            } else {
                alert(titulo + ': ' + mensaje);
            }
        },

        confirmar: function(titulo, mensaje, callback) {
            if (typeof Swal !== 'undefined') {
                Swal.fire({
                    title: titulo,
                    text: mensaje,
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Si',
                    cancelButtonText: 'No'
                }).then(function(result) {
                    if (result.isConfirmed) {
                        callback();
                    }
                });
            } else {
                if (confirm(mensaje)) {
                    callback();
                }
            }
        }
    };

    // ==========================================
    // MODULO: CHECKLIST
    // ==========================================
    var Checklist = {
        setBadge: function(id, ok) {
            var el = $('#' + id);
            el.removeClass('badge-secondary badge-success')
              .addClass(ok ? 'badge-success' : 'badge-secondary')
              .text(ok ? 'Completo' : 'Pendiente');
        },

        actualizar: function() {
            this.setBadge('chkFacturaEstado', ($('#archivoFacturaPago').prop('files') || []).length > 0);
            this.setBadge('chk12901Estado', !!($('#carta12901Texto').val() || '').trim());

            var ok12902 = ($('#nombreRepresentante').val() || '').trim() && ($('#direccionEcuador').val() || '').trim();
            this.setBadge('chk12902Estado', !!ok12902);
            this.setBadge('chk12903Estado', $('#tablaAeronaves tbody tr').length > 0);
            this.setBadge('chkAOCEstado', ($('#archivoAOC').prop('files') || []).length > 0);
            this.setBadge('chkOpSpecsEstado', ($('#archivoOpSpecs').prop('files') || []).length > 0);
            this.setBadge('chkManualEstado', ($('#archivoManualOperaciones').prop('files') || []).length > 0);
            this.setBadge('chkMELEstado', ($('#archivoMEL').prop('files') || []).length > 0);
            this.setBadge('chkPermisoEstado', ($('#archivoPermisoOperacion').prop('files') || []).length > 0);
            this.setBadge('chkPlanEstado', ($('#archivoPlanSeguridad').prop('files') || []).length > 0);
            this.setBadge('chkRuidoEstado', ($('#archivoCertificadoRuido').prop('files') || []).length > 0);
        }
    };

    // ==========================================
    // MODULO: CARTA 129-01
    // ==========================================
    var Carta12901 = {
        construir: function() {
            var expl = $('#nombreCompania').val() || '________________';
            var fecha = $('#cartaFecha').val() || '____-__-__';
            var oficio = $('#cartaOficio').val() || '____';
            var director = $('#cartaDirector').val() || 'Director DGAC';
            var aace = $('#cartaAace').val() || 'Autoridad Aeronautica';
            var opspec = $('#cartaOpspecDetalle').val() || 'OpSpecs ___';
            var acuerdo = $('#cartaAcuerdo').val() || '______';
            var acuerdoF = $('#cartaAcuerdoFecha').val() || '____-__-__';
            var rep = $('#cartaRepresentante').val() || $('#nombreRepresentante').val() || 'Representante Legal';

            var txt = 'Quito, ' + fecha + '\n\nOficio: ' + oficio + '\n\nIng. ' + director +
                '\nDIRECCION GENERAL DE AVIACION CIVIL\nPresente\n\n' +
                'Por medio del presente solicito se autorice el otorgamiento del Reconocimiento del Certificado de Explotador de Servicios Aereos (AOCR) a ' +
                expl + ', de conformidad con el Permiso de Operacion emitido por el Consejo Nacional de Aviacion Civil (Acuerdo ' +
                acuerdo + ' de fecha ' + acuerdoF + ') y las OpSpecs vigentes (' + opspec + ') emitidas por ' + aace + '.\n\n' +
                expl + ' designa como Representante Legal y/o Apoderado General en el Ecuador a ' + rep + '.\n\nAtentamente,\n' + rep + '\n' + expl;

            $('#cartaPreview').val(txt);
            $('#carta12901Texto').val(txt);
        },

        copiar: function() {
            var texto = $('#cartaPreview').val() || '';
            if (!texto.trim()) return;

            if (navigator.clipboard) {
                navigator.clipboard.writeText(texto).then(function() {
                    Utilidades.mostrarAlerta('success', 'Copiado', 'Carta copiada al portapapeles');
                });
            } else {
                var ta = document.getElementById('cartaPreview');
                ta.select();
                document.execCommand('copy');
                Utilidades.mostrarAlerta('success', 'Copiado', 'Carta copiada al portapapeles');
            }
        }
    };

    // ==========================================
    // MODULO: FORMULARIO PRINCIPAL
    // ==========================================
    var Formulario = {
        mapearCampos: function() {
            $('#nombreOperador').val($('#nombreCompania').val() || '');
            $('#rucOperador').val($('#rucRepresentante').val() || $('#rucOperador').val());
            $('#telefonoOperador').val($('#telefonoEcuador').val() || '');
            $('#razonSocial').val($('#razonSocial').val() || $('#nombreCompania').val());

            var tipos = [];
            if ($('#opsRegulares').is(':checked')) tipos.push('Ops Regulares');
            if ($('#opsNoRegulares').is(':checked')) tipos.push('Ops No Regulares');
            if ($('#pasajeros').is(':checked')) tipos.push('Pasajeros/Carga/Correo');
            if ($('#carga').is(':checked')) tipos.push('Carga');

            $('#tipoOperacion').val(tipos.join(' | '));
            $('#descripcionOperacion').val($('#resumenOperaciones').val() || '');
            $('#observacionesGenerales').val($('#conceptoFacturaPago').val() || '');
        },

        guardar: function() {
            if (!Validaciones.formularioCompleto()) {
                return;
            }

            this.mapearCampos();

            var token = $('input[name="__RequestVerificationToken"]').val();

            var vm = {
                'Solicitud.CodigoSolicitud': parseInt($('#codigoSolicitud').val() || '0'),
                'Solicitud.NombreOperador': $('#nombreOperador').val(),
                'Solicitud.RepresentanteLegal': $('#nombreRepresentante').val(),
                'Solicitud.CedulaRepresentante': $('#rucRepresentante').val(),
                'Solicitud.Direccion': $('#direccionEcuador').val(),
                'Solicitud.Telefono': $('#telefonoOperador').val(),
                'Solicitud.Ruc': $('#rucOperador').val(),
                'Solicitud.RazonSocial': $('#razonSocial').val(),
                'Solicitud.TipoOperacion': $('#tipoOperacion').val(),
                'Solicitud.DescripcionOperacion': $('#descripcionOperacion').val(),
                'Solicitud.ObservacionesGenerales': $('#observacionesGenerales').val(),
                'Banco': $('#banco').val(),
                'NumeroComprobante': $('#numeroComprobante').val(),
                '__RequestVerificationToken': token
            };

            // Agregar aeronaves
            var aeronaves = Aeronaves.obtenerLista();
            for (var i = 0; i < aeronaves.length; i++) {
                vm['Aeronaves[' + i + '].Fabricante'] = aeronaves[i].Fabricante;
                vm['Aeronaves[' + i + '].Modelo'] = aeronaves[i].Modelo;
                vm['Aeronaves[' + i + '].Serie'] = aeronaves[i].Serie;
                vm['Aeronaves[' + i + '].Matricula'] = aeronaves[i].Matricula;
                vm['Aeronaves[' + i + '].Configuracion'] = aeronaves[i].Configuracion;
                vm['Aeronaves[' + i + '].EtapaRuido'] = aeronaves[i].EtapaRuido;
                vm['Aeronaves[' + i + '].Peso'] = aeronaves[i].Peso;
                vm['Aeronaves[' + i + '].DesignadorOACI'] = aeronaves[i].DesignadorOACI;
            }

            $.ajax({
                url: config.urlGuardar,
                type: 'POST',
                data: vm,
                dataType: 'json',
                timeout: 15000,
                success: function(r) {
                    if (typeof r === 'object' && r !== null) {
                        if (r.success === true) {
                            Utilidades.mostrarAlerta('success', 'Exito', r.mensaje || 'Solicitud guardada correctamente');
                            setTimeout(function() { location.reload(); }, 1500);
                        } else {
                            Utilidades.mostrarAlerta('error', 'Error', r.mensaje || 'Error al guardar la solicitud');
                        }
                    } else {
                        Utilidades.mostrarAlerta('error', 'Error', 'El servidor respondio con un formato invalido.');
                    }
                },
                error: function(xhr, status, error) {
                    var msg = 'No se pudo guardar la solicitud.';

                    if (status === 'timeout') {
                        msg = 'La solicitud tardo demasiado. Intente nuevamente.';
                    } else if (xhr.status === 0) {
                        msg = 'Error de conexion. Verifique su conexion a internet.';
                    } else if (xhr.status === 401) {
                        msg = 'Su sesion ha expirado. Por favor, inicie sesion nuevamente.';
                        setTimeout(function() { location.href = config.urlLogin; }, 2000);
                    } else if (xhr.status === 403) {
                        msg = 'No tiene permisos para realizar esta accion.';
                    } else if (xhr.status === 500) {
                        msg = 'Error interno del servidor. Intente mas tarde.';
                    }

                    Utilidades.mostrarAlerta('error', 'Error', msg);
                    console.error('Error AJAX:', { status: xhr.status, error: error });
                }
            });
        },

        cancelar: function() {
            Utilidades.confirmar(
                'Cancelar formulario',
                'Esta seguro que desea cancelar? Los datos no guardados se perderan.',
                function() {
                    var modal = $('#modalFormularioEmision');
                    if (modal.length > 0 && modal.hasClass('show')) {
                        modal.modal('hide');
                    } else {
                        window.location.href = config.urlIndex;
                    }
                }
            );
        }
    };

    // ==========================================
    // EVENTOS
    // ==========================================
    function inicializarEventos() {
        // Delegacion de eventos para aeronaves
        $(document).on('click', '.btn-eliminar-aeronave', function() {
            Aeronaves.eliminar(this);
        });

        $(document).on('click', '#btnAgregarAeronave', function() {
            $('#formAgregarAeronave')[0].reset();
            $('#modalAgregarAeronave').modal('show');
        });

        $(document).on('click', '#btnGuardarAeronave', function() {
            var datos = Aeronaves.validarFormulario();
            if (datos) {
                Aeronaves.agregar(datos);
                $('#modalAgregarAeronave').modal('hide');
            }
        });

        $(document).on('click', '#btnCargarArchivo', function() {
            $('#inputArchivo').click();
        });

        $(document).on('change', '#inputArchivo', function(e) {
            var archivo = e.target.files[0];
            Aeronaves.cargarCSV(archivo);
            $(this).val('');
        });

        // Eventos para companias
        $(document).on('click', '#btnInformacionCompania', function(e) {
            e.preventDefault();
            $('#modalInformacionCompania').modal('show');
        });

        // Eventos para archivos
        $(document).on('change', '[id^="archivo"]', function() {
            Archivos.validar(this.id);
        });

        // Eventos para checklist
        $(document).on('change input', '#archivoFacturaPago, #nombreRepresentante, #direccionEcuador, #archivoAOC, #archivoOpSpecs, #archivoManualOperaciones, #archivoMEL, #archivoPermisoOperacion, #archivoPlanSeguridad, #archivoCertificadoRuido', function() {
            Checklist.actualizar();
        });

        // Eventos para carta
        $(document).on('input change', '#cartaFecha, #cartaOficio, #cartaDirector, #cartaAace, #cartaOpspecDetalle, #cartaAcuerdo, #cartaAcuerdoFecha, #cartaRepresentante, #nombreCompania', function() {
            Carta12901.construir();
        });

        $(document).on('click', '#btnCopiarCarta', function() {
            Carta12901.copiar();
        });
    }

    // ==========================================
    // INICIALIZACION
    // ==========================================
    function init(opciones) {
        // Configurar URLs
        config.urlGuardar = opciones.urlGuardar || '';
        config.urlIndex = opciones.urlIndex || '';
        config.urlLogin = opciones.urlLogin || '';

        // Inicializar
        $('#contadorAeronaves').val('');
        inicializarEventos();
        Carta12901.construir();
        Checklist.actualizar();
    }

    // ==========================================
    // API PUBLICA
    // ==========================================
    return {
        init: init,
        Aeronaves: Aeronaves,
        Companias: Companias,
        Archivos: Archivos,
        Validaciones: Validaciones,
        Formulario: Formulario,
        Checklist: Checklist,
        Carta12901: Carta12901
    };

})(jQuery);
