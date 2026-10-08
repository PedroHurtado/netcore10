$(document).ready(function () {

    //Gridview de lotes
    CargarGridViewLotes();
});

//Configuración publicada por el servidor
var config = JSON.parse(document.getElementById('lotes-config').textContent);

//Recogemos el tipo de ejecución
var tipoEjecucion = config.tipoEjecucion;

//Final ajax
$(document).ajaxStop(function () {

    //Dependiendo del tipo de ejecución, ocultamos las columnas
    if (tipoEjecucion == "v") {

        //Validar
        ocultarColumnaGridview(1, 'gridLotes');
        ocultarColumnaGridview(11, 'gridLotes');
        ocultarColumnaGridview(12, 'gridLotes');
        ocultarColumnaGridview(15, 'gridLotes');
    }
    if (tipoEjecucion == "c") {
        //Consultar
        ocultarColumnaGridview(1, 'gridLotes');
        ocultarColumnaGridview(14, 'gridLotes');
        ocultarColumnaGridview(15, 'gridLotes');
    }
    if (tipoEjecucion == "cr") {

        //Crear remesa
        ocultarColumnaGridview(12, 'gridLotes');
        ocultarColumnaGridview(14, 'gridLotes');
        ocultarColumnaGridview(15, 'gridLotes');
    }
    if (tipoEjecucion == "b") {

        //Borrar lote
        ocultarColumnaGridview(1, 'gridLotes');
        ocultarColumnaGridview(12, 'gridLotes');
        ocultarColumnaGridview(14, 'gridLotes');
    }
});


//*********************************Eventos************************************
//****************************************************************************

//Evento botón búsqueda avanzada
$('#BtnBusquedaAvanzada').click(function () {

    //Recuperamos los datos de la búsqueda
    var proceso = $("#Proceso").val();
    var ejercicio = $("#Ejercicio").val();

    //Activamos la búsqueda avanzada
    busquedaAvanzada();

    //Igualamos los datos de la búsqueda
    $("#ProcesoAvanzada").val(proceso);
    $("#EjercicioAvanzada").val(ejercicio);
});

//Evento botón búsqueda lotes
$('#BtnBuscarLotes').click(function () {

    //Variables
    var proceso = $("#Proceso").val();
    var ejercicio = $("#Ejercicio").val();
    var remesa = $("#Nombreremesa").val();

    //Ejecutamos la consulta filtrada.
    consultaLotes(proceso, ejercicio, '', '', '', remesa);
});

//Evento botón búsqueda avanzada remesas
$('#BtnBuscarAvanzadaLotes').click(function () {

    //Variables
    var proceso = $("#ProcesoAvanzada").val();
    var ejercicio = $("#EjercicioAvanzada").val();
    var estado = $("#Estado").val();
    var pago = $("#Pago").val();
    var tipo = $("#Tipo").val();
    var remesa = $("#NombreremesaAvanzada").val();

    //Ejecutamos la consulta filtrada.
    consultaLotes(proceso, ejercicio, estado, pago, tipo, remesa);
});

//Evento botón crear remesa
$('#BtnCrearRemesas').click(function () {
    //Creamos la remesa de los lotes seleccionados
    crearRemesa();
});


//*********************************Funciones**********************************
//****************************************************************************

 //Función para actualizar el gridview de lotes
function CargarGridViewLotes() {
    consultaLotes(config.proceso, config.ejercicio, '', '', '', '');
}

