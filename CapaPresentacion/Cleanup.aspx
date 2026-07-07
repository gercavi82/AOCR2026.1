<%@ Page Language="C#" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="Npgsql" %>
<%@ Import Namespace="System.Configuration" %>

<!DOCTYPE html>
<html>
<head><title>Cleanup DB</title></head>
<body>
<%
    try {
        var connStr = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString 
                   ?? ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString;
                   
        using (var conn = new NpgsqlConnection(connStr)) {
            conn.Open();
            int facturas = 0, pagos = 0;
            using (var cmd = new NpgsqlCommand("DELETE FROM aocr_tb_factura_pago WHERE orden_id NOT IN (SELECT id FROM aocr_or_orden);", conn)) {
                facturas = cmd.ExecuteNonQuery();
            }
            using (var cmd = new NpgsqlCommand("DELETE FROM aocr_tbpago WHERE codigo_solicitud NOT IN (SELECT id FROM aocr_or_orden);", conn)) {
                pagos = cmd.ExecuteNonQuery();
            }
            Response.Write("<h1>SUCCESS</h1>");
            Response.Write("<p>Deleted " + facturas + " orphaned records from aocr_tb_factura_pago.</p>");
            Response.Write("<p>Deleted " + pagos + " orphaned records from aocr_tbpago.</p>");
        }
    } catch (Exception ex) {
        Response.Write("<h1>ERROR</h1>");
        Response.Write("<p>" + ex.Message + "</p>");
        Response.Write("<pre>" + ex.StackTrace + "</pre>");
    }
%>
</body>
</html>
