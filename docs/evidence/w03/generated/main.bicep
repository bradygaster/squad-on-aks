targetScope = 'subscription'

param resourceGroupName string

param location string

param principalId string

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

module aks_acr 'aks-acr/aks-acr.bicep' = {
  name: 'aks-acr'
  scope: rg
  params: {
    location: location
  }
}

module aks 'aks/aks.bicep' = {
  name: 'aks'
  scope: rg
  params: {
    location: location
    acrName: aks_acr.outputs.name
  }
}