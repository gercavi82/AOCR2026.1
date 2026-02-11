(function(window){
    'use strict';
    window.AOCR = window.AOCR || {};

    // Escapa texto para incluirlo en HTML (contenido) de forma segura
    window.AOCR.escapeHtml = function(text){
        if (text === null || text === undefined) return '';
        return String(text).replace(/[&<>"']/g, function (m) { return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]; });
    };

    // Escapa texto para atributos (añade comillas escapadas)
    window.AOCR.escapeAttr = function(text){
        return window.AOCR.escapeHtml(text).replace(/"/g, '&quot;');
    };

    // Crea una opción de select de forma segura
    window.AOCR.createOption = function(value, text){
        var $opt = $('<option>').val(value).text(text);
        return $opt;
    };

})(window);
