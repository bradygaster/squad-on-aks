@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource frontdoor 'Microsoft.Cdn/profiles@2024-09-01' = {
  name: take('frontdoor-${uniqueString(resourceGroup().id)}', 260)
  location: 'Global'
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
  tags: {
    'aspire-resource-name': 'frontdoor'
  }
}

resource appEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-09-01' = {
  name: take('app-endpoint-${uniqueString(resourceGroup().id)}', 46)
  location: 'Global'
  properties: {
    enabledState: 'Enabled'
  }
  parent: frontdoor
}

resource appOriginGroup 'Microsoft.Cdn/profiles/originGroups@2024-09-01' = {
  name: take('app-origin-group-${uniqueString(resourceGroup().id)}', 90)
  properties: {
    healthProbeSettings: {
      probePath: '/health/ready'
      probeProtocol: 'Http'
      probeIntervalInSeconds: 30
    }
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
  }
  parent: frontdoor
}

resource appOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2024-09-01' = {
  name: take('app-origin-${uniqueString(resourceGroup().id, 'squad-on-aks-live.westus2.cloudapp.azure.com')}', 90)
  properties: {
    enabledState: 'Enabled'
    enforceCertificateNameCheck: false
    hostName: 'squad-on-aks-live.westus2.cloudapp.azure.com'
    httpPort: 80
    originHostHeader: 'squad-on-aks-live.westus2.cloudapp.azure.com'
  }
  parent: appOriginGroup
}

resource appRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-09-01' = {
  name: take('app-route-${uniqueString(resourceGroup().id)}', 90)
  properties: {
    enabledState: 'Enabled'
    forwardingProtocol: 'HttpOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    originGroup: {
      id: appOriginGroup.id
    }
    patternsToMatch: [
      '/*'
    ]
  }
  parent: appEndpoint
  dependsOn: [
    appOrigin
  ]
}

output app_endpointUrl string = 'https://${appEndpoint.properties.hostName}'