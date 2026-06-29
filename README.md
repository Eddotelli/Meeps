# Meeps

A modern event platform built with ASP.NET Core Web API and Blazor WebAssembly.

## About

Meeps lets users create, discover and join local events. The platform handles authentication, location-based search, real-time chat and image uploads.

## Tech Stack

**Backend**
- ASP.NET Core Web API (.NET 9)
- Entity Framework Core + SQL Server
- FluentValidation
- SignalR (real-time chat)
- JWT authentication

**Frontend**
- Blazor WebAssembly
- MudBlazor UI component library

**Integrations**
- Mapbox – map rendering and location search
- Google AI – image moderation
- Mailtrap – email delivery (development)

## Features

- Registration with email verification
- Create and manage events with location, age restrictions and gender restrictions
- Location-based event search with radius filtering
- Real-time chat per event (SignalR)
- Image uploads for profile and events
- Role-based participant management (host / participant)

## Architecture

The project follows **Vertical Slice Architecture** — each feature is self-contained with its own endpoint, handler and validator.

```
Meeps/
├── API/          # Backend – endpoints, handlers, validators
├── Client/       # Blazor WebAssembly – pages and components
└── Shared/       # Shared contracts (DTOs, Result pattern, error codes)
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- SQL Server LocalDB

### Installation

1. Clone the repository
2. Set up User Secrets in the `API/` folder:
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=MeepsDb;Trusted_Connection=True"
   dotnet user-secrets set "Jwt:Key" "your-secret-key-at-least-32-characters-long"
   ```
3. Apply database migrations:
   ```bash
   cd API
   dotnet ef database update
   ```
4. Start the project:
   ```bash
   .\watch.bat
   ```

The app runs at **https://localhost:7000**
