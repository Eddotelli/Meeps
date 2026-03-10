# Så Funkar Token-Hanteringen i Meeps - Enkel Förklaring

**För redovisning och förståelse**

---

## 🎯 Grundidén (Super Enkelt)

Tänk dig tokens som två nycklar:

1. **Access Token** = Entrébiljett till en konsert (giltig 15 minuter)
2. **Refresh Token** = Årskort som låter dig få nya entrébiljetter (giltigt 30-60 dagar)

När entrébiljetten går ut, använder du årskortet för att få en ny biljett automatiskt - utan att köa vid entrén igen!

---

## 🏗️ Vad är BFF (Backend-for-Frontend)?

### Problemet Utan BFF:

```
Blazor WebAssembly (körs i webbläsaren)
├── Kan inte säkert lagra tokens (JavaScript kan läsa localStorage)
├── XSS-attacker kan stjäla tokens
└── Vi måste skicka token i varje request (manuellt arbete)
```

### Lösningen Med BFF:

```
Backend hanterar ALLT med tokens
├── Tokens i HttpOnly cookies (JavaScript kan INTE läsa)
├── Automatisk validering vid varje request
└── Client behöver inte ens veta att tokens finns!
```

**Resultat:** Blazor gör vanliga API-calls, backend sköter resten magiskt! 🎩✨

---

## 🔐 De Två Token-Typerna (Detaljerat)

### Access Token (JWT)

**Vad är det?**

- En krypterad fil som innehåller info om vem du är
- Innehåller: User ID, Email, DisplayName, Roller
- Signerad med en hemlig nyckel (bara backend kan skapa äkta tokens)

**Varför så kort livslängd (15 min)?**

- Om någon stjäl den kan de bara använda den i 15 minuter
- Sen blir den värdelös automatiskt
- Säkrare än långa tokens!

**Hur används den?**

```
Client: GET /api/profile
Backend: Läser AccessToken-cookie → Validerar → Tillåter request
```

### Refresh Token

**Vad är det?**

- En långlivad slumpsträng (som ett lösenord)
- Sparas BÅDE i cookie OCH i databasen
- Används BARA för att få nya access tokens

**Varför två olika tokens?**

- Access token skickas med VARJE request (risk att avlyssnas)
- Refresh token används SÄLLAN (bara när access token går ut)
- Om access token stjäls → skadan är begränsad (15 min)
- Om refresh token stjäls → vi upptäcker det (reuse detection)

---

## 📊 Hela Flödet Steg-för-Steg

### 1️⃣ **REGISTRERING**

```
User → Fyller i email
     → Klickar "Register"
Backend → Skickar verifieringsmail
       → Väntar på att user klickar länken

User → Klickar länk i mail
     → Fyller i lösenord + profilinfo
     → Klickar "Complete Registration"

Backend → Skapar Access Token (15 min)
       → Skapar Refresh Token (30 dagar, RememberMe=true)
       → Sparar Refresh Token i databasen
       → Sätter BÅDA i HttpOnly cookies
       → User är inloggad! ✅
```

### 2️⃣ **LOGIN**

```
User → Fyller i email + lösenord
     → Kryssar i "Keep Me Logged In" (eller inte)
     → Klickar "Login"

Backend → Validerar lösenord
       → Skapar Access Token (15 min)
       → Skapar Refresh Token:
          • 30 dagar om "Keep Me Logged In" EJ ikryssad
          • 60 dagar om "Keep Me Logged In" ikryssad
       → Sparar Refresh Token i databasen med RememberMe-flagga
       → Sätter båda tokens i cookies
       → User är inloggad! ✅
```

**Varför "Keep Me Logged In"?**

- Utan: Token går ut efter 30 dagars inaktivitet
- Med: Token går ut efter 60 dagars inaktivitet
- Val till användaren = bättre UX!

### 3️⃣ **NORMAL ANVÄNDNING**

```
User → Klickar på "My Profile"

Client → GET /api/users/me
      → (skickar cookies automatiskt)

Backend → Läser AccessToken från cookie
       → Validerar token:
          ✓ Signatur OK?
          ✓ Inte utgången?
          ✓ Rätt Issuer/Audience?
       → ✅ Tillåter request
       → Returnerar profildata

Client → Visar profil för användaren
```

**Magic:** Client behöver inte göra något speciellt - cookies skickas automatiskt!

### 4️⃣ **AUTOMATISK TOKEN REFRESH** (Det Smarta!)

```
Middleware (körs INNAN varje request)
├── Kollar: Har Access Token < 5 min kvar?
│   ├── NEJ → Låt requesten fortsätta
│   └── JA → Refresha innan det är för sent!
│
└── Refresh-process:
    1. Hämtar Refresh Token från cookie
    2. Validerar mot databasen:
       ✓ Finns den?
       ✓ Inte revokerad?
       ✓ Inte utgången?
    3. Skapar NYA tokens (access + refresh)
    4. Revokar GAMLA Refresh Token i databasen
    5. Sparar nya tokens
    6. Sätter nya cookies
    7. Requesten fortsätter med ny token! ✅
```

