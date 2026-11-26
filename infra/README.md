# Infraestructura

> **Estado:** Pendiente (2026-11-24). La plantilla se compila y se escanea en
> el CI; no se ha creado ningún recurso en Azure. Crear recursos exige
> autorización explícita del propietario.

Plantillas Bicep de la fase 2.

## Qué crea

| Pieza | Recurso | Oferta |
|---|---|---|
| `logAnalytics` | `Microsoft.OperationalInsights/workspaces` | 30 días de retención |
| `entornoContenedores` | `Microsoft.App/managedEnvironments` | Consumo, logs en Log Analytics |
| `api` | `Microsoft.App/containerApps` | 0,25 vCPU, 0,5 GiB, de 0 a 1 réplica |
| `identidad` | `Microsoft.ManagedIdentity/userAssignedIdentities` | Identidad de la API |
| `servidorSql` | `Microsoft.Sql/servers` | Solo Microsoft Entra ID, sin contraseña |
| `baseSql` | `Microsoft.Sql/servers/databases` | Serverless `GP_S_Gen5_2`, oferta gratuita y auto-pausa |
| `web` | `Microsoft.Web/staticSites` | Plan gratuito |
| `presupuesto` | `Microsoft.Consumption/budgets` | Módulo aparte, en la suscripción |

La API recibe la audiencia, el tenant, la cuenta permitida, el origen de la web
y una cadena de conexión con `Authentication=Active Directory Default`; no hay
contraseñas ni claves en los parámetros. La base nace con la red pública
desactivada. La conectividad, los usuarios de la base, el usuario migrador y la
publicación de la imagen se completan en un cambio posterior. Hasta entonces la
plantilla describe los recursos, no una aplicación arrancable.

## Publicación de la web

El recurso `web` y su origen en `Cors__AllowedOrigins__0` ya están en
`main.bicep`. La publicación no la hace el CI ni un push: el workflow manual
`SWA (manual)` publica con `scripts/preparar-swa.py` y el secreto
`AZURE_STATIC_WEB_APPS_API_TOKEN`; el despliegue sigue **Pendiente** hasta que el
propietario lo autorice y lo ejecute. El entorno `swa` debe llevar una regla de
protección con revisor obligatorio.

## Nombres

Todos los nombres de recurso salen de la función `nombre(tipo, carga, entorno,
región)` de `main.bicep`: abreviatura del Cloud Adoption Framework, carga,
entorno, código de región y un sufijo estable de seis caracteres. Un `name:`
literal en un recurso de nivel superior es un hallazgo.

```bash
python3 scripts/comprobar-nombres-iac.py --autoprueba
```

La autoprueba comprueba que un nombre literal se rechaza, que uno calculado
pasa, que un parámetro con valor por defecto no sirve de puerta trasera, que el
mapa de abreviaturas y la lista de tipos coinciden y que la carga máxima cabe en
el límite de cada tipo. El módulo del presupuesto recibe el nombre ya resuelto;
la prueba también vigila ese argumento.

## Validación

```bash
# Compilar. El JSON generado no se versiona.
bicep build infra/main.bicep --outfile infra/main.json

# Nombres y autoprueba.
python3 scripts/comprobar-nombres-iac.py --autoprueba

# Escaneo de configuración. Trivy no lee Bicep: se escanea el ARM compilado.
docker run --rm -e TRIVY_MISCONFIG_SCANNERS=azure-arm \
  -v "$PWD/infra:/src" aquasec/trivy:0.76.0 config \
  --severity CRITICAL,HIGH --exit-code 1 /src/main.json
```

El CI ejecuta estos tres pasos dentro del trabajo «Análisis estático (CodeQL)»
y publica el SARIF de Trivy; no añade un check nuevo al ruleset. Trivy no
evalúa las expresiones de ARM: los nombres calculados se ven como valores
vacíos. Por eso el escaneo de SQL deja un hallazgo MEDIUM (`AZU-0027`): la
auditoría está declarada, pero Trivy no resuelve la relación entre el servidor
—con nombre calculado— y su recurso hijo `auditingSettings`. El umbral del CI
es CRITICAL y HIGH, así que ese hallazgo no corta el trabajo.

## Parámetros

`main.parameters.example.json` lleva marcadores, no valores reales. El archivo
real, `infra/main.parameters.json`, está ignorado por Git. Los datos del tenant,
la cuenta permitida, el administrador de SQL y el correo de aviso se pasan en el
despliegue y no se versionan. La imagen debe ser pública: el registro es GHCR y
no se guardan credenciales de registro.
