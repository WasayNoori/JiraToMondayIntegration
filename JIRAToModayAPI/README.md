# Jira to Monday Integration API

This API integrates Jira with Monday.com, allowing you to transfer Jira issues to Monday.com boards.

## Configuration

### Azure Key Vault Setup

The application uses Azure Key Vault to securely store sensitive configuration values. The following secrets should be configured in your Key Vault:

1. **JiraToken** - Your Jira API token
2. **BlobConnectionString** - Azure Blob Storage connection string

### Configuration Files

#### appsettings.json
```json
{
  "KeyVault": {
    "VaultUrl": "https://JiraMonday.vault.azure.net/"
  },
  "AzureBlob": {
    "ContainerName": "jira-monday-mappings"
  },
  "Jira": {
    "Url": "https://your-domain.atlassian.net",
    "Username": "your-email@domain.com"
  }
}
```

#### Key Vault Secrets
- `JiraToken` - Your Jira API token (stored in Key Vault)
- `BlobConnectionString` - Azure Blob Storage connection string (stored in Key Vault)

### Authentication

The application uses `DefaultAzureCredential` to authenticate with Azure Key Vault. This supports:
- Managed Identity (recommended for production)
- Service Principal
- Azure CLI credentials (for development)

## API Endpoints

### Jira Endpoints
- `GET /api/jira/projects` - Get all Jira projects
- `POST /api/jira/search` - Search Jira issues
- `GET /api/jira/issues` - Get Jira issues with filters
- `GET /api/jira/issues/typed` - Get typed Jira issues response

### Main Endpoints
- `GET /api/main/TransferIssue` - Transfer Jira issues to Monday.com

## Development Setup

1. Ensure you have the Azure CLI installed and are logged in
2. Update the `appsettings.json` with your Key Vault URL and Jira configuration
3. Make sure the secrets are properly configured in your Azure Key Vault
4. Run the application

## Production Deployment

For production deployment:
1. Use Managed Identity for Key Vault authentication
2. Ensure the application has proper access policies to the Key Vault
3. Configure environment-specific settings in `appsettings.{Environment}.json`



