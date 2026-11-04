# Qué puedo demostrar y qué no

Esta página es la diferencia entre tener el carné de conducir y haber conducido. Todo lo que digo de la Comicteca está aquí con la prueba que lo sostiene, y lo que no he probado aparece como pendiente. La última comprobación es del 4 de noviembre de 2026, en local.

## Cómo lo compruebo

```bash
dotnet build Comicteca.slnx -c Release -warnaserror
dotnet test Comicteca.slnx -c Release --no-build
dotnet format Comicteca.slnx --verify-no-changes --no-restore
gitleaks detect --source . --redact --verbose --exit-code 2
```

## Qué está probado

| Capacidad | Dónde está | Cómo lo compruebo | Estado |
|---|---|---|---|
| Compila en Release sin avisos | Toda la solución | `dotnet build` | Validado |
| Una serie no puede quedarse sin título | `Serie`, `Tomo` | 6 pruebas unitarias: título, longitudes y tomo único | Validado |
| La API devuelve las series agrupadas | `Api/Program.cs` | 1 prueba de contrato con base temporal y 1 de `/health` | Validado |
| Carga el CSV de demostración | `CsvSeeder` | Arranque real: 20 series y 65 filas | Validado en local |
| El esquema se versiona con migraciones | `Infrastructure/Migrations` | `dotnet ef database update`; historial en `__EFMigrationsHistory` | Validado |
| Agrupa por serie y editorial | `SqliteSerieReader` | Prueba con dos series y un tomo único | Validado |
| Alta y baja de series y tomos por API | `ISerieWriter`, `SqliteSerieWriter` | 17 pruebas de contrato: 201, 204, 400, 404 y 409 | Validado |
| Alta y baja de series y tomos desde la web | `Home.razor` | Prueba manual en Chromium a 375 px: alta, duplicado, número inválido, baja de tomo y de serie, sin recargar | Validado en local: sin prueba automatizada en el repositorio |
| Busca por título y editorial | `Home.razor` | Prueba manual en el navegador | Parcial: sin prueba automática |
| Se ve bien en el móvil | `app.css` | Prueba manual a 375 px y 1280 px | Parcial: sin dispositivos reales |
| Solo el navegador queda limitado por CORS | `Api/appsettings.json` | Prueba manual con y sin origen permitido | Parcial: sin prueba automática |
| No hay secretos en el código | `.gitleaks.toml` | `gitleaks detect` | Validado: sin hallazgos |
| CI: compilación con analizadores y pruebas | `.github/workflows/ci.yml` | Ejecución en GitHub Actions | Validado: run del 3 de noviembre de 2026 en verde |
| CI: Gitleaks sobre el historial | `.github/workflows/ci.yml` | Ejecución en GitHub Actions | Validado: run del 3 de noviembre de 2026 en verde |
| CI: auditoría de dependencias | `.github/workflows/ci.yml` | Ejecución en GitHub Actions | Validado: run del 3 de noviembre de 2026 en verde |
| Despliegue en Azure | — | — | Pendiente: API en contenedor, Functions para el aviso y Azure SQL (ADR 0003) |
| Ensayo del despliegue en local con contenedores | `compose.yaml`, Dockerfiles | `docker compose up`: lista en 5077, `/health` en 5094, prueba a 375 px en contenedor y base conservada tras `down` | Validado en local: sin Docker en CI |
| Aviso de novedades | — | — | Diseño |
| Varias personas con su cuenta | — | — | Pendiente |

## Lo que no puedo decir

- «Validado» quiere decir que lo he ejecutado y he visto el resultado en mi máquina, no que esté desplegado ni que lo haya usado otra persona.
- No he medido la cobertura: hay 25 pruebas y ningún umbral.
- La interfaz no la he probado con lector de pantalla ni en Safari.
- El CORS no protege la API de otros clientes: solo limita a los navegadores. Cualquiera con la dirección puede llamarla.
- La API no pide autenticación: quien llegue a ella puede escribir. La escritura solo vale en local hasta que exista el despliegue con Entra ID.
