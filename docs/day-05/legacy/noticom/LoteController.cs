using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
using Noticom.Web.Models;
using Noticom.Web.Services;

namespace Noticom.Web.Controllers
{
    // 🚩 Sin [Authorize]: cada acción comprueba Session["Usuario"] a mano (y alguna se olvida)
    public class LoteController : Controller
    {
        // 🚩 Dependencia creada a mano (sin inyección): imposible sustituirla en una prueba
        private readonly LoteService _servicio = new LoteService();

        // GET /Lote/Index?t=c   (c = consulta, v = validar, cr = crear remesa, b = borrar)
        public ActionResult Index(string t)
        {
            if (Session["Usuario"] == null) return RedirectToAction("Index", "Login");

            var modelo = new LoteModel
            {
                TipoEjecucion = t ?? "c",   // 🚩 string mágico que viaja a la vista y al JavaScript
                Proceso = (int)(Session["ProcesoActual"] ?? 120),   // 🚩 estado en Session
                Ejercicio = DateTime.Now.Year.ToString()
            };
            return View(modelo);
        }

        // POST /Lote/ConsultaLotes   ← lo llama $.ajax desde la vista y devuelve la tabla como HTML
        // 🚩 POST para LEER: no se puede enlazar, ni cachear, ni volver atrás
        [HttpPost]
        public ActionResult ConsultaLotes(int? proceso, string ejercicio, string estado, string pago, string tipo,
                                          string tipoEjecucion, string remesa)
        {
            // 🚩 Si la sesión caducó, se devuelve la vista de login con código 200; el JavaScript busca "_Logon_" en el HTML
            if (Session["Usuario"] == null) return PartialView("_Logon_");

            var lotes = _servicio.Consultar(proceso, ejercicio, estado, pago, tipo, remesa, tipoEjecucion);
            return PartialView("_GridLotes", lotes);
        }

        // POST /Lote/Validar   (desde modalConfirmacion)
        [HttpPost]
        public JsonResult Validar(int idLote)
        {
            // 🚩 Sin [ValidateAntiForgeryToken]: vulnerable a CSRF
            // 🚩 Sin comprobar el rol: cualquiera que conozca la URL valida lotes
            _servicio.Validar(idLote, (string)Session["Usuario"]);
            return Json(new { Ok = true });   // 🚩 PascalCase (Newtonsoft); en ASP.NET Core sería "ok"
        }

        [HttpPost]
        public JsonResult Borrar(int idLote)
        {
            // 🚩 El servidor no comprueba que el lote esté pendiente: "lo garantiza" que la columna Borrar
            //    solo se ve en la pantalla "b"
            _servicio.Borrar(idLote);
            return Json(new { Ok = true });
        }

        // POST /Lote/CrearRemesa   lista = "[{\"Id\":\"12\"},{\"Id\":\"15\"}]"
        [HttpPost]
        public JsonResult CrearRemesa(string lista, string nombreRemesa)
        {
            // 🚩 Se deserializa a mano un JSON montado en el navegador recorriendo las celdas de la tabla
            var ids = JsonConvert.DeserializeObject<List<LoteSeleccionado>>(lista).Select(l => int.Parse(l.Id)).ToList();
            var idRemesa = _servicio.CrearRemesa(nombreRemesa, ids);   // 🚩 sin validar el nombre en el servidor
            return Json(new { Ok = true, IdRemesa = idRemesa });
        }
    }

    public class LoteSeleccionado
    {
        public string Id { get; set; }
    }
}
