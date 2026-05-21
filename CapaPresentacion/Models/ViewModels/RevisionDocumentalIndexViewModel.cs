using System;
using System.Collections.Generic;

namespace CapaPresentacion.Models.ViewModels
{
    public class RevisionDocumentalIndexViewModel
    {
        public RevisionDocumentalIndexViewModel()
        {
            Solicitudes = new List<RevisionDocumentalSolicitudRowViewModel>();
        }

        public IList<RevisionDocumentalSolicitudRowViewModel> Solicitudes { get; set; }
        public int TotalSolicitudesPendientes { get; set; }
        public int TotalDocumentosPendientes { get; set; }
        public int TotalSolicitudesEnRevision { get; set; }
        public int TotalDocumentosObservados { get; set; }
        public int TotalDocumentosAceptados { get; set; }
        public int TotalDocumentosSubsanados { get; set; }
    }

    public class RevisionDocumentalSolicitudRowViewModel
    {
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Operadora { get; set; }
        public string Responsable { get; set; }
        public string EstadoSolicitud { get; set; }
        public string EstadoDocumentalCodigo { get; set; }
        public string EstadoDocumentalNombre { get; set; }
        public string EstadoDocumentalDetalle { get; set; }
        public DateTime? FechaCargaDocumentos { get; set; }
        public int DocumentosCargados { get; set; }
        public int DocumentosPendientes { get; set; }
        public int DocumentosObservados { get; set; }
        public int DocumentosAceptados { get; set; }
        public int DocumentosSubsanados { get; set; }
        public bool TieneDocumentosCargados { get; set; }
    }
}