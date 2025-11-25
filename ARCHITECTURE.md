# Azure Services Architecture

This diagram shows the Azure services deployed by this solution and how they connect.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              Azure Resource Group                                │
│                            (rg-expensemgmt-demo)                                │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────────┐│
│  │                         User Assigned Managed Identity                       ││
│  │                        (mid-appmodassist-xxxxxx)                            ││
│  │                                                                              ││
│  │   Used by App Service to authenticate to SQL Database and Azure OpenAI     ││
│  └─────────────────────────────────────────────────────────────────────────────┘│
│                                      │                                           │
│                    ┌─────────────────┼─────────────────┐                        │
│                    │                 │                 │                        │
│                    ▼                 ▼                 ▼                        │
│  ┌──────────────────────┐ ┌──────────────────┐ ┌──────────────────────┐        │
│  │    App Service       │ │   Azure SQL      │ │   Azure OpenAI       │        │
│  │ (app-expensemgmt-xx) │ │   Database       │ │  (oai-expensemgmt)   │        │
│  │                      │ │                  │ │                      │        │
│  │  ┌────────────────┐  │ │  ┌────────────┐  │ │  ┌────────────────┐  │        │
│  │  │ ASP.NET 8.0   │  │ │  │ Northwind  │  │ │  │    GPT-4o      │  │        │
│  │  │ Razor Pages   │──┼─┼─▶│  Database  │  │ │  │    Model       │  │        │
│  │  │    + APIs     │  │ │  │            │  │ │  │                │  │        │
│  │  └────────────────┘  │ │  └────────────┘  │ │  └────────────────┘  │        │
│  │         │            │ │                  │ │          ▲           │        │
│  │         │            │ │  Entra ID Only   │ │          │           │        │
│  │         │            │ │  Authentication  │ │   Function Calling   │        │
│  │         ▼            │ └──────────────────┘ │   for DB Operations  │        │
│  │  ┌────────────────┐  │                      └──────────────────────┘        │
│  │  │   Chat UI      │  │                                ▲                      │
│  │  │   Component    │──┼────────────────────────────────┘                      │
│  │  └────────────────┘  │                                                       │
│  │                      │         ┌──────────────────────┐                      │
│  │  App Service Plan    │         │   Azure AI Search    │                      │
│  │  (Standard S1)       │         │ (search-expensemgmt) │                      │
│  └──────────────────────┘         │                      │                      │
│                                   │  Optional RAG index  │                      │
│                                   └──────────────────────┘                      │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ HTTPS
                                      ▼
                              ┌───────────────┐
                              │    Users      │
                              │  (Browsers)   │
                              └───────────────┘
```

## Service Details

| Service | Purpose | SKU | Region |
|---------|---------|-----|--------|
| App Service | Hosts the ASP.NET web application | S1 Standard | UK South |
| App Service Plan | Compute resources for App Service | S1 | UK South |
| Azure SQL Server | Database server with Entra ID auth | N/A | UK South |
| Azure SQL Database | Stores expense data | Basic | UK South |
| User Assigned MI | Enables passwordless auth | N/A | UK South |
| Azure OpenAI | AI chat with function calling | S0 | Sweden Central |
| Azure AI Search | Context search for RAG | Basic | UK South |

## Data Flow

1. **User Request** → App Service receives HTTP request
2. **Database Query** → App uses Managed Identity to query Azure SQL via stored procedures
3. **Chat Request** → Chat component sends message to Azure OpenAI
4. **Function Calling** → OpenAI invokes functions to interact with database
5. **Response** → Results returned through the API layer to the user

## Authentication

- **App Service to SQL**: User Assigned Managed Identity with AD authentication
- **App Service to OpenAI**: User Assigned Managed Identity with Cognitive Services OpenAI User role
- **App Service to AI Search**: User Assigned Managed Identity with Search Index Data Contributor role
- **SQL Authentication**: Entra ID only (SQL auth disabled for MCAPS compliance)
