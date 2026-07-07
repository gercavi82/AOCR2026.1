<%@ Page Language="C#" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="iTextSharp.text" %>
<%@ Import Namespace="iTextSharp.text.pdf" %>
<%@ Import Namespace="System.IO" %>

<!DOCTYPE html>
<html>
<head>
    <title>iTextSharp NRE Test</title>
</head>
<body>
    <h1>iTextSharp NRE Test</h1>
    <pre>
<%
    try {
        var v = iTextSharp.text.Version.GetInstance();
        Response.Write("Version GetInstance() succeeded: " + v.GetVersion + "\n");
        
        using (var output = new MemoryStream()) {
            var doc = new Document();
            var writer = PdfWriter.GetInstance(doc, output);
            doc.Open();
            doc.Add(new Paragraph("Hello"));
            doc.Close();
            Response.Write("PdfWriter succeeded!\n");
        }
    } catch (Exception ex) {
        Response.Write("EXCEPTION THROWN:\n");
        Response.Write("Type: " + ex.GetType().FullName + "\n");
        Response.Write("Message: " + ex.Message + "\n");
        Response.Write("StackTrace: \n" + ex.StackTrace + "\n");
        if (ex.InnerException != null) {
            Response.Write("\nINNER EXCEPTION:\n");
            Response.Write("Type: " + ex.InnerException.GetType().FullName + "\n");
            Response.Write("Message: " + ex.InnerException.Message + "\n");
            Response.Write("StackTrace: \n" + ex.InnerException.StackTrace + "\n");
        }
    }
%>
    </pre>
</body>
</html>
