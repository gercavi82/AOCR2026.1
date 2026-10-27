// Compatibility adapter. Both forms use the same location/date collector.
(function ($, window) {
    window.AocrInspectionDatesFormIntegration = {
        prepareInspectionDatesForSubmission: function () {
            $('.lugar-inspeccion-check').closest('form').each(function () {
                window.AocrInspectionDateManager.prepareSubmission($(this));
            });
        }
    };
}(jQuery, window));
