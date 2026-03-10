# Token-Hantering Policy - Meeps Application

**Version:** 1.0  
**Datum:** Januari 2026  
**Status:** Aktiv

---

## 1. Översikt

Meeps-applikationen använder JWT (JSON Web Tokens) för autentisering och auktorisering i en Backend-for-Frontend (BFF) arkitektur. Detta dokument definierar policies och standarder för säker token-hantering.

---

## 2. Arkitektur

### 2.1 Backend-for-Frontend (BFF) Pattern

**Motivation:** Blazor WebAssembly-applikationer körs i webbläsaren och kan inte säkert lagra tokens i localStorage (XSS-risker). BFF-mönstret flyttar token-hanteringen till backend.

**Implementation:**

- Backend (ASP.NET Core Web API) hanterar ALL token-logik
- Tokens lagras i HttpOnly cookies (ej tillgängliga för JavaScript)
- Client (Blazor WASM) gör vanliga HTTP-requests utan token-medvetenhet
- Backend läser tokens från cookies och validerar automatiskt

---

## 3. Token-Typer

### 3.1 Access Token

**Typ:** JWT (JSON Web Token)  
**Livslängd:** 15 minuter  
**Syfte:** Auktorisera API-anrop  
**Innehåll:**

- User ID
- Email
- DisplayName
- Roller (claims)
- Expiry timestamp

**Lagring:** HttpOnly cookie `AccessToken`

**Säkerhet:**

- Signeras med HS256 (HMAC-SHA256)
- Valideras vid varje API-request
- ClockSkew = 0 (ingen tolerans för utgångna tokens)

### 3.2 Refresh Token

**Typ:** Kryptografiskt säker slumpsträng (256-bit)  
**Livslängd:**

- 30 dagar (standard)
- 60 dagar (med "Keep Me Logged In")

**Syfte:** Förnya access token utan återinloggning  
**Lagring:**

- HttpOnly cookie `RefreshToken`
- Database (RefreshTokens-tabell) med metadata

**Säkerhet:**

- Revokeras vid användning (token rotation)
- Reuse detection (revokar alla tokens vid misstänkt stöld)
- Kan revokeras manuellt vid utloggning

### 3.3 Password Reset Token

**Typ:** Kryptografiskt säker slumpsträng (256-bit)  
**Livslängd:** 1 timme  
**Syfte:** Återställa användares lösenord via email-verifiering  
**Lagring:**

- Database (User.PasswordResetToken och User.PasswordResetTokenExpiry)
- Lagras direkt på User-objektet

**Säkerhet:**

- Genereras med `RandomNumberGenerator.GetBytes(32)` (kryptografiskt säker)
- Revokeras automatiskt vid användning
- Revokeras automatiskt vid expiry
- Vaga felmeddelanden (anti-enumeration)
- Rate limiting: 5 requests/minut på validering och reset
- IP och User-Agent loggas vid alla försök

**Validation:**

- Token måste matcha exakt mot databas
- Token måste inte ha gått ut (< 1 timme gammalt)
- Returnerar samma vaga felmeddelande oavsett:
  - Token finns inte
  - Token är ogiltig
  - Token har gått ut
  - Användare finns inte

**Anti-Enumeration:**

- `ValidateResetToken` returnerar alltid samma `ErrorCode` vid fel
- Förhindrar attackerare att kartlägga giltiga tokens
- Förhindrar timing-attacks

**Endpoints:**

- `POST /api/auth/forgot-password` - Skickar reset-länk via email
- `POST /api/auth/validate-reset-token` - Validerar token (eager validation)
- `POST /api/auth/reset-password` - Återställer lösenord med giltig token

---

## 4. Cookie-Policy

### 4.1 Security Flags

Alla auth-cookies MÅSTE ha följande flaggor:

```csharp
HttpOnly = true     // Skyddar mot XSS
Secure = true       // Kräver HTTPS (disabled i Testing-miljö)
SameSite = Strict   // Skyddar mot CSRF
Path = "/"          // Gäller hela applikationen
Expires = [token-expiry] // Matchar token-livslängd
```

### 4.2 Cookie-Namn

- `AccessToken` - Innehåller JWT access token
- `RefreshToken` - Innehåller refresh token

**OBS:** Inga andra autentiseringsmetoder tillåts (ingen localStorage, sessionStorage, eller headers).

---

## 5. Token Refresh Policy

### 5.1 Automatisk Refresh (Middleware)

**Trigger:** Access token har < 5 minuter kvar till expiry  
**Process:**

1. Middleware detekterar utgående token
2. Hämtar refresh token från cookie
3. Validerar refresh token mot database
4. Genererar nya tokens (rotation)
5. Revokar gamla tokens
6. Sätter nya cookies
7. Request fortsätter med ny token

**Säkerhet:**

- Race condition protection (endast en refresh per user samtidigt)
- Token freshness check (skippar om token < 30s gammalt)
- Non-blocking (failar inte requesten vid fel)