**User upplever:** Ingenting! Allt händer i bakgrunden.  
**Resultat:** User blir ALDRIG utloggad (så länge de är aktiva).

#### 🛡️ Race Condition Protection

**Problemet:**

```
User har 3 tabs öppna → alla ser att token snart går ut
→ ALLA försöker refresha samtidigt
→ Skapar 3 nya refresh tokens
→ Token 1 revokar Token 2 och 3
→ Requests misslyckas!
```

**Vår Lösning:**

```csharp
// Endast EN refresh per user åt gången
var userLock = _refreshLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

if (await userLock.WaitAsync(TimeSpan.Zero)) // Non-blocking
{
    // Första requesten får refresha
    await RefreshTokens();
}
else
{
    // Andra requesten skippar (första hanterar det)
    Continue();
}
```

**Plus:** Token Freshness Check

```csharp
// Om token är < 30 sekunder gammalt, skippa refresh
// (betyder att någon annan just refreshat)
if (tokenAge < TimeSpan.FromSeconds(30))
    return false;
```

### 5️⃣ **SLIDING EXPIRATION** (Håller Aktiva Användare Inloggade)

```
Scenario: User med RememberMe (60 dagar)
├── Dag 1: Login → Token går ut Dag 61
├── Dag 54: Token har 7 dagar kvar
├── User gör en request...
│
└── Sliding Expiration triggas:
    ├── "Token snart utgången men user är aktiv!"
    ├── Skapar ny token som går ut Dag 114 (60 dagar från NU)
    ├── Kollar absolut max (90 dagar från Dag 1 = Dag 91)
    └── User får 60 nya dagar! 🎉

VIKTIGT: Absolut max = 90 dagar från första inloggning
         → Förhindrar oändlig förlängning
         → Efter 90 dagar MÅSTE user logga in igen
```

**Logik:**

```csharp
if (refreshToken.ExpiresAt < DateTime.UtcNow.AddDays(7))
{
    // Token går ut inom 7 dagar → Förläng!
    newExpiry = DateTime.UtcNow.AddDays(60); // Eller 30, beroende på RememberMe
}
else
{
    // Token har gott om tid → Behåll samma expiry
    newExpiry = refreshToken.ExpiresAt;
}

// Enforce absolute max (konfigurerbart, default 90 dagar från skapande)
var absoluteMaxDays = int.Parse(_configuration["Jwt:RefreshTokenAbsoluteMaxDays"]!);
var absoluteMaxExpiry = refreshToken.CreatedAt.AddDays(absoluteMaxDays);

if (newExpiry > absoluteMaxExpiry)
{
    newExpiry = absoluteMaxExpiry; // Cappa vid absolut max
}
```

**Resultat:** Användare som använder appen regelbundet behöver ALDRIG logga in igen!

### 6️⃣ **LOGOUT**

```
User → Klickar "Logout"

Client → POST /api/auth/logout

Backend → Hittar Refresh Token i cookie
       → Hittar token i databasen
       → Sätter IsRevoked = true
       → Tar bort båda cookies
       → ✅ User är utloggad

Client → Redirectar till login-sidan
```

**Viktigt:** Token finns kvar i databasen (för audit trail) men är revokerad.

### 7️⃣ **TOKEN REUSE DETECTION** (Säkerhet!)

**Scenariot:**

```
Hacker stjäl User's Refresh Token
└── User refreshar → får Token B (Token A revokeras)
└── Hacker försöker använda Token A
    └── Backend upptäcker: "Token A är revokerad men försöker användas!"
```

**Respons:**

```csharp
if (refreshToken.IsRevoked)
{
    // 🚨 SÄKERHETSVARNING! 🚨
    _logger.LogWarning("Revoked token reuse detected!");

    // Revokera ALLA tokens för denna user
    RevokeAllUserTokens(userId);

    // Tvinga re-autentisering
    return Unauthorized();

    // TODO: Skicka email till user om misstänkt aktivitet
}
```

**Varför så drastiskt?**  
En revoked token som återanvänds = stark indikation på stöld. Bättre att vara säker än ledsen!

---

## 🍪 Cookies (Varför Använder Vi Dem?)

### HttpOnly Cookie = Supervapen Mot XSS

**Utan HttpOnly:**

```javascript
// JavaScript kan läsa token
const token = localStorage.getItem("token");
// Om XSS-attack → hacker kan stjäla detta
```

**Med HttpOnly Cookie:**

```javascript
// JavaScript KAN INTE läsa cookie
document.cookie; // → "AccessToken=..." syns INTE!
// XSS-attack → hacker får ingenting! 🛡️
```

### Cookie Flags (Säkerhet)

```csharp
new CookieOptions
{
    HttpOnly = true,     // JavaScript kan inte läsa
    Secure = true,       // Endast HTTPS
    SameSite = Strict,   // Skydd mot CSRF
    Expires = tokenExpiry, // Auto-cleanup
    Path = "/"           // Hela appen
}
```

