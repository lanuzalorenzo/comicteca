targetScope = 'resourceGroup'

// Infraestructura de la fase 2. No se despliega desde aquí: la plantilla se
// compila y se escanea en el CI, y el despliegue se autoriza aparte.
//
// Los nombres de recurso salen siempre de `nombre(tipo, carga, entorno, región)`,
// que aplica las abreviaturas del Cloud Adoption Framework y añade un sufijo
// estable. Un `name:` literal en un recurso de nivel superior es un hallazgo:
// lo comprueba `scripts/comprobar-nombres-iac.py` en el CI.

@description('Carga o aplicación. Se usa en el nombre de todos los recursos.')
@minLength(3)
// Diez caracteres dejan el nombre de `cae` en 31 con la región más larga
// (`eus2`): 3 + 1 + 10 + 1 + 4 + 1 + 4 + 1 + 6. El límite del entorno de
// Container Apps es 32 y no se puede pasar.
@maxLength(10)
param carga string = 'comicteca'

@description('Entorno del despliegue.')
@allowed([
  'dev'
  'prod'
])
param entorno string = 'dev'

@description('Región de Azure. Por defecto, la del grupo de recursos.')
param location string = resourceGroup().location

@description('Imagen de la API, con etiqueta o digest. El registro es público: no lleva credenciales.')
param imagen string

@description('Identificador del tenant de Microsoft Entra ID.')
param tenantId string

@description('Audiencia que espera la API, por ejemplo api://<client-id>.')
param apiAudience string

@description('oid de la cuenta que puede usar la aplicación.')
param cuentaPermitida string

@description('Nombre del administrador de Microsoft Entra ID para Azure SQL.')
param administradorSql string

@description('objectId del administrador de Microsoft Entra ID para Azure SQL.')
param administradorSqlObjectId string

@description('Correo que recibe los avisos del presupuesto. Se pasa en el despliegue, no se versiona.')
param emailAviso string

@description('Importe mensual del presupuesto, en euros.')
@minValue(1)
param importePresupuesto int = 1

@description('Primer día del periodo del presupuesto, en ISO 8601.')
param inicioPresupuesto string = '2026-12-01T00:00:00Z'

@description('Último día del periodo del presupuesto, en ISO 8601.')
param finPresupuesto string = '2027-11-30T00:00:00Z'

// Abreviaturas del CAF para los tipos que usa esta plantilla. La función falla
// si se le pasa un tipo que no está en la lista: un tipo inventado no se
// convierte en un nombre plausible.
func abreviaturaTipo(tipo string) string => {
  log: 'log'
  cae: 'cae'
  ca: 'ca'
  sql: 'sql'
  sqldb: 'sqldb'
  stapp: 'stapp'
  id: 'id'
  bt: 'bt'
}[tipo]

// Códigos de región del CAF. Una región desconocida cae a sus tres primeras
// letras, que siguen siendo válidas para todos los tipos que se crean aquí.
func regiones() object => {
  westeurope: 'weu'
  northeurope: 'neu'
  spaincentral: 'esc'
  eastus: 'eus'
  eastus2: 'eus2'
  westus: 'wus'
  westus2: 'wus2'
  centralus: 'cus'
  uksouth: 'uks'
  francecentral: 'frc'
  germanywestcentral: 'gwc'
  swedencentral: 'swc'
  italynorth: 'itn'
}

func abreviaturaRegion(region string) string =>
  union({ '${region}': substring(toLower(region), 0, 3) }, regiones())[region]

// Patrón del CAF: <tipo>-<carga>-<entorno>-<región>-<sufijo>. El sufijo es
// estable por suscripción y grupo de recursos, y hace únicos los nombres que
// Azure exige globales (SQL Server y Static Web Apps).
func nombre(tipo string, carga string, entorno string, region string) string =>
  '${abreviaturaTipo(tipo)}-${toLower(carga)}-${toLower(entorno)}-${abreviaturaRegion(region)}-${substring(uniqueString(subscription().id, resourceGroup().id), 0, 6)}'

