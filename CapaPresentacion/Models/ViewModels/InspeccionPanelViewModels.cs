using CapaModelo;

namespace CapaPresentacion.Models.ViewModels
{
    public class FirmaInspectorPanelVm
    {
        public int CodigoInspeccion { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }
        public bool PuedeFirmarInspector { get; set; }
        public bool PuedeEnviarADirdac { get; set; }
        public bool PuedeReintentarNotificacionDirdac { get; set; }
        public bool InformeEnviadoADirdac { get; set; }
        public bool InformeDevueltoCoordinador { get; set; }
        public bool InformeDevueltoDireccion { get; set; }
        public bool InformeAprobadoDireccion { get; set; }
        public string EstadoInformeTecnico { get; set; }

        public FirmaInspectorPanelVm()
        {
            InformeTecnico = new InspeccionInformeTecnico();
            EstadoInformeTecnico = "BORRADOR";
        }
    }

    public class CoordinadorPanelVm
    {
        public int CodigoInspeccion { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }
        public bool EsCoordinador { get; set; }
        public string EstadoInformeTecnico { get; set; }
        public string RutaInformeVisual { get; set; }

        public CoordinadorPanelVm()
        {
            InformeTecnico = new InspeccionInformeTecnico();
            EstadoInformeTecnico = "BORRADOR";
            RutaInformeVisual = string.Empty;
        }
    }

    public class DireccionJefaturaPanelVm
    {
        public int CodigoInspeccion { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }
        public bool EsDirdac { get; set; }
        public bool PuedeFirmarDirdac { get; set; }
        public bool InformeEnviadoADirdac { get; set; }
        public bool InformeAprobadoDireccion { get; set; }
        public bool InformeDevueltoDireccion { get; set; }
        public string EstadoInformeTecnico { get; set; }
        public string RutaInformeVisual { get; set; }

        public DireccionJefaturaPanelVm()
        {
            InformeTecnico = new InspeccionInformeTecnico();
            EstadoInformeTecnico = "BORRADOR";
            RutaInformeVisual = string.Empty;
        }
    }
}