### 5.2 Manuell Refresh

**Endpoint:** `POST /api/auth/refresh-token`  
**Rate limit:** 10 requests/minut  
**Trigger:** Client kan explicit begära refresh

---

## 6. Token Rotation Policy

### 6.1 Rotation vid Refresh

Vid varje token-refresh MÅSTE:

1. Ny access token genereras
2. Ny refresh token genereras
3. Gammal refresh token markeras som revoked
4. Gammal token länkas till ny via `ReplacedByToken`
5. Nya tokens sparas/sätts

### 6.2 Reuse Detection

Om en revoked token försöker användas:

1. Logga säkerhetsvarning
2. Revokera ALLA aktiva tokens för användaren
3. Tvinga re-autentisering
4. (Framtida: Skicka säkerhetsvarning via email)

**Motivation:** En revoked token som återanvänds indikerar stöld.

---

## 7. Sliding Expiration Policy

### 7.1 Aktiveringsvillkor

Sliding expiration aktiveras när:

- Refresh token har < 7 dagar kvar till expiry
- Användaren är aktiv (gör requests)

### 7.2 Förlängning

När sliding expiration triggas:

- Ny refresh token får FULL livslängd från nu (30 eller 60 dagar)
- Expiry-datum flyttas fram
- RememberMe-flagga bevaras
- **Absolut Maximum Enforcement:** Token kan aldrig förlängas bortom 90 dagar från ursprungligt skapande

**Resultat:** Aktiva användare behöver aldrig logga in igen (inom 90 dagar).

**Konfiguration:**

```json
"Jwt": {
  "RefreshTokenAbsoluteMaxDays": 90
}
```

**Motivering:** Förhindrar att tokens förlängs oändligt genom sliding expiration. Efter 90 dagar måste användaren re-autentisera sig oavsett aktivitet.

---

## 8. "Keep Me Logged In" Policy

### 8.1 Funktionalitet

Checkbox vid login som ger användaren val:

- **Ej ikryssad:** 30 dagars refresh token
- **Ikryssad:** 60 dagars refresh token

### 8.2 Default-beteende

- **Vid Login:** Användaren väljer själv
- **Vid Registrering:** Default `RememberMe = true` (bättre UX)

### 8.3 Bevarande

RememberMe-flaggan MÅSTE bevaras genom:

- Token rotation
- Sliding expiration
- Hela användarens session

---

## 9. Security Policies

### 9.1 JWT Signing

**Algoritm:** HS256 (HMAC SHA-256)  
**Key-krav:**

- Minst 256 bits (32 bytes)
- Lagras i Azure Key Vault (produktion)
- User Secrets (utveckling)
- Roteras var 6:e månad

### 9.2 Validering

Vid varje request valideras:

- ✅ Signature (korrekt signeringsnyckel)
- ✅ Issuer (MeepsAPI)
- ✅ Audience (MeepsClient)
- ✅ Expiry (ej utgången)
- ✅ Not-Before claim (ej för tidig)

### 9.3 Refresh Token Validering

Vid refresh-request valideras:

- ✅ Token finns i database
- ✅ Token ej revoked
- ✅ Token ej expired
- ✅ Användaren finns och är aktiv
- ✅ Användaren ej locked out

### 9.4 Password Reset Token Validering

Vid validate/reset-request valideras:

- ✅ Token finns i database och matchar exakt
- ✅ Token ej expired (< 1 timme gammalt)
- ✅ Användaren finns och är aktiv
- ✅ Vaga felmeddelanden (samma ErrorCode för alla fel-scenarier)
- ✅ IP och User-Agent loggas för säkerhetsövervakning

**Best Practices:**

- Eager validation: Token valideras direkt när reset-sidan laddas
- Returnerar aldrig olika felkoder för invalid vs expired
- Rate limiting förhindrar brute force-attacker
- Token revokas automatiskt efter användning
- Utgångna tokens rensas från databasen

---

## 10. Logging & Audit Policy

### 10.1 Obligatorisk Loggning

MÅSTE loggas:

- ✅ Login-försök (success/fail, IP, User-Agent)
- ✅ Token refresh (user ID, automatic/manual)
- ✅ Token revocation (user ID, anledning)
- ✅ Reuse detection (SECURITY ALERT)
- ✅ Sliding expiration (triggered/skipped)
- ✅ Logout
- ✅ Password reset requests (IP, User-Agent)
- ✅ Password reset token validation (success/fail, IP, User-Agent)
- ✅ Password reset completion (user ID, IP)

### 10.2 Log-Level

- `Information`: Normala operationer (login success, refresh)
- `Warning`: Misslyckade login, utgångna tokens
- `Error`: Token reuse, säkerhetsincidenter

### 10.3 Sensitive Data

FÖRBJUDET att logga:

- ❌ Lösenord (klarttext eller hashade)
- ❌ Token-innehåll (endast metadata)
- ❌ JWT Signing Key

---

## 11. Rate Limiting Policy

### 11.1 Auth Endpoints

```
/api/auth/login                      → 5 requests/minut per IP
/api/auth/refresh-token              → 10 requests/minut per IP
/api/auth/register                   → 5 requests/minut per IP
/api/auth/forgot-password            → 5 requests/minut per IP
/api/auth/validate-reset-token       → 5 requests/minut per IP
/api/auth/reset-password             → 5 requests/minut per IP
```

**Syfte:** Skydd mot brute force-attacker

### 11.2 Undantag

- Testing-miljö: Rate limiting disabled
- Health check endpoints: Ingen rate limiting

---

## 12. Token Cleanup Policy

### 12.1 Database Cleanup

**Frequency:** Varje natt kl 03:00 (BackgroundService)  
**Process:**

- Ta bort tokens äldre än 90 dagar
- Ta bort revoked tokens äldre än 30 dagar

### 12.2 Memory Cleanup

**Frequency:** Var 10:e minut (TokenRefreshMiddleware)  
**Process:**

- Ta bort SemaphoreSlim locks för inaktiva användare (> 10 min)

---

## 13. Miljö-Specifika Regler

### 13.1 Development

- Secure cookie flag = false (HTTP allowed)
- JWT key från User Secrets
- Detaljerad logging

### 13.2 Testing

- Secure cookie flag = false (HTTP allowed)
- Rate limiting disabled
- In-memory database
- Fake email service

### 13.3 Production

- Secure cookie flag = true (HTTPS required)
- JWT key från Azure Key Vault
- Minimal logging (ej sensitive data)
- Real email service

---

## 14. Compliance

### 14.1 Standards

Denna implementation följer:

- ✅ OWASP Top 10 (2023)
- ✅ RFC 7519 (JWT)
- ✅ RFC 6749 (OAuth 2.0 patterns)
- ✅ GDPR (data minimering, user consent)

### 14.2 Best Practices

- ✅ Short-lived access tokens (15 min)
- ✅ Token rotation vid refresh
- ✅ HttpOnly cookies (XSS protection)
- ✅ HTTPS enforcement
- ✅ Rate limiting
- ✅ Comprehensive logging

---

## 15. Incident Response

### 15.1 Vid Misstänkt Token-Stöld

1. Revokera alla tokens för berörda användare
2. Tvinga re-autentisering
3. Logga incident
4. Skicka säkerhetsvarning till användare
5. Analysera loggar för attack-pattern

### 15.2 Vid Key-Kompromittering

1. Rotera JWT signing key OMEDELBART
2. Revokera ALLA tokens i systemet
3. Tvinga re-autentisering för alla användare
4. Analysera omfattning
5. Rapportera till säkerhetsteam

### 15.3 Vid Password Reset Token Missbruk

Om misstänkt brute force eller enumeration upptäcks:

1. Analysera loggar för attack-pattern (IP, User-Agent)
2. Överväg tillfällig blockering av IP-adress
3. Öka rate limiting temporärt om nödvändigt
4. Revokera alla aktiva password reset tokens för berörda användare
5. Skicka säkerhetsvarning till potentiellt berörda användare

**Indicators:**

- Många misslyckade valideringar från samma IP
- Försök att validera många olika tokens
- Rate limit triggers från samma IP
- Sequential token guessing patterns

---

## 16. Ansvar

### 16.1 Backend Team

- Implementera och underhålla token-logik
- Övervaka säkerhet och prestanda
- Uppdatera policies vid behov

### 16.2 DevOps Team

- Säker key management (Azure Key Vault)
- Monitoring och alerting
- Key rotation

### 16.3 Security Team

- Säkerhetsgranskningar (quarterly)
- Penetration testing
- Incident response

---

## 17. Review & Updates

**Review-frekvens:** Var 6:e månad eller vid:

- Säkerhetsincident
- Major framework-uppdatering
- Nya säkerhetshot
- Regulatory changes

**Senaste review:** Januari 2026  
**Nästa review:** Juli 2026

---

## 18. Referenser

- [OWASP JWT Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)
- [OWASP Forgot Password Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html)
- [RFC 7519 - JSON Web Token](https://tools.ietf.org/html/rfc7519)
- [Microsoft - Secure ASP.NET Core Blazor WebAssembly](https://docs.microsoft.com/en-us/aspnet/core/blazor/security/webassembly)
- [BFF Pattern - DuendeSoftware](https://docs.duendesoftware.com/identityserver/v6/bff/)

---

**Godkänd av:** Development Team  
**Datum:** Januari 26, 2026  
**Version:** 1.1

**Ändringshistorik:**

- v1.1 (2026-01-26): Lagt till Password Reset Token Policy (sektion 3.3, 9.4, 15.3)
- v1.0 (2026-01-25): Initial version
