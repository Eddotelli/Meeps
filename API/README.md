# Meeps API

Modern ASP.NET Core Web API för Meeps event-plattform.

## 🏗️ Arkitektur

- **Minimal API** - Modern endpoint-design
- **Feature-Slicing** - Vertikal slice-arkitektur
- **Result Pattern** - Railway-oriented programming
- **REPR Pattern** - Request-Endpoint-Response
- **.NET 8** - Senaste versionen

## 🚀 Kom igång

### Förutsättningar

- .NET 8 SDK
- SQL Server (LocalDB eller full installation)
- Visual Studio 2022 / VS Code / Rider

### Installation

1. **Klona projektet**

   ```bash
   cd API
   ```

2. **Uppdatera appsettings.json**

   Uppdatera följande inställningar:

   - `ConnectionStrings:DefaultConnection` - Din SQL Server connection string
   - `Jwt:Key` - En stark hemlig nyckel (minst 32 tecken)
   - `Email:*` - Mailtrap eller annan SMTP-konfiguration

3. **Skapa databas med migrations**

   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

4. **Kör applikationen**

   ```bash
   dotnet run
   ```

   API:et kommer vara tillgängligt på:

   - HTTPS: https://localhost:7001
   - HTTP: http://localhost:5000
   - Swagger UI: https://localhost:7001

## 📁 Projektstruktur

```
API/
├── Common/                    # Delad infrastruktur
│   ├── Results/              # Result Pattern
│   ├── Errors/               # Centraliserade fel
│   ├── Exceptions/           # Global Exception Handler
│   └── Extensions/           # Helper extensions
│
├── Infrastructure/           # External concerns
│   ├── Data/                # Database
│   │   ├── ApplicationDbContext.cs
│   │   └── Configurations/  # EF Core configurations
│   └── Services/            # Infrastructure services
│       ├── EmailService.cs
│       └── TokenService.cs
│
├── Models/                   # Domain models
│   ├── User.cs
│   ├── Event.cs
│   └── ...
│
├── Shared/                   # Shared contracts
│   └── Enums/
│
└── Features/                 # Feature-sliced endpoints
    └── Auth/
        ├── Register/
        ├── VerifyEmail/
        ├── CompleteRegistration/
        ├── Login/
        ├── RefreshToken/
        └── Logout/
```

## 🔐 Authentication Flow

1. **Register** (`POST /api/auth/register`)

   - Användare anger endast email
   - Verifikationsmail skickas

2. **Verify Email** (`POST /api/auth/verify-email`)

   - Användare klickar på länk i mail
   - Email verifieras

3. **Complete Profile** (`POST /api/auth/complete-profile`)

   - Användare sätter lösenord och fyller i profil
   - JWT tokens genereras

4. **Login** (`POST /api/auth/login`)

   - Användare loggar in med email + lösenord
   - JWT tokens returneras

5. **Refresh Token** (`POST /api/auth/refresh-token`)

   - Client använder refresh token för ny access token

6. **Logout** (`POST /api/auth/logout`)
   - Revokerar alla aktiva refresh tokens

## 🔧 Konfiguration

### JWT Settings

```json
{
  "Jwt": {
    "Key": "your-super-secret-key-at-least-32-characters-long",
    "Issuer": "MeepsAPI",
    "Audience": "MeepsClient",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

### Email Settings (Mailtrap)

```json
{
  "Email": {
    "Host": "sandbox.smtp.mailtrap.io",
    "Port": 2525,
    "Username": "your-username",
    "Password": "your-password",
    "From": "noreply@meeps.com",
    "FromName": "Meeps"
  }
}
```

## 📚 API Endpoints

### Health

- `GET /api/health` - Health check

### Auth

- `POST /api/auth/register` - Registrera med email
- `POST /api/auth/verify-email` - Verifiera email
- `POST /api/auth/complete-profile` - Slutför profil
- `POST /api/auth/login` - Logga in
- `POST /api/auth/refresh-token` - Förnya access token
- `POST /api/auth/logout` - Logga ut (kräver auth)

## 🛠️ Nästa Steg

### Kommande Features

1. **Events** - CRUD för events
2. **Event Filtering** - Filtrera på ålder, kön, kategorier
3. **User Preferences** - Hantera intressen
4. **Recommendations** - Event-förslag baserat på intressen
5. **Event Participants** - Join/leave events
6. **Messages** - Gruppchatt för events
7. **SignalR** - Real-time chat
8. **Image Upload** - Profilbilder och event-bilder
9. **Notifications** - Email/push notifications

## 🧪 Testing

```bash
# Kör alla tester
dotnet test

# Med coverage
dotnet test /p:CollectCoverage=true
```

## 📝 Migrations

```bash
# Skapa ny migration
dotnet ef migrations add MigrationName

# Uppdatera databas
dotnet ef database update

# Ta bort senaste migration
dotnet ef migrations remove

# Återställ databas till specifik migration
dotnet ef database update MigrationName
```

## 🔒 Säkerhet

- ✅ JWT med kort livstid (15 min)
- ✅ Refresh tokens med rotation
- ✅ Email verification
- ✅ Password requirements
- ✅ Account lockout efter 5 misslyckade försök
- ✅ HTTPS enforcement
- ✅ SQL Injection-skydd (EF Core)
- ✅ CORS-konfiguration

## 📄 License

Detta projekt är skapat för skoländamål.
