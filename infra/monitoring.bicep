// Logs and Application Insights for MyPoGoCompanion (E01-F08, docs/OBSERVABILITY.md). Used by main.bicep, and
// deployed by the QA and prod deploys (.github/scripts/deploy_api.sh) for their own copy, so a new Application
// Insights appears without anyone running main.bicep again. Safe to run again: it only changes what differs, and a
// deploy for one copy leaves the other copy's Application Insights alone.

@description('Azure region.')
param location string = 'westeurope'

@description('The copies that get an Application Insights: prod, qa or both.')
param environments array = ['prod', 'qa']

var name = 'mypogocompanion'

// --- Log workspace: 30 days, capped at 0.16 GB/day (about 5 GB/month, Azure's free allowance; decision 2026-11-02).
// When the cap is reached, logging pauses until the next day (UTC); the app keeps working.
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${name}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json('0.16') }
  }
}

// --- One Application Insights per copy, storing its data in the workspace above (so the cap covers it too).
// IP addresses stay blanked (Azure's default, DisableIpMasking false); no local sign-in keys beyond the connection
// string the API needs to send.
resource insights 'Microsoft.Insights/components@2020-02-02' = [
  for env in environments: {
    name: 'appi-${name}-${env}'
    location: location
    kind: 'web'
    properties: {
      Application_Type: 'web'
      WorkspaceResourceId: logs.id
      IngestionMode: 'LogAnalytics'
      DisableIpMasking: false
      RetentionInDays: 30
    }
  }
]

output workspaceCustomerId string = logs.properties.customerId

@description('Application Insights connection strings, in the order of "environments" (not secret, but kept as a Container Apps secret).')
output connectionStrings array = [for (env, i) in environments: insights[i].properties.ConnectionString]
