// One Web App for the whole game — it serves the built React SPA as static files
// and hosts the SignalR hub. Deployed into the resource group that already owns
// the B1 plan, so the plan is a same-scope `existing` reference.
//
//   az deployment group create -g rg-superherofight -f infra/main.bicep

targetScope = 'resourceGroup'

@description('Web App name. Becomes <name>.azurewebsites.net, so it must be globally unique.')
param name string = 'mexicanstandoff'

@description('The existing B1 plan that hosts the app. Linux — a plan cannot mix operating systems.')
param appServicePlanName string = 'superherofight-plan'

@description('Lets the host add auto-playing bot seats in the lobby. Off unless a deploy asks for it.')
param botsEnabled bool = false

param location string = resourceGroup().location

resource plan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: appServicePlanName
}

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true

    // In-memory game state pins a game to one instance: never scale this out without
    // Azure SignalR Service + external state (see docs/tech-stack.md). One worker also
    // makes affinity cookies pointless — there is nowhere else for a phone to land.
    clientAffinityEnabled: false

    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      numberOfWorkers: 1

      // Off by default on App Service — without it SignalR silently falls back to
      // long-polling and the reveal animations judder.
      webSocketsEnabled: true

      // An idle unload would wipe every game in progress, and hand the next party a
      // cold .NET start while eight people stare at a QR code.
      alwaysOn: true

      healthCheckPath: '/health'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'

      appSettings: [
        // Config keys mirror GameOptions ("Game" section); __ is the nesting separator.
        // The selection timer is deliberately not here: the host picks it per game in
        // the lobby, so GameOptions.SelectionTimerSeconds is only a fallback for a
        // CreateGame that sends no settings — which the SPA never does.
        {
          name: 'Game__Bots__Enabled'
          value: toLower(string(botsEnabled))
        }
        // TLS terminates at the App Service front end, so Kestrel sees plain http from
        // it. Without this the app builds http:// links behind an https:// site — and
        // the share/QR buttons hide themselves outside a secure context.
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
      ]
    }
  }
}

output webAppName string = site.name
output url string = 'https://${site.properties.defaultHostName}'
