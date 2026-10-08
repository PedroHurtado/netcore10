using System;
using System.Collections.Generic;
using System.Linq;
using Noticom.Web.Data;   // NoticomEntities: contexto EF6 generado desde el modelo EDMX

namespace Noticom.Web.Services
{
    public class LoteService
    {
        public List<Lote> Consultar(int? proceso, string ejercicio, string estado, string pago, string tipo,
                                    string remesa, string tipoEjecucion)
        {
            using (var db = new NoticomEntities())
            {
                // 🚩 ToList() ANTES de filtrar: se trae la tabla Lotes entera y se filtra en memoria
                IEnumerable<Lote> lotes = db.Lotes.Include("Remesa").ToList();

                if (proceso.HasValue) lotes = lotes.Where(l => l.Proceso == proceso);
                if (!string.IsNullOrEmpty(ejercicio)) lotes = lotes.Where(l => l.Ejercicio.ToString() == ejercicio);
                if (!string.IsNullOrEmpty(estado)) lotes = lotes.Where(l => l.IdEstado.ToString() == estado);
                if (!string.IsNullOrEmpty(tipo)) lotes = lotes.Where(l => l.Tipo == tipo);
                if (!string.IsNullOrEmpty(remesa)) lotes = lotes.Where(l => l.Remesa != null && l.Remesa.Nombre.Contains(remesa));

                // 🚩 El tipo de PANTALLA decide qué datos se leen, dentro del servicio
                if (tipoEjecucion == "v" || tipoEjecucion == "b") lotes = lotes.Where(l => l.IdEstado == 1);
                if (tipoEjecucion == "cr") lotes = lotes.Where(l => l.IdEstado == 2);

                return lotes.OrderByDescending(l => l.Ejercicio).ToList();   // 🚩 sin paginación
            }
        }

        public void Validar(int idLote, string usuario)
        {
            using (var db = new NoticomEntities())
            {
                var lote = db.Lotes.Find(idLote);
                lote.IdEstado = 2;                    // 🚩 sin comprobar el estado anterior
                lote.FechaValidacion = DateTime.Now;
                lote.UsuarioValidacion = usuario;
                db.SaveChanges();
            }
        }

        public void Borrar(int idLote)
        {
            using (var db = new NoticomEntities())
            {
                db.Lotes.Remove(db.Lotes.Find(idLote));
                db.SaveChanges();
            }
        }

        public int CrearRemesa(string nombre, List<int> ids)
        {
            using (var db = new NoticomEntities())
            {
                var remesa = new Remesa { Nombre = nombre, FechaCreacion = DateTime.Now };
                db.Remesas.Add(remesa);
                db.SaveChanges();

                foreach (var id in ids)
                {
                    var lote = db.Lotes.Find(id);   // 🚩 una consulta por lote (N+1)
                    lote.IdRemesa = remesa.Id;       // 🚩 sin comprobar que esté validado ni que no tenga ya remesa
                    lote.IdEstado = 3;
                    db.SaveChanges();                // 🚩 un SaveChanges por lote: si falla a mitad, remesa a medias
                }
                return remesa.Id;
            }
        }
    }
}
