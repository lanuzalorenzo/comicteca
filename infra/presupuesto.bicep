targetScope = 'subscription'

// Presupuesto mensual de la suscripción. El nombre lo calcula la función
// `nombre` de `main.bicep`; aquí llega ya resuelto para no duplicar el mapa de
// abreviaturas. No contiene credenciales: solo el correo de aviso, que se pasa
// en el despliegue y no se versiona.

@description('Nombre calculado por la función `nombre` de la plantilla principal.')
param nombrePresupuesto string

@description('Importe mensual del presupuesto, en euros.')
@minValue(1)
param importe int = 1

@description('Primer día del periodo, en ISO 8601.')
param inicio string

@description('Último día del periodo, en ISO 8601.')
param fin string

@description('Correo que recibe los avisos.')
param emailAviso string

resource presupuesto 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: nombrePresupuesto
  properties: {
    timePeriod: {
      startDate: inicio
      endDate: fin
    }
    timeGrain: 'Monthly'
    category: 'Cost'
    amount: importe
    notifications: {
      gasto80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [
          emailAviso
        ]
      }
      prevision100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [
          emailAviso
        ]
      }
    }
  }
}
