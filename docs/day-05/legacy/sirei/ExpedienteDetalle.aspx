<%@ Page Title="Expediente" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="ExpedienteDetalle.aspx.cs" Inherits="SIREI.ExpedienteDetalle" %>

<asp:Content ID="Contenido" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Expediente <asp:Label ID="lblNumero" runat="server" /></h2>

    <table class="ficha">
        <tr><td>Titular</td><td><asp:Label ID="lblTitular" runat="server" /></td></tr>
        <tr><td>Asunto</td><td><asp:Label ID="lblAsunto" runat="server" /></td></tr>
        <tr>
            <td>Estado</td>
            <td>
                <asp:DropDownList ID="ddlEstado" runat="server">
                    <asp:ListItem Value="1" Text="Abierto" />
                    <asp:ListItem Value="2" Text="En trámite" />
                    <asp:ListItem Value="3" Text="Suspendido" />
                    <asp:ListItem Value="4" Text="Cerrado" />
                </asp:DropDownList>
            </td>
        </tr>
        <tr>
            <td>Observaciones</td>
            <td><asp:TextBox ID="txtObservaciones" runat="server" TextMode="MultiLine" Rows="4" Columns="60" /></td>
        </tr>
    </table>

    <asp:Label ID="lblError" runat="server" CssClass="error" EnableViewState="false" />
    <asp:Label ID="lblMensaje" runat="server" CssClass="ok" EnableViewState="false" />

    <%-- 🚩 Confirmación con JavaScript inline (OnClientClick): una CSP estricta lo bloquea --%>
    <asp:Button ID="btnGuardar" runat="server" Text="Guardar" OnClick="btnGuardar_Click"
                OnClientClick="return confirm('¿Guardar los cambios?');" />

    <h3>Trámites</h3>
    <asp:GridView ID="gvTramites" runat="server" AutoGenerateColumns="false" DataKeyNames="Id"
                  OnRowCommand="gvTramites_RowCommand" EmptyDataText="El expediente no tiene trámites.">
        <Columns>
            <asp:BoundField DataField="Descripcion" HeaderText="Trámite" />
            <asp:BoundField DataField="FechaAlta" HeaderText="Alta" DataFormatString="{0:dd/MM/yyyy}" />
            <asp:CheckBoxField DataField="Pendiente" HeaderText="Pendiente" />
            <asp:ButtonField CommandName="Completar" Text="Completar" ButtonType="Link" />
        </Columns>
    </asp:GridView>

    Nuevo trámite: <asp:TextBox ID="txtNuevoTramite" runat="server" />
    <asp:Button ID="btnAgregarTramite" runat="server" Text="Añadir" OnClick="btnAgregarTramite_Click" />

    <p><a href="BuscarExpedientes.aspx">Volver a la búsqueda</a></p>
</asp:Content>