var nombreLogAnalytics = nombre('log', carga, entorno, location)
var nombreEntornoContenedores = nombre('cae', carga, entorno, location)
var nombreApi = nombre('ca', carga, entorno, location)
var nombreServidorSql = nombre('sql', carga, entorno, location)
var nombreBaseSql = nombre('sqldb', carga, entorno, location)
var nombreWeb = nombre('stapp', carga, entorno, location)
var nombreIdentidad = nombre('id', carga, entorno, location)
var nombrePresupuesto = nombre('bt', carga, entorno, location)

var autoridad = '${environment().authentication.loginEndpoint}${tenantId}/v2.0'
var origenWeb = 'https://${nombreWeb}.azurestaticapps.net'
var cadenaAplicacion = 'Server=tcp:${nombreServidorSql}.${environment().suffixes.sqlServerHostname},1433;Database=${nombreBaseSql};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Pooling=False'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: nombreLogAnalytics
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource entornoContenedores 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: nombreEntornoContenedores
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource identidad 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: nombreIdentidad
  location: location
}

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: nombreApi
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identidad.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: entornoContenedores.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: imagen
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'Datos__Proveedor'
              value: 'SqlServer'
            }
            {
              name: 'Datos__RutaCsv'
              value: '/app/data/coleccion.csv'
            }
            {
              name: 'ConnectionStrings__Comicteca'
              value: cadenaAplicacion
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: identidad.properties.clientId
            }
            {
              name: 'AZURE_TENANT_ID'
              value: tenantId
            }
            {
              name: 'Autenticacion__Authority'
              value: autoridad
            }
            {
              name: 'Autenticacion__Audience'
              value: apiAudience
            }
            {
              name: 'Autenticacion__CuentaPermitida'
              value: cuentaPermitida
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: origenWeb
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 5
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

resource servidorSql 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: nombreServidorSql
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: administradorSql
      sid: administradorSqlObjectId
      tenantId: tenantId
      azureADOnlyAuthentication: true
    }
  }

  // El nombre `default` lo impone Azure para la configuración de auditoría del
  // servidor; no es un nombre de recurso que se pueda elegir.
  resource auditoria 'auditingSettings' = {
    name: 'default'
    properties: {
      state: 'Enabled'
      isAzureMonitorTargetEnabled: true
      retentionDays: 90
      auditActionsAndGroups: [
        'SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP'
        'FAILED_DATABASE_AUTHENTICATION_GROUP'
        'BATCH_COMPLETED_GROUP'
      ]
    }
  }
}

// Oferta gratuita: serverless, auto-pausa al agotar la cuota y sin agrupación
// de conexiones en la cadena de la API. La conectividad y los usuarios de la
// base se completan en un cambio posterior; aquí la base nace cerrada.
resource baseSql 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: servidorSql
  name: nombreBaseSql
  location: location
  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 34359738368
    minCapacity: json('0.5')
    autoPauseDelay: 60
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    readScale: 'Disabled'
    zoneRedundant: false
  }
}

resource web 'Microsoft.Web/staticSites@2022-03-01' = {
  name: nombreWeb
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

// El presupuesto vive en la suscripción, no en el grupo de recursos: se crea
// con un módulo aparte. El nombre lo calcula la misma función y se pasa ya
// resuelto. Dos avisos: al 80 % del gasto real y al 100 % de la previsión.
module presupuesto 'presupuesto.bicep' = {
  name: 'presupuesto'
  scope: subscription()
  params: {
    nombrePresupuesto: nombrePresupuesto
    importe: importePresupuesto
    inicio: inicioPresupuesto
    fin: finPresupuesto
    emailAviso: emailAviso
  }
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output webUrl string = 'https://${web.properties.defaultHostname}'
output sqlServerFqdn string = servidorSql.properties.fullyQualifiedDomainName
output identityClientId string = identidad.properties.clientId
