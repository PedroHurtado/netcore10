# Código legacy de partida

Fragmentos **representativos** de las dos aplicaciones del caso práctico. No se compilan: son el punto de partida que se analiza y se migra en los laboratorios. Si tenéis acceso al código real de SIREI y Noticom, usadlo en su lugar: los patrones son los mismos.

| Carpeta | Aplicación | Tecnología | Pantallas | Se migra en |
|---|---|---|---|---|
| [sirei/](sirei/) | SIREI | ASP.NET Web Forms 4.8 + ADO.NET (`DataSet`) | `BuscarExpedientes.aspx`, `ExpedienteDetalle.aspx` | [Lab 1](../labs/lab-01-sirei-expedientes.md) → `src/day-05/GestorIncidencias.Web/Areas/Sirei` |
| [noticom/](noticom/) | Noticom | ASP.NET MVC 5 + EF6 (EDMX) + jQuery | `Lote/Index?t=c\|v\|cr\|b` (consulta, validación, crear remesa, borrado) | [Lab 2](../labs/lab-02-noticom-lotes.md) → `src/day-05/GestorIncidencias.Web/Areas/Noticom` |

La vista de Noticom está basada en la vista real de lotes ([examples/prueba.txt](../../../examples/prueba.txt)), recortada para el curso.

Al leer cada fichero, buscad las señales de alarma del [capítulo 7.6 del día 3](../../day-03/07-analisis-legacy.md#76-señales-de-alarma-en-un-análisis): están todas marcadas con un comentario `// 🚩`.
