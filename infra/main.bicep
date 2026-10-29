// Azure resources for MyPoGoCompanion (prod and QA), deployed into one resource group.
// Run once from Azure Cloud Shell; see docs/SETUP-ENVIRONMENTS.md. Safe to run again: it only changes what differs.
//
// Container images live in GitHub Container Registry (free for a public repository), so there is no Azure registry.
// Scheduled jobs (daily import, AI researcher, backups) are added to the Container Apps environment by their own
// deploy pipelines when they exist.

@description('Azure region for everything except where noted.')
param location string = 'westeurope'

@description('''GitHub repository allowed to sign in to Azure, exactly as GitHub's sign-in token names it:
"owner@ownerId/name@repositoryId" (the numeric IDs make it immune to someone re-creating a deleted name).
If the repository is renamed, deploy again with the new name.''')
param githubRepository string = 'Roxkax@3799595/MyPoGoCompanion@1434430254'

@description('Email for the monthly budget alerts.')
param alertEmail string

@description('Monthly budget in the subscription currency; alerts at 80% spent and 100% forecast.')
param monthlyBudget int = 5

@description('First day of the current month; leave the default.')
param budgetStartDate string = utcNow('yyyy-MM-01')

var name = 'mypogocompanion'
var environments = ['prod', 'qa']

// --- Logs: capped at 0.5 GB/day and 30 days, inside the free allowance -------------------------------------
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${name}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json('0.5') }
  }
}

// --- Container Apps: consumption plan, scale to zero ------------------------------------------------------
resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${name}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// Placeholder image until the API pipeline (E00-F06 / E00-F08) deploys the real one.
resource api 'Microsoft.App/containerApps@2024-03-01' = [
  for env in environments: {
    name: 'api-${env}'
    location: location
    properties: {
      managedEnvironmentId: containerEnv.id
      configuration: {
        ingress: { external: true, targetPort: 80, transport: 'auto' }
      }
      template: {
        containers: [
          {
            name: 'api'
            image: 'mcr.microsoft.com/k8se/quickstart:latest'
            resources: { cpu: json('0.25'), memory: '0.5Gi' }
          }
        ]
        scale: { minReplicas: 0, maxReplicas: env == 'prod' ? 2 : 1 }
      }
    }
  }
]

// --- Web app: Static Web Apps free plan; QA is its named environment "qa", created on first deploy --------
resource web 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'swa-${name}'
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

// --- GitHub Actions sign-in (OIDC): no passwords stored -------------------------------------------------
resource github 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${name}-github'
  location: location
}

// One credential per GitHub environment. Azure rejects parallel writes here, so they are created one at a time.
@batchSize(1)
resource githubEnvironments 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = [
  for env in environments: {
    parent: github
    name: 'github-${env}'
    properties: {
      issuer: 'https://token.actions.githubusercontent.com'
      subject: 'repo:${githubRepository}:environment:${env}'
      audiences: ['api://AzureADTokenExchange']
    }
  }
]

// Contributor on this resource group only.
resource githubContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, github.id, 'contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
    principalId: github.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- Budget alert ---------------------------------------------------------------------------------------
resource budget 'Microsoft.Consumption/budgets@2023-05-01' = {
  name: 'budget-${name}'
  properties: {
    category: 'Cost'
    amount: monthlyBudget
    timeGrain: 'Monthly'
    timePeriod: { startDate: budgetStartDate }
    notifications: {
      actual80: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 80, thresholdType: 'Actual', contactEmails: [alertEmail] }
      forecast100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Forecasted', contactEmails: [alertEmail] }
    }
  }
}

// --- Values for the GitHub environments ----------------------------------------------------------------
output AZURE_CLIENT_ID string = github.properties.clientId
output AZURE_TENANT_ID string = github.properties.tenantId
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId
output webUrl string = 'https://${web.properties.defaultHostname}'
output apiProdUrl string = 'https://${api[0].properties.configuration.ingress.fqdn}'
output apiQaUrl string = 'https://${api[1].properties.configuration.ingress.fqdn}'