//Función consultar lotes
function consultaLotes(_proceso, _ejercicio, _estado, _pago, _tipo, _remesa) {

    //Visualizamos el loading
    $("#loading").show();

    //Verificamos si ejecutamos el filtro anterior
    if (sessionStorage.getItem("EjecutarFiltroLotes") != null) {

        //Recogemos filtro anterior
        var filtroAnterior = JSON.parse(sessionStorage["FiltroLotes"]);

        //Igualamos al filtro anterior
        _proceso = filtroAnterior.proceso;
        _ejercicio = filtroAnterior.ejercicio;
        _estado = filtroAnterior.estado;
        _pago = filtroAnterior.pago;
        _tipo = filtroAnterior.tipo;
        _remesa = filtroAnterior.remesa;

        //Verificamos si activamos la búsqueda avanzada
        if (_estado != '' || _pago != '' || _tipo != '') {

            //Activamos búsqueda avanzada
            busquedaAvanzada();

            //Igualamos los datos de la búsqueda avanzada
            $("#ProcesoAvanzada").val(_proceso);
            $("#EjercicioAvanzada").val(_ejercicio);
            $("#Estado").val(_estado);
            $("#Pago").val(_pago);
            $("#Tipo").val(_tipo);
            $("#NombreremesaAvanzada").val(_remesa);
        }
        else {
            //Igualamos los datos de la búsqueda
            $("#Proceso").val(_proceso);
            $("#Ejercicio").val(_ejercicio);
            $("#Nombreremesa").val(_remesa);
        }

        //Limpiamos el sessionStorage
        sessionStorage.removeItem("EjecutarFiltroLotes");
    }

    //Guardamos las variables del filtro
    var filtro = { proceso: _proceso, ejercicio: _ejercicio, estado: _estado, pago: _pago, tipo: _tipo, remesa: _remesa };

    //Guardamos el filtro en una variable de sesión Storage
    sessionStorage["FiltroLotes"] = JSON.stringify(filtro);

    //Recogemos el tipo de ejecución
    var _tipoEjecucion = tipoEjecucion;

    $.ajax({
        url: urlpagina + '/Lote/ConsultaLotes',
        type: 'POST',
        cache: false,
        dataType: "html",
        data: { proceso: _proceso, ejercicio: _ejercicio, estado: _estado, pago: _pago, tipo: _tipo, tipoEjecucion: _tipoEjecucion, remesa: _remesa },
        success: function (result) {
            if (result != null && result != '') {
                //Generamos el gridview de remesas
                $('#GridviewLotes').html(result);

                //Verificamos si el gridview ha devuelto datos
                if ($('#GridviewLotes tr').length == 1) {
                    //Mostramos sin datos
                    $("#InformacionGridviewVacio").show();

                    //Tipo de ejecución crear remesa
                    if (tipoEjecucion == "cr") {
                        //Ocultamos el botón crear remesa
                        $("#BtnCrearRemesas").hide();

                        //Ocultamos el textbox del nombre remesa
                        $("#Nombreremesa").hide();
                        $("#lblNombreremesa").hide();
                    }
                } else {
                    //Ocultamos sin datos
                    $("#InformacionGridviewVacio").hide();
                }

                //Ocultamos loader
                $("#loading").hide();
            } else {
                //Mostramos el error general
                mostrarTextoError('- Error al mostrar los lotes. Por favor, póngase en contacto con el administrador.');

                //Ocultamos loader
                $("#loading").hide();
            }
        },
        statusCode: {
            200: function (result) {

                //Perder sesión
                if (result.indexOf("_Logon_") > 0) {
                    //Login
                    window.location.href = urlpagina + '/Login';
                }
            }
        }
    });
}

//Ocultamos una columna del gridview
function ocultarColumnaGridview(idColumna, idGridView) {

    //Ocultamos la columna
    $("#" + idGridView + " th:nth-child(" + idColumna + ")").hide();
    $("#" + idGridView + " td:nth-child(" + idColumna + ")").hide();
}

//Función para activar la búsqueda avanzada
function busquedaAvanzada() {

    //Ocultamos la búsqueda
    $("#Busqueda").hide();

    //Visualizamos la búsqueda avanzada
    $("#BusquedaAvanzada").show();
}

//Función para crear una remesa de los lotes seleccionados
function crearRemesa() {

    //Variables
    var list = [];
    var seleccionados = 0;
    var texto = '';
    var _Id = 0;
    var nombreRemesa = "";

    //Validamos el nombre de la remesa
    if ($('#Nombreremesa').val() == '') {
        //Mostramos el error general
        mostrarTextoError('- El nombre de la remesa es obligatorio.');

        return;
    }

    //Recorremos el contenido del gridview
    $("#gridLotes td:nth-child(2)").each(function () {

        //Valor del id del lote
        _Id = $(this).text();

        //Verificamos si el registro esta seleccionado
        if ($('#chkBoxSel_' + _Id).is(":checked")) {

            //Sumamos un seleccionado
            seleccionados = seleccionados + 1;

            //Añadimos al listado
            list.push({ Id: _Id });
        }
    });

    //Controlamos si se ha seleccionado algún lote
    if (seleccionados == 0) {
        //Mostramos el error general
        mostrarTextoError('- No se ha seleccionado ningún lote.');
    } else {

        //Texto lotes
        if (seleccionados == 1) {
            texto = seleccionados + ' lote'
        } else {
            texto = seleccionados + ' lotes'
        }

        //Recogemos el nombre de la remesa
        nombreRemesa = $('#Nombreremesa').val();

        //Confirmamos la validación
        modalConfirmacion('Crear remesa', 'Se va a crear una remesa a partir de ' + texto, tipoEjecucion, 0, list, nombreRemesa, 0, 0);
    }
}

//Llamada a ver documentos asociados al lote
function verDocumentos(idLote) {

    //Visualizamos el loading
    $("#loading").show();

    //Encriptamos la url y la llamamos
    encriptarURL('Notificacion', 'Index', 't=' + tipoEjecucion + '&l=' + idLote + '&tc=cl', false);
}

//Función para borrar un lote
function borrarLote(idLote) {
    //Confirmamos la validación
    modalConfirmacion('Borrar lote', 'Se va a borrar el lote', tipoEjecucion, idLote, null, '', 0, 0);
}
