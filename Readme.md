# Azure functions proxy to XMLA

Adaptation of https://github.com/microsoft/azure-analysis-services-http-sample to Azure Functions world.

Define data connection string in the environment variable `DatasetConnectionString`
```
Data Source=powerbi://api.powerbi.com/v1.0/myorg/{Workspace};
initial catalog={DataSetName};
User Id={UserName}@{Tenant}.onmicrosoft.com;
Password={Password}"
```