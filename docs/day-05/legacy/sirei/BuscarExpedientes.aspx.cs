using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI.WebControls;

namespace SIREI
{
    public partial class BuscarExpedientes : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // 🚩 Seguridad "a mano", copiada en cada página
            if (Session["Usuario"] == null) Response.Redirect("~/Login.aspx");

            if (!IsPostBack)
            {
                // 🚩 Session para recordar la última búsqueda entre páginas
                if (Session["BusquedaExpedientes"] != null)
                    txtBuscar.Text = (string)Session["BusquedaExpedientes"];
                CargarExpedientes();
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            gvExpedientes.PageIndex = 0;
            Session["BusquedaExpedientes"] = txtBuscar.Text;
            CargarExpedientes();
        }

        protected void ddlEstado_SelectedIndexChanged(object sender, EventArgs e)
        {
            gvExpedientes.PageIndex = 0;
            CargarExpedientes();
        }

        protected void gvExpedientes_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvExpedientes.PageIndex = e.NewPageIndex;
            CargarExpedientes();
        }

        protected void gvExpedientes_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;

            // 🚩 Lógica de presentación (y de negocio) en el evento de la rejilla: el número 4 es "Cerrado"
            var fila = (DataRowView)e.Row.DataItem;
            switch ((int)fila["IdEstado"])
            {
                case 1: e.Row.Cells[3].Text = "Abierto"; break;
                case 2: e.Row.Cells[3].Text = "En trámite"; break;
                case 3: e.Row.Cells[3].Text = "Suspendido"; e.Row.CssClass = "aviso"; break;
                case 4: e.Row.Cells[3].Text = "Cerrado"; e.Row.CssClass = "gris"; break;
            }
        }

        private void CargarExpedientes()
        {
            // 🚩 Inyección SQL: el texto del usuario se concatena en la consulta
            var sql = "SELECT e.*, (SELECT COUNT(*) FROM Tramites t WHERE t.IdExpediente = e.Id AND t.Pendiente = 1) AS Pendientes " +
                      "FROM Expedientes e WHERE (e.Numero LIKE '%" + txtBuscar.Text + "%' OR e.Titular LIKE '%" + txtBuscar.Text +
                      "%' OR e.Asunto LIKE '%" + txtBuscar.Text + "%')";
            if (ddlEstado.SelectedValue != "")
                sql += " AND e.IdEstado = " + ddlEstado.SelectedValue;
            sql += " ORDER BY e.FechaModificacion DESC";

            // 🚩 SELECT * + DataTable con TODAS las filas; el GridView pagina después, en memoria
            var tabla = new DataTable();
            using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["SIREI"].ConnectionString))
            {
                new SqlDataAdapter(sql, cn).Fill(tabla);
            }

            gvExpedientes.DataSource = tabla;
            gvExpedientes.DataBind();
            lblTotal.Text = tabla.Rows.Count + " expedientes";
        }
    }
}
