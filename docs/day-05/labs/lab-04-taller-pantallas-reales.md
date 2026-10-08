# Lab 4 — Taller: plan de migración de una pantalla real

**Duración:** 20 min (grupos de 2 o 3) + puesta en común · **Teoría relacionada:** [01 — Método](../01-metodo-migracion.md), [05 — Cierre](../05-cierre-curso.md#52-lista-de-comprobación-para-migrar-cualquier-pantalla)

## Objetivo

Salir del curso con el **plan de migración de una pantalla real** de SIREI o de Noticom, hecho con la receta de hoy. No se escribe código: se escribe la ficha que guiaría a quien la migre (quizá vosotros, la semana que viene).

Cada grupo elige **una** pantalla real distinta de las migradas hoy (el formador puede repartirlas). Si no tenéis acceso al código, usad una pantalla que conozcáis bien de memoria.

## Paso 1 — Ficha de la pantalla (8 min)

| Apartado | Vuestra pantalla |
|---|---|
| Aplicación y pantalla | |
| Quién la usa y cuánto | |
| **Operaciones** (evento / acción → qué hace) | |
| **Entradas** (query string, formulario, Session, ViewState, sessionStorage) | |
| **Datos** (tablas, consultas, procedimientos) | |
| **Reglas** (y dónde están: code-behind, markup, JavaScript, SQL) | |
| **Seguridad** (quién entra, quién hace cada operación, ¿se comprueba en el servidor?) | |
| **🚩 Señales de alarma** | |

## Paso 2 — Contrato y diseño (8 min)

| Operación legacy | Verbo + URL nueva | Acción | Caso de uso | Regla de dominio | Autorización |
|---|---|---|---|---|---|
| | | | | | |
| | | | | | |
| | | | | | |

Y responded:

1. ¿Qué entidad(es) y qué métodos del dominio hacen falta? ¿Qué devuelve cada uno si la regla no se cumple?
2. ¿Qué tabla(s) se mapean y qué nombres del legacy hay que respetar (`HasColumnName`, claves ajenas)?
3. ¿Qué JavaScript desaparece, cuál se mueve a `wwwroot/js` y qué datos necesita en `data-*`?
4. ¿Qué URLs antiguas hay que redirigir?

## Paso 3 — Pruebas y estimación (4 min)

| Nivel | Prueba que escribiríais primero |
|---|---|
| Unitaria (dominio) | |
| Unitaria (caso de uso) | |
| Integración (pantalla) | |

**Estimación** en días y **mayor riesgo** (una línea).

## Puesta en común (2 min por grupo)

Cada grupo cuenta: la pantalla, la regla más escondida que ha encontrado, su mayor riesgo y la estimación. El formador recoge las fichas: son el primer *backlog* de la migración real.
