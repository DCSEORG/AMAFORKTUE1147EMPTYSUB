#!/bin/bash

# ============================================================================
# Expense Management System - Deployment Script (With GenAI)
# ============================================================================
# This script deploys everything including Azure OpenAI and AI Search
# for full Chat UI functionality with natural language database interaction
# ============================================================================

set -e

# Configuration - Update these values as needed
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
BASE_NAME="expensemgmt"

# Get current user info for SQL admin
echo "Getting current user information..."
ADMIN_LOGIN=$(az account show --query user.name -o tsv)
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

echo "============================================"
echo "Deployment Configuration (with GenAI):"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location: $LOCATION"
echo "  Admin Login: $ADMIN_LOGIN"
echo "  Admin Object ID: $ADMIN_OBJECT_ID"
echo "============================================"

# Create resource group if it doesn't exist
echo "Creating resource group..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

# Deploy infrastructure using Bicep (including GenAI)
echo "Deploying infrastructure with GenAI resources..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infrastructure/main.bicep \
  --parameters \
    location="$LOCATION" \
    baseName="$BASE_NAME" \
    adminLogin="$ADMIN_LOGIN" \
    adminObjectId="$ADMIN_OBJECT_ID" \
    deployGenAI=true \
  --query properties.outputs \
  --output json)

# Extract outputs
WEB_APP_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.webAppName.value')
WEB_APP_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.webAppUrl.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerName.value')
DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.databaseName.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
OPENAI_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.searchEndpoint.value')

echo "============================================"
echo "Infrastructure deployed successfully!"
echo "  Web App Name: $WEB_APP_NAME"
echo "  Web App URL: $WEB_APP_URL"
echo "  SQL Server: $SQL_SERVER_FQDN"
echo "  Database: $DATABASE_NAME"
echo "  Managed Identity: $MANAGED_IDENTITY_NAME"
echo "  OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "  OpenAI Model: $OPENAI_MODEL_NAME"
echo "  Search Endpoint: $SEARCH_ENDPOINT"
echo "============================================"

# Configure App Service settings (including GenAI settings)
echo "Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config appsettings set \
  --name "$WEB_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "ConnectionStrings__DefaultConnection=$CONNECTION_STRING" \
    "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
    "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
    "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
    "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
    "Search__Endpoint=$SEARCH_ENDPOINT" \
  --output none

# Wait for SQL Server to be fully ready
echo "Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30

# Add current user's IP to SQL firewall
echo "Adding current IP to SQL Server firewall..."
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "DeploymentMachine" \
  --start-ip-address "$MY_IP" \
  --end-ip-address "$MY_IP" \
  --output none

# Install Python dependencies
echo "Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity

# Import database schema
echo "Importing database schema..."
python3 run-sql.py

# Configure database roles for managed identity
echo "Configuring database roles for managed identity..."
# Update script.sql with the actual managed identity name
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak
python3 run-sql-dbrole.py

# Create stored procedures
echo "Creating stored procedures..."
python3 run-sql-stored-procs.py

# Deploy application code
echo "Deploying application code..."
az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$WEB_APP_NAME" \
  --src-path ./app.zip \
  --type zip

echo "============================================"
echo "Deployment complete with GenAI features!"
echo ""
echo "Access your application at:"
echo "  ${WEB_APP_URL}/Index"
echo ""
echo "The Chat UI is now fully enabled with:"
echo "  - Azure OpenAI GPT-4o for natural language processing"
echo "  - Azure AI Search for enhanced context"
echo "  - Function calling for database operations"
echo "============================================"
