param name string
param location string

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

// Integration events published by the Outbox publisher.
resource integrationEvents 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespace
  name: 'gosit-integration-events'
  properties: {
    defaultMessageTimeToLive: 'P14D'
  }
}

output name string = namespace.name