**Varje flagga skyddar mot specifika attacker!**

---

## 🔄 Token Rotation (Varför Byta Token Varje Gång?)

### Utan Rotation:

```
User får Refresh Token A → Använder i 60 dagar
Om stolen Dag 1 → Hacker kan använda i 59 dagar!
```

### Med Rotation:

```
Dag 1: Token A
Dag 8: Token A → Token B (A revokeras)
Dag 15: Token B → Token C (B revokeras)
...
Om Token A stolen Dag 1 → Hacker kan bara använda den 1 gång
→ Vi upptäcker reuse → Revokar ALLT!
```

**Resultat:** Stulna tokens blir snabbt värdelösa! ✨

---

## 🎓 Sammanfattning För Redovisning

### Vad Har Vi Byggt?

1. **BFF-Arkitektur**
   - Backend hanterar alla tokens
   - Blazor (frontend) vet inget om tokens
   - HttpOnly cookies för säkerhet

2. **Två Token-System**
   - Access Token (15 min) för API-anrop
   - Refresh Token (30-60 dagar) för förnyelse

3. **Automatisk Refresh**
   - Middleware som refreshar INNAN token går ut
   - User märker aldrig att det händer
   - Race condition safe med locks

4. **Sliding Expiration**
   - Aktiva användare får automatiskt förlängning
   - Behöver aldrig logga in igen (om aktiva)

5. **Säkerhetsfunktioner**
   - Token rotation vid varje refresh
   - Reuse detection (revokar allt vid misstänkt stöld)
   - HttpOnly cookies (XSS-skydd)
   - HTTPS enforcement
   - Rate limiting

### Varför Är Det Här Bra?

✅ **Säkerhet:** HttpOnly cookies + rotation + reuse detection  
✅ **UX:** Automatisk refresh = aldrig utloggad  
✅ **Modern:** Följer 2026 best practices (GitHub, Discord, LinkedIn)  
✅ **Flexibelt:** "Keep Me Logged In" ger user kontroll  
✅ **Robust:** Race condition protection, error handling

---

## 📖 Vanliga Frågor

### Q: Varför inte bara en lång Access Token?

**A:** Om den stjäls kan hacker använda den länge. Med kort access + lång refresh är skadan begränsad.

### Q: Varför spara Refresh Token i databasen?

**A:** För att kunna revokera den! JWT kan inte revokeras (de är self-contained), men vi kan kolla databasen.

### Q: Vad händer om både Access och Refresh går ut?

**A:** User måste logga in igen. Det är okej - betyder 30-60 dagars inaktivitet.

### Q: Kan user ha flera enheter inloggade?

**A:** Ja! Varje enhet får sin egen Refresh Token. Fungerar oberoende.

### Q: Vad händer vid "Remember Me"?

**A:** RememberMe-flaggan sparas i databasen och bevaras genom alla refreshes. Avgör om token är 30 eller 60 dagar.

---

## 🎬 Exempel-Scenario (Från Start Till Slut)

```
DAG 1 - Registrering:
09:00 → Kenza registrerar sig med kenza@meeps.se
09:01 → Email skickas
09:03 → Kenza klickar länk, fyller i profil
09:04 → Tokens skapas: Access (09:19), Refresh (Mars 27)
        RememberMe = true (default)
        → Inloggad!

DAG 1-60 - Normal Användning:
Varje 15:e minut → Access Token går ut
                 → Middleware refreshar automatiskt
                 → Nya tokens skapas
                 → User märker ingenting

DAG 54 - Sliding Expiration:
Token har 7 dagar kvar (Mars 27)
Kenza använder appen → Sliding triggas
Ny expiry: Maj 25 (60 dagar från nu)
→ Kenza får 60 nya dagar!

DAG 90:
Kenza använt appen regelbundet
→ Fortfarande inloggad (tack vare sliding)

SCENARIO: Inaktivitet
DAG 120:
Kenza inte använt appen på 60+ dagar
→ Refresh Token utgången
→ Måste logga in igen
```

---

## 🔑 Nyckelkoncept Att Komma Ihåg

1. **BFF = Backend sköter tokens, frontend slipper tänka**
2. **HttpOnly = JavaScript kan inte läsa = Säkert**
3. **Rotation = Nya tokens varje gång = Stöld upptäcks**
4. **Sliding = Aktiva users behöver inte logga in igen**
5. **Race Protection = Många tabs fungerar = Robust**

---

**Tips För Redovisning:**

1. Rita flödet på tavlan (login → refresh → sliding)
2. Förklara VARFÖR (inte bara VAD) - säkerheten är nyckeln
3. Jämför med andra appar (GitHub, Discord) - vi är moderna
4. Demo med DevTools: Visa HttpOnly cookies
5. Förklär race condition-problemet och hur vi löste det

**Lycka till!** 🚀
