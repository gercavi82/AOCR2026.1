<%@ Page Language="C#" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="iTextSharp.text" %>
<%@ Import Namespace="iTextSharp.text.pdf" %>
<%@ Import Namespace="System.IO" %>

<!DOCTYPE html>
<html>
<head>
    <title>iTextSharp Test</title>
</head>
<body>
    <h1>iTextSharp Initialization Test</h1>
    <%
        try {
            Response.Write("<p>Loading Version...</p>");
            var version = iTextSharp.text.Version.GetInstance();
            Response.Write("<p>Version string: " + version.GetVersion + "</p>");
            
            using (var ms = new MemoryStream()) {
                var doc = new Document();
                var writer = PdfWriter.GetInstance(doc, ms);
                doc.Open();
                doc.Add(new Paragraph("Test"));
                doc.Close();
                
                var bytes = ms.ToArray();
                Response.Write("<p>PDF generated. Size: " + bytes.Length + "</p>");
                
                var reader = new PdfReader(bytes);
                using (var ms2 = new MemoryStream()) {
                    var stamper = new PdfStamper(reader, ms2);
                    stamper.Close();
                    Response.Write("<p>PdfStamper created successfully.</p>");
                }
            }
            Response.Write("<p style='color:green;font-weight:bold'>SUCCESS!</p>");
        } catch (Exception ex) {
            Response.Write("<p style='color:red'>ERROR: " + ex.Message + "<br/>" + ex.StackTrace.Replace("\n", "<br/>") + "</p>");
        }
    %>
</body>
</html>
