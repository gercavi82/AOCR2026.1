$(function () {
    $('#frmVerificarPago').on('submit', function (e) {
        e.preventDefault();
        const numero = $('#txtNumeroSolicitud').val().trim();
        if (!numero) return alert('Debe ingresar un número de solicitud');

        $.get('/VerificarPago/Buscar', { id: numero })
            .done(function (resp) {
                var $ok = $('<div>').addClass('alert alert-success').text(resp.mensaje || '');
                $('#resultado').empty().append($ok);
            })
            .fail(function () {
                $('#resultado').html(`<div class="alert alert-danger">Error al verificar el pago</div>`);
            });
    });
});
