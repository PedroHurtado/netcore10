<%@ Page Title="Búsqueda de expedientes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="BuscarExpedientes.aspx.cs" Inherits="SIREI.BuscarExpedientes" %>

<asp:Content ID="Contenido" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Búsqueda de expedientes</h2>

    <asp:Panel ID="pnlBusqueda" runat="server" DefaultButton="btnBuscar">
        Buscar: <asp:TextBox ID="txtBuscar" runat="server" Width="250px" />
        Estado:
        <asp:DropDownList ID="ddlEstado" runat="server" AutoPostBack="true"
                          OnSelectedIndexChanged="ddlEstado_SelectedIndexChanged">
            <asp:ListItem Value="" Text="(todos)" />
            <asp:ListItem Value="1" Text="Abierto" />
            <asp:ListItem Value="2" Text="En trámite" />
            <asp:ListItem Value="3" Text="Suspendido" />
            <asp:ListItem Value="4" Text="Cerrado" />
        </asp:DropDownList>
        <asp:Button ID="btnBuscar" runat="server" Text="Buscar" OnClick="btnBuscar_Click" />
    </asp:Panel>

    <%-- 🚩 AllowPaging en el GridView: pagina EN MEMORIA, después de traer todas las filas --%>
    <asp:GridView ID="gvExpedientes" runat="server" AutoGenerateColumns="false" AllowPaging="true" PageSize="20"
                  OnPageIndexChanging="gvExpedientes_PageIndexChanging" OnRowDataBound="gvExpedientes_RowDataBound"
                  CssClass="rejilla" EmptyDataText="No hay expedientes.">
        <Columns>
            <asp:HyperLinkField DataTextField="Numero" HeaderText="Número"
                                DataNavigateUrlFields="Id" DataNavigateUrlFormatString="ExpedienteDetalle.aspx?id={0}" />
            <asp:BoundField DataField="Titular" HeaderText="Titular" />
            <asp:BoundField DataField="Asunto" HeaderText="Asunto" />
            <asp:BoundField DataField="IdEstado" HeaderText="Estado" />
            <asp:BoundField DataField="Pendientes" HeaderText="Trám. pend." />
            <asp:BoundField DataField="FechaModificacion" HeaderText="Modificado" DataFormatString="{0:dd/MM/yyyy}" />
        </Columns>
    </asp:GridView>

    <asp:Label ID="lblTotal" runat="server" CssClass="nota" />
</asp:Content>
