# Meeps API - Postman Collection

Detta directory innehåller Postman collections för testning av Meeps API endpoints.

## 📁 Filer

- `meeps-api.postman_collection.json` - Collection med alla API requests
- `meeps-local.postman_environment.json` - Environment för manuell utveckling
- `meeps-testing.postman_environment.json` - Environment för CI/CD automation

## 🚀 Setup

### 1. Importera till Postman

1. Öppna Postman
2. Klicka "Import" (längst upp till vänster)
3. Välj alla JSON-filerna från denna mapp
4. Collection och environments importeras

### 2. Välj Environment

I Postman, välj environment från dropdown:en i övre högra hörnet:

- **"Meeps - Local"** för manuell testning under development
- **"Meeps - Testing (CI/CD)"** för automatiserade tester

## ⚙️ Environment Configuration

Båda environments är förkonfigurerade för development:

- **Base URL**: `http://localhost:5000` (Dev API, inte https!)
- **Test Email**: `test@meeps.com`
- **Test Display Name**: `testuser123`
- **Test Full Name**: `Test User`
- **Test Password**: `TestPassword123!`
- **Test Birth Year**: `1995`

## 🤖 Automatisk Testning (NYT!)

Collection:en har nu **fullständig automation** - ingen manuell token-hantering behövs längre!

### Nya Test Endpoints (DEBUG only)

API:et har nu test-endpoints som **bara finns i DEBUG builds**:

- `GET /api/test/verification-token/{email}` - Hämta verification token
- `DELETE /api/test/reset-user/{email}` - Radera test user
- `DELETE /api/test/reset-database` - Radera alla test users

⚠️ **Viktigt**: Dessa endpoints finns **INTE** i Release/Production builds!

### Uppdaterat Test Flow

```
0. Reset Test User (Optional) → Radera gammal test user
1. Register                   → Endast email
2. Get Verification Token     → Automatiskt från test endpoint!
3. Verify Email (Optional)    → Kan skippas
4. Complete Registration      → Password + profil, auto-login
5. Login                      → Email + password
5b. Login - Invalid           → Negativt test
6. Refresh Token              → Hämta nytt access token
7. Logout                     → Revokera session
```

### Kör Hela Flödet Automatiskt

I Postman:

1. Välj "Meeps - Testing (CI/CD)" environment
2. Högerklicka på "Auth" folder
3. Välj "Run folder"
4. Klicka "Run Auth"
5. ✅ Alla steg körs automatiskt!

## 🔐 Authentication Flow (Detaljerat)

## 🔐 Authentication Flow (Detaljerat)

Meeps authentication följer dessa steg:

### 0. Reset Test User (Optional)

- **Endpoint**: `DELETE /api/test/reset-user/{email}`
- **Response**: Bekräftelse att user raderades
- **OBS**: Bara tillgänglig i DEBUG builds!

### 1. Register - Skicka Verifieringslänk

- **Endpoint**: `POST /api/auth/register`
- **Body**: `{ "email": "test@meeps.com" }`
- **Response**: Bekräftelse att verifieringsmail skickades
- **OBS**: Endast email skickas här!

### 2. Get Verification Token (Auto)

- **Endpoint**: `GET /api/test/verification-token/{email}`
- **Response**: `{ "token": "..." }`
- **OBS**: Token sparas automatiskt i environment variable!
- **OBS**: Bara tillgänglig i DEBUG builds!

### 3. Verify Email (Optional)

- **Endpoint**: `POST /api/auth/verify-email`
- **Body**: `{ "token": "{{verificationToken}}" }`
- **Response**: Bekräftelse att email är verifierad
- **OBS**: Kan skippas, gå direkt till Complete Registration

### 4. Complete Registration

- **Endpoint**: `POST /api/auth/complete-registration`
- **Body**:
  - `verificationToken` (från steg 2)
  - `displayName` (ex: "testuser123")
  - `fullName` (ex: "Test User")
  - `password` + `confirmPassword`
  - `birthYear` (ex: 1995)
  - `gender` (valfritt)
  - `categoryIds` (valfri array)
- **Response**: Access token + user data + refresh token cookie
- **OBS**: Detta är där du sätter lösenord och profiluppgifter!
- **OBS**: Detta loggar automatiskt in dig!

### 5. Login

- **Endpoint**: `POST /api/auth/login`
- **Body**: `{ "email": "...", "password": "..." }`
- **Response**: Access token + user data + refresh token cookie
- **OBS**: Fungerar bara efter Complete Registration!

### 6. Refresh Token

- **Endpoint**: `POST /api/auth/refresh-token`
- **Body**: Ingen (använder refresh token från cookie)
- **Response**: Nytt access token

### 7. Logout

- **Endpoint**: `POST /api/auth/logout`
- **Headers**: `Authorization: Bearer <access-token>`
- **Response**: Success meddelande + rensar refresh token

## 📋 Använda Collection:en

### Första Gången (Automatisk!)

Nu behöver du **inte längre** kopiera tokens manuellt!

**I Postman:**

1. Välj "Meeps - Testing (CI/CD)" environment
2. Starta API:et lokalt: `cd API && dotnet run`
3. Högerklicka på "Auth" folder i Postman
4. Välj "Run folder"
5. Alla requests körs automatiskt i ordning! ✅

**Eller kör manuellt:**

1. Kör "0. Reset Test User" (om du kört tidigare)
2. Kör "1. Register"
3. Kör "2. Get Verification Token" → Token sparas automatiskt!
4. Hoppa över "3. Verify Email" (optional)
5. Kör "4. Complete Registration" → Auto-login!
6. Nu kan du köra "5. Login", "6. Refresh Token", "7. Logout"

