param location string = resourceGroup().location
param cosmosAccountName string = 'freakycosmosdb${uniqueString(resourceGroup().id)}'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless' // Serverless = Det mest ekonomiska valet! Du betalar bara per anrop.
      }
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'FreakyFashionDb'
  properties: {
    resource: {
      id: 'FreakyFashionDb'
    }
  }
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'Orders'
  properties: {
    resource: {
      id: 'Orders'
      partitionKey: {
        paths: [
          '/id' // Exakt samma partition key som du kör i den lokala C#-kod! Guid.NewGuid().ToString();
        ]
        kind: 'Hash'
      }
    }
  }
}