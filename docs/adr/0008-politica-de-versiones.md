# 0008 — Política de versiones de la plataforma

**Fecha:** 15 de noviembre de 2026
**Estado:** Aceptada (la versión del SDK queda declarada en `global.json`; no se ha ejecutado ninguna migración)

## De qué iba

Comicteca nació en .NET 10: el primer commit ya declaraba `net10.0` y no ha cambiado nunca de versión mayor. Tampoco había una regla sobre cuándo cambiar. `AGENTS.md` decía cómo —un cambio propio, CI en verde y versiones exactas—, pero no cuándo, quién decide, qué se toca ni qué evidencia cierra el cambio. Sin `global.json`, además, nada declaraba qué SDK espera el proyecto: solo el TFM impedía usar uno más antiguo, y uno más nuevo —el 11, publicado el día 10— habría compilado contra `net10.0` con otro compilador y otros analizadores sin que nadie lo hubiera decidido.

El 10 de noviembre de 2026 salió .NET 11, la primera versión que obliga a decidir. No es una decisión gratuita en ninguna dirección: .NET 11 es STS y termina el 14 de noviembre de 2028 —desde .NET 9 la STS dura 24 meses, no 18—, [el mismo día que .NET 10 LTS](https://dotnet.microsoft.com/platform/support/policy/dotnet-core). Migrar no alarga el soporte ni un día; quedarse tampoco lo acorta. La LTS siguiente, .NET 12, llegará en noviembre de 2027.

Tres hechos más pesan:

- Los parches mensuales ya llegan solos: Dependabot agrupa `patch` y `minor` y los fusiona con los siete checks en verde.
- El aviso de una versión mayor existe pero es mudo: Dependabot abrirá un `nuget-major` con EF Core 11, que exige .NET 11 y no compila contra `net10.0`, y se quedará en rojo sin que nada diga qué hacer con él.
- El CI comprueba que no haya dependencias vulnerables (`dotnet list package --vulnerable`), no que la versión sea la adecuada (`--outdated`).

## Qué decidí

1. **LTS por defecto.** Se adopta la LTS, no la STS. Una STS solo entra con un motivo concreto y escrito —una característica que sustituya algo real, no «estar al día»— y enmienda este documento antes de tocar los proyectos. El soporte no es un motivo: 10 y 11 terminan el mismo día.
2. **Ventana de adopción.** La LTS se adopta dentro de los seis meses siguientes a su publicación y, en todo caso, antes de que caduque la versión en uso. Para .NET 12: entre noviembre de 2027 y mayo de 2028, con .NET 10 soportado hasta noviembre de 2028.
3. **Una sola versión declarada, en tres sitios.** `global.json` fija la versión mayor y menor del SDK y acepta cualquier banda 10.0.xxx; `TargetFramework` sigue en cada `.csproj` (ADR 0005); `Directory.Packages.props` fija los paquetes. Los tres se mueven en el mismo cambio, junto con la lista de abajo.
4. **El cambio de versión es un cambio propio.** No se mezcla con una característica, con un arreglo ni con una actualización de los paquetes `LL.*`, y pasa por el revisor como cualquier lote que toca el CI y las imágenes.
5. **Las mejoras no viajan con la versión.** Adoptar una característica de la plataforma es una decisión aparte, una por cambio, y solo cuando sustituye algo que hoy se hace peor. Que exista no es motivo.
6. **Los parches no necesitan decisión.** Dependabot lleva los de los paquetes; la etiqueta de la imagen (`sdk:10.0`) trae el último parche al construir; la versión mayor y menor del SDK solo se mueve con el cambio de versión.

### Qué se toca en este repositorio

| Sitio | Qué cambia |
|---|---|
| `global.json` | La versión mayor y menor del SDK |
| Los 7 `.csproj` | `TargetFramework` |
| `Directory.Packages.props` | Los paquetes del marco: EF Core, ASP.NET Core, OpenAPI, Blazor WebAssembly y su DevServer, MSAL, JwtBearer, Mvc.Testing y Pkcs. El resto —xunit, coverlet, el SDK de pruebas y `System.IdentityModel.Tokens.Jwt`— sigue su propio ciclo |
| `dotnet-tools.json` | `dotnet-ef`, versión exacta y `rollForward` desactivado |
| `src/Comicteca.Api/Dockerfile` | `sdk` y `aspnet` |
| `src/Comicteca.Web/Dockerfile` | `sdk` |
| `.github/workflows/ci.yml` | Los dos `setup-dotnet` y la acción `dotnet@v1.4` |
| `LL.Hosting` y `LL.Security` | Comprobar que el paquete es consumible desde el TFM nuevo; si no, el cambio es de `shared-dotnet` |
| `docs/pruebas.md` | Los requisitos, que hoy dicen «.NET 10 SDK» |
| `tests/e2e` | Chromium, la API y la web con la versión nueva |

En las imágenes la versión la fija la etiqueta (`sdk:10.0`), no `global.json`; el `Dockerfile` copia el archivo al contexto del `restore` para que un cambio de etiqueta incompatible falle al construir en vez de pasar inadvertido.

Si la acción compartida no acepta la versión como entrada, el cambio va a `core` y este repositorio espera su etiqueta: un control que falta se resuelve en la plataforma.

### Qué cierra el cambio

1. El CI completo en verde en el PR: compilación y pruebas, enlaces, interfaz, secretos, dependencias, análisis estático e imágenes.
2. `dotnet format` sin cambios y `gitleaks` limpio, como en cualquier lote.
3. `docker compose up -d --build` con la API en `healthy`, la web en 200, 401 sin token y el listado de las 20 series con token.
4. El login del navegador con los valores del tenant, que es la parte que hoy sigue en **Parcial**.
5. Una entrada en `bitacora/` con la versión, el run y lo observado; la entrada de `AGENTS.md` §Estados actualizada y los requisitos de `docs/pruebas.md`.

## Qué otras opciones tenía

| Opción | Lo bueno | Por qué no |
|---|---|---|
| Adoptar .NET 11 ahora | La última versión y C# 15 | No alarga el soporte ni un día y ninguna de sus mejoras sustituye algo que esta aplicación haga hoy; añade una migración en plena fase 2, con el despliegue sin cerrar |
| No migrar nunca y vivir de parches | Coste cero | Termina en noviembre de 2028 sin soporte y con tres años de cambios que absorber de golpe |
| Seguir la LTS hasta su último día | Una migración cada tres años | Llega al límite sin margen: si la LTS siguiente se retrasa o el salto falla, no hay red |
| Adoptar cada STS al salir | Siempre al día | Dos migraciones en dos años para no ganar soporte: el doble de coste y el mismo beneficio |
| Fijar el SDK con una revisión exacta | Reproducibilidad total | Un clon con otra revisión de la misma versión falla sin motivo; lo que hay que fijar es la versión mayor y menor, no el número de parche |
| Convertir `--outdated` en un check que falla | Nada envejece en silencio | Fallaría todos los meses y obligaría a actualizar a destiempo; informar basta |

## Qué gano y qué pierdo

- **Gano** una respuesta escrita a la pregunta que se repite cada noviembre y un cambio con la lista de sitios y la evidencia antes de empezar.
- **Gano** un `global.json`: un clon con 9.x u 11.x falla al instante y con un mensaje claro, en lugar de compilar con otro compilador y otros analizadores; dentro de 10.0, cualquier banda vale, que es lo que necesitan el CI y las imágenes.
- **Pierdo** la actualización «de paso»: la versión no viaja con una característica, así que una mejora que la exija espera a la ventana o abre su propia enmienda.
- **Asumo** que .NET 12 será LTS y llegará en noviembre de 2027, como marca el calendario anual; si cambia, se revisa este documento.
- **Asumo** que el PR `nuget-major` de Dependabot seguirá en rojo a propósito. No es un fallo: se cierra citando este ADR. Su grupo no se desactiva porque también avisa de las actualizaciones mayores de los paquetes que sí se adoptan.
- **Dejo pendiente** el `--outdated` informativo en el CI. No bloquea; si se añade, será como informe, igual que la auditoría de dependencias.

## Criterio para revisarla

Se revisa si la plataforma compartida publica su propia política de versiones —entonces esta pasa a ser su instancia y no su origen—, si una característica concreta justifica una STS, si .NET cambia la cadencia o el tipo de soporte, o si alguna pieza de la pila (Functions, Container Apps o la acción del CI) deja de admitir la versión elegida.
