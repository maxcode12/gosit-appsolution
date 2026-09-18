param name string
param location string

resource cache 'Microsoft.Cache/redis@2024-11-01' = {
  name: name
  location: location
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

output hostName string = cache.properties.hostName