### Efterföljande Inloggningar

När du väl är registrerad:

- Kör bara "5. Login" med email + password

### Testa Token Refresh

1. Se till att du är inloggad
2. Kör "6. Refresh Token"

### Testa Logout

1. Se till att du är inloggad
2. Kör "7. Logout"

## 💡 Testing Tips

### Automatisk Token Hantering ✨

Collection:en automatiskt:

- Hämtar verification token från test endpoint
- Sparar access token vid login/complete registration
- Använder sparade tokens i autentiserade requests
- Uppdaterar token vid refresh
- Rensar token vid logout

### Återställa Test User

**Med Test Endpoint (Rekommenderat):**

- Kör "0. Reset Test User" i Postman

**Manuellt:**

```sql
DELETE FROM Users WHERE Email = 'test@meeps.com';
```

### Vanliga Problem

**Problem**: "Email already registered"

- **Lösning**: Kör "0. Reset Test User" först

**Problem**: "Could not get any response"

- **Lösning**: Se till att API:et körs på `http://localhost:5000`

**Problem**: "Test endpoint not found (404)"

- **Lösning**: API:et måste köras i DEBUG mode. Kör `dotnet run` (inte `dotnet run --configuration Release`)

## 📦 Collection Structure

```
Auth/
├── 0. Reset Test User (Optional) - Radera test user
├── 1. Register - Skicka email
├── 2. Get Verification Token (Auto) - Hämta token automatiskt!
├── 3. Verify Email (Optional) - Kan skippas
├── 4. Complete Registration - Sätt lösenord + profil
├── 5. Login - Email + password
├── 5b. Login - Invalid Credentials (negativt test)
├── 6. Refresh Token - Förnya access token
└── 7. Logout - Revokera session

Events (TODO)/
Categories (TODO)/
```

## 🚀 CI/CD med GitHub Actions

Ett GitHub Actions workflow är inkluderat för automatisk testning!

### Workflow: `.github/workflows/api-tests.yml`

**Triggas vid:**

- Push till `main` eller `develop`
- Pull requests
- Manuellt via "Actions" tab

**Vad händer:**

1. ✅ Setup .NET + Node.js
2. ✅ Installera Newman
3. ✅ Bygga API
4. ✅ Starta API i Testing mode (InMemory DB + Fake Email)
5. ✅ Health check
6. ✅ Kör Newman tests
7. ✅ Generera HTML rapport
8. ✅ Ladda upp rapport som artifact

**Testing Environment:**

- `ASPNETCORE_ENVIRONMENT=Testing`
- InMemory databas (ingen SQL Server)
- FakeEmailService (inga riktiga emails)
- Test endpoints tillgängliga
- Helt automatiskt!

### Visa Test Rapport

Efter workflow kör:

1. Gå till "Actions" tab på GitHub
2. Klicka på senaste workflow run
3. Ladda ner "newman-report" artifact
4. Öppna `newman-report.html` i webbläsare

## 🔧 Development Notes

- **URL**: `http://localhost:5000` (HTTP, inte HTTPS!)
- **Test Endpoints**: Bara i DEBUG builds
- **Fake Email**: Används i Testing environment
- **Real Email**: Kan konfigureras för Development
- **Access Token**: 15 min validity
- **Refresh Token**: 7 dagar validity

## 🚀 Kör Tester via Newman (CLI)

### Lokal Testing

```powershell
# Installera Newman
npm install -g newman newman-reporter-htmlextra

# Starta API först
cd API
dotnet run

# I ny terminal - kör tester
cd Tests/Postman
newman run meeps-api.postman_collection.json -e meeps-testing.postman_environment.json -r cli,htmlextra --reporter-htmlextra-export newman-report.html

# Öppna rapport
start newman-report.html
```

### CI/CD Testing

Workflow:en kör automatiskt Newman i GitHub Actions. Se `.github/workflows/api-tests.yml`.

## 🎯 Best Practices & Säkerhet

### För Development

- ✅ Använd "Meeps - Local" eller "Meeps - Testing" environment
- ✅ Kör API med `dotnet run` (DEBUG mode)
- ✅ Test endpoints tillgängliga
- ✅ Fake email service (inga riktiga emails)

### För CI/CD

- ✅ API startas med `--environment Testing`
- ✅ InMemory databas
- ✅ Newman genererar HTML rapport

### Säkerhet

⚠️ **VIKTIGT**: Test endpoints (`/api/test/*`) finns **BARA** i DEBUG builds!

- Production builds (Release configuration) har **INGA** test endpoints
- `#if DEBUG` kod kompileras bort automatiskt
- Inga säkerhetsrisker i production

## 🎓 Summary - Vad är Nytt

**Implementerat:**

- ✅ `FakeEmailService` - Loggar istället för skickar emails
- ✅ Test endpoints (`/api/test/*`) - Bara i DEBUG
- ✅ Automatisk token-hämtning i Postman
- ✅ Testing environment för CI/CD
- ✅ GitHub Actions workflow
- ✅ Newman HTML rapporter

**Resultat:**

- ✅ **Fullautomatisk testning** - Ingen manuell token-hantering!
- ✅ **CI/CD ready** - Körs i GitHub Actions
- ✅ **Inga riktiga emails** - FakeEmailService i Testing mode
- ✅ **Production säker** - Test endpoints bara i DEBUG
- ✅ **Kan köras obegränsat** - Reset user mellan runs

---

**Senast Uppdaterad**: Januari 17, 2026
