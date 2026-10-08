using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI.WebControls;

namespace SIREI
{
    public partial class ExpedienteDetalle : System.Web.UI.Page
    {
        // 🚩 Cadena de conexión leída con ConfigurationManager en cada página
        private static string Cadena => ConfigurationManager.ConnectionStrings["SIREI"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["Usuario"] == null) Response.Redirect("~/Login.aspx");

            if (!IsPostBack)
            {
                var id = int.Parse(Request.QueryString["id"]);   // 🚩 sin comprobar: ?id=abc → excepción (página amarilla)
                ViewState["IdExpediente"] = id;                   // 🚩 el id viaja en el ViewState
                CargarExpediente(id);
            }
        }

        private void CargarExpediente(int id)
        {
            var ds = new DataSet();
            using (var cn = new SqlConnection(Cadena))
            {
                // 🚩 SELECT * y concatenación
                var da = new SqlDataAdapter("SELECT * FROM Expedientes WHERE Id = " + id, cn);
                da.Fill(ds, "Expediente");
                da.SelectCommand.CommandText = "SELECT * FROM Tramites WHERE IdExpediente = " + id + " ORDER BY FechaAlta";
                da.Fill(ds, "Tramites");
            }
            Session["ExpedienteActual"] = ds;   // 🚩 un DataSet entero en Session (memoria del servidor, por usuario)

            var fila = ds.Tables["Expediente"].Rows[0];
            lblNumero.Text = fila["Numero"].ToString();
            lblTitular.Text = fila["Titular"].ToString();
            lblAsunto.Text = fila["Asunto"].ToString();
            txtObservaciones.Text = fila["Observaciones"].ToString();
            ddlEstado.SelectedValue = fila["IdEstado"].ToString();

            // 🚩 La regla "cerrado = solo lectura" solo existe en la pantalla
            var cerrado = (int)fila["IdEstado"] == 4;
            btnGuardar.Visible = !cerrado;
            btnAgregarTramite.Visible = !cerrado;

            gvTramites.DataSource = ds.Tables["Tramites"];
            gvTramites.DataBind();
        }

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            var ds = (DataSet)Session["ExpedienteActual"];   // 🚩 si la sesión ha caducado: NullReferenceException
            var fila = ds.Tables["Expediente"].Rows[0];

            // 🚩 REGLA DE NEGOCIO escondida en el evento del botón, con un número mágico
            if (ddlEstado.SelectedValue == "4" && ds.Tables["Tramites"].Select("Pendiente = 1").Length > 0)
            {
                lblError.Text = "No se puede cerrar un expediente con trámites pendientes.";
                return;
            }

            fila["Observaciones"] = txtObservaciones.Text;
            fila["IdEstado"] = int.Parse(ddlEstado.SelectedValue);
            fila["FechaModificacion"] = DateTime.Now;   // 🚩 hora local del servidor

            // 🚩 "El último que guarda gana": el DataSet se cargó hace minutos y nadie comprueba si otro lo cambió
            using (var cn = new SqlConnection(Cadena))
            {
                var da = new SqlDataAdapter("SELECT * FROM Expedientes WHERE Id = " + ViewState["IdExpediente"], cn);
                new SqlCommandBuilder(da);
                da.Update(ds, "Expediente");
            }
            lblMensaje.Text = "Guardado";
        }

        protected void gvTramites_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "Completar") return;

            var indice = Convert.ToInt32(e.CommandArgument);
            var idTramite = (int)gvTramites.DataKeys[indice].Value;

            // 🚩 Nadie comprueba que el trámite sea de ESTE expediente ni que el expediente no esté cerrado
            using (var cn = new SqlConnection(Cadena))
            using (var cmd = new SqlCommand("UPDATE Tramites SET Pendiente = 0, FechaCompletado = GETDATE() WHERE Id = " + idTramite, cn))
            {
                cn.Open();
                cmd.ExecuteNonQuery();
            }
            CargarExpediente((int)ViewState["IdExpediente"]);
        }

        protected void btnAgregarTramite_Click(object sender, EventArgs e)
        {
            if (txtNuevoTramite.Text.Trim() == "") return;

            using (var cn = new SqlConnection(Cadena))
            using (var cmd = new SqlCommand(
                "INSERT INTO Tramites (IdExpediente, Descripcion, FechaAlta, Pendiente) VALUES (" + ViewState["IdExpediente"] +
                ", '" + txtNuevoTramite.Text + "', GETDATE(), 1)", cn))   // 🚩 inyección SQL (y falla con un apóstrofo: "D'Ors")
            {
                cn.Open();
                cmd.ExecuteNonQuery();
            }
            txtNuevoTramite.Text = "";
            CargarExpediente((int)ViewState["IdExpediente"]);
        }
    }
}
