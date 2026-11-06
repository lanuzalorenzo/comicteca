# 0005 — Configuración de compilación y versiones de paquetes en dos archivos raíz

**Fecha:** 6 de noviembre de 2026
**Estado:** Aceptada

## Contexto

La solución tiene siete proyectos y todos comparten la misma configuración de compilación y las mismas versiones de EF Core. `Directory.Build.props` ya existía para fijar `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `AnalysisLevel` y la auditoría NuGet; las versiones de los paquetes seguían repartidas por los `.csproj`.

`Directory.Build.props` y `Directory.Packages.props` son mecanismos de MSBuild y NuGet, no una imposición de la plataforma. Microsoft documenta el primero para [personalizar la compilación por directorio](https://learn.microsoft.com/dotnet/msbuild/customize-by-directory) —MSBuild lo importa solo si está en el directorio del proyecto o en uno superior— y el segundo como [gestión central de paquetes](https://learn.microsoft.com/nuget/consume-packages/central-package-management), con plantilla propia (`dotnet new packagesprops`). Son la vía soportada cuando varios proyectos comparten dependencias, y evitan que cada archivo sea una copia que se desvía.

## Decisión

1. **Mantener `Directory.Build.props`** como fuente única de la configuración de compilación: estricta en local y en CI, con la auditoría NuGet como error.
2. **Añadir `Directory.Packages.props`** con `ManagePackageVersionsCentrally`. Las versiones se declaran una sola vez y los `.csproj` referencian los paquetes sin `Version`.
3. **`TargetFramework` sigue en cada proyecto.** Hoy todos usan `net10.0`; dejarlo ahí permite multi-target sin tocar la configuración común.

## Alternativas descartadas

- **Dejar las versiones en cada `.csproj`.** Funciona con siete proyectos, pero multiplica los sitios que hay que cambiar cuando EF Core recibe un parche de seguridad y permite que dos proyectos queden en versiones distintas sin que nadie lo note.
- **Un `.props` importado a mano.** Cada proyecto nuevo puede olvidar el `Import`; el archivo raíz se aplica por convención.
- **Un script que reescriba versiones.** Automatiza la edición, no la fuente de verdad.

## Consecuencias

- Cambiar una versión es una línea y un `restore`.
- Una misma ID de paquete usa la misma versión en todo el repositorio. Si un proyecto necesitara otra, se justifica con `VersionOverride`; no se duplica en silencio.
- La lista de paquetes queda a la vista en un archivo, lo que hace evidente qué se actualiza y qué audita Dependabot.
- El proyecto de migraciones de Azure SQL y el de SQLite comparten las versiones de EF Core sin coordinarlas a mano.

## Criterio para revisarla

Se revisa si aparece un caso real de dos versiones simultáneas del mismo paquete o si la solución se parte en repositorios con ciclos de vida separados. En ese caso, `VersionOverride` o un `Directory.Packages.props` por solución son las salidas previstas, no volver a la versión por proyecto.
