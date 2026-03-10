using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Shared.Common.Errors;
using Shared.Common.Results;
using API.Infrastructure.Configuration;

namespace API.Infrastructure.Services;

public class GeminiModerationService : IGeminiModerationService
{
    private readonly GoogleAISettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiModerationService> _logger;

    public GeminiModerationService(
        IOptions<GoogleAISettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<GeminiModerationService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<Result<ModerationResult>> ModerateMessageAsync(string message)
    {
        try
        {
            _logger.LogInformation("🤖 Starting Gemini moderation - Message preview: '{Preview}...'", 
                message.Length > 50 ? message.Substring(0, 50) : message);

            if (string.IsNullOrEmpty(_settings.ApiKey))
            {
                _logger.LogError("Gemini API key is not configured");
                return Result.Failure<ModerationResult>(ModerationErrors.ServiceUnavailable);
            }

            // Build prompt for moderation
            var prompt = BuildModerationPrompt(message);

            // Build request payload
            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json"
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            _logger.LogDebug("Gemini moderation request payload size: {Size} bytes", jsonContent.Length);

            // Make API request
            var url = $"/v1beta/models/gemini-2.5-flash:generateContent?key={_settings.ApiKey}";
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini API moderation request failed: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                return Result.Failure<ModerationResult>(ModerationErrors.ServiceUnavailable);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Gemini moderation response size: {Size} bytes", responseContent.Length);

            // Parse response
            var jsonResponse = JsonDocument.Parse(responseContent);

            // Navigate: candidates[0].content.parts[0].text
            if (jsonResponse.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var textPart = parts[0];
                    if (textPart.TryGetProperty("text", out var textElement))
                    {
                        var moderationJson = textElement.GetString();
                        if (!string.IsNullOrEmpty(moderationJson))
                        {
                            var moderationResult = JsonSerializer.Deserialize<ModerationResult>(
                                moderationJson,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                            );

                            if (moderationResult != null)
                            {
                                var emoji = moderationResult.Severity >= 7 ? "🔴" : moderationResult.Severity >= 4 ? "🟡" : "🟢";
                                _logger.LogInformation(
                                    "{Emoji} Gemini result - Inappropriate: {IsInappropriate}, Severity: {Severity}/10, Category: {Category}, Reason: {Reason}",
                                    emoji,
                                    moderationResult.IsInappropriate,
                                    moderationResult.Severity,
                                    moderationResult.Category ?? "None",
                                    moderationResult.Reason ?? "N/A"
                                );

                                return Result.Success(moderationResult);
                            }
                        }
                    }
                }
            }

            _logger.LogError("Failed to parse Gemini moderation response");
            return Result.Failure<ModerationResult>(ModerationErrors.ServiceUnavailable);
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Gemini moderation request timed out");
            return Result.Failure<ModerationResult>(ModerationErrors.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during message moderation");
            return Result.Failure<ModerationResult>(ModerationErrors.ServiceUnavailable);
        }
    }

    private string BuildModerationPrompt(string message)
    {
        return $@"Du är en avancerad NLP-modell som analyserar chattmeddelanden med flera metoder.

=== NLP-BASERAD ANALYS: REGEL + ML + NEURAL NETWORK-PRINCIPER ===

Meddelande att analysera: ""{message}""

STEG 1: REGEL-BASERAD ANALYS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Negation Detection:
- Upptäck negationer: ""inte"", ""inte okej"", ""borde inte""
- ""Du är INTE dum"" = Positiv negation → OK
- ""Det är INTE okej att behandla andra så"" = Konstruktiv kritik → OK

Linguistic Pattern Matching:
KONSTRUKTIVA MÖNSTER (acceptera):
  • ""Du har varit [negativt] MOT [tredje part]"" → Medling
  • ""Det var [negativt] AV dig att..."" → Konstruktiv kritik
  • ""[Negativt] MEN [lösning]"" → Lösningsorienterat
  • Innehåller: ""be om ursäkt"", ""var snällare"", ""tänk på andra"" → Konstruktiv

DESTRUKTIVA MÖNSTER (varna/blockera):
  • ""Du ÄR [negativt]"" (utan kontext) → Personangrepp
  • ""[Negativt] OCH [mer negativitet]"" → Ackumulerad attack
  • Hot + svordomar → Allvarligt

STEG 2: MACHINE LEARNING-PRINCIPER
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Feature Extraction - Viktning av ord:

HÖGT VIKT (VARNA/BLOCKERA):
  • Isolerade personangrepp: ""Du är dum"" (kort + riktat)
  • Kombinationer: ""dum + idiot + värdelös"" (flera negativa)
  • Hot-mönster: ""slå"", ""döda"", ""förtjänar att..."", ""borde dö""

LÅGT VIKT (ACCEPTERA):
  • Positivt kontext: ""Fan vad kul + roligt + härligt""
  • Tredje part nämnd: ""mot den personen"" (medling)
  • Lösning inkluderad: ""be om ursäkt"", ""var snällare""

Bag of Words Context:
Räkna ord-typer:
  • Konstruktiva: ""ursäkt"", ""förlåt"", ""förstå"", ""bättre"", ""snäll"", ""tänk på""
  • Destruktiva: ""hatar"", ""döda"", ""försvinna"", ""aldrig"", ""ingen gillar""
  • Empatiska: ""känner"", ""ledsen"", ""tråkigt för"", ""förstår""

Klassificering:
  IF (konstruktiva_ord > destruktiva_ord × 2) → Severity 0-2
  IF (tredje_part_nämnd AND lösning_finns) → Severity 0-2
  IF (personangrepp AND INTE lösning) → Severity 5-6
  IF (hot_eller_hatpropaganda) → Severity 7-10

STEG 3: NEURAL NETWORK - SEMANTISK FÖRSTÅELSE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Intent Classification:

CONFLICT RESOLUTION (Severity 0-1):
  • ""Du har varit dum MOT DEN PERSONEN, be om ursäkt""
  • Semantic: Medlande + lösning + empati för tredje part

CONSTRUCTIVE FEEDBACK (Severity 1-2):
  • ""Det var dumt ATT göra så, nästa gång tänk på...""
  • Semantic: Kritik av handling + framåtblickande råd

PERSONAL ATTACK (Severity 5-6):
  • ""Du ÄR dum och ingen gillar dig""
  • Semantic: Identitetsangrepp + social utstötning

Dependency Parsing - Vem gör vad mot vem?
  • ""Du → [handling] → mot tredje person"" = Försvar av offer → OK
  • ""Du → är → [negativt adjektiv]"" = Direkt angrepp → Varna

Svensk Språkförståelse (Transfer Learning):
  • ""Fan också!"" = Besvikelse, INTE destruktiv svordom
  • ""Det var dumt"" = Evaluering av handling, INTE personangrepp
  • ""Lite dum ibland"" = Självdistans/humor, helt OK

Sequence Analysis:
  • ""Du har varit dum MEN jag förstår"" → Negativ + förståelse = Balanserat → OK
  • ""Du är dum OCH ingen bryr sig"" → Negativ + mer negativ = Eskalering → Varna

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INTEGERAD BESLUTSTABELL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Faktorer som MINSKAR severity:
  ✓ Negation detekterad (inverterar sentiment): -2
  ✓ Tredje part nämnd som offer: -3
  ✓ Lösning/råd inkluderat: -2
  ✓ Empatiuttryck (""förstår"", ""ledsen""): -1

Faktorer som ÖKAR severity:
  ✗ Direkt personattack (""Du ÄR""): +4
  ✗ Ackumulerad negativitet (>3 negativa ord): +2
  ✗ Hot eller våld: +6
  ✗ Hatpropaganda: +7

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
EXEMPEL MED NLP-ANALYS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✅ ACCEPTABLA (Severity 0-2):
1. ""Du har varit dum mot den personen idag""
   → Medling + tredje part → Severity 0

2. ""Du har varit dum men den personen och det är inte okej. Be gärna om ursäkt""
   → Konstruktiv medling + lösning + negation → Severity 0

3. ""Fan vad kul!""
   → Positivt uttryck, vardagligt → Severity 0

4. ""Det var dumt gjort""
   → Kritik av handling, inte person → Severity 1

5. ""Jag är lite dum ibland haha""
   → Självdistans/humor → Severity 0

⚠️ VARNINGAR (Severity 4-6):
1. ""Du är dum""
   → Isolerat personangrepp, kort → Severity 4-5

2. ""Du är ganska dum faktiskt""
   → Personangrepp, förstärkt → Severity 5

3. ""Sluta vara så dum hela tiden""
   → Upprepat personangrepp → Severity 6

🛑 BLOCKERA (Severity 7-10):
1. ""Du är dum i huvudet och bör inte få leva""
   → Hot + personangrepp → Severity 9

2. ""Din jävla idiot, jag ska slå dig""
   → Våldshot → Severity 9

3. ""Alla bögar borde försvinna""
   → Hatpropaganda → Severity 10

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
KATEGORIER (använd ENGELSKA)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
- Profanity: Grova svordomar i aggressiv kontext
- Threats: Hot om våld eller skada
- Harassment: Kränkande, mobbande språk
- Sexual Content: Olämpliga sexuella kommentarer
- Hate Speech: Rasism, sexism, diskriminering
- Spam: Upprepat innehåll, reklam

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
VIKTIGT: Prioritera KONTEXT och INTENTION framför enskilda ord!
Använd alla tre metoderna (Regel + ML + Neural) samtidigt.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Svara ENDAST med JSON:
{{
  ""isInappropriate"": true/false,
  ""severity"": 0-10,
  ""category"": ""ENGLISH category"" eller ""OK"",
  ""reason"": ""kort förklaring på svenska""
}}

Severity-riktlinjer:
• 0-3: Acceptabelt (vardagligt språk, konstruktiv kommunikation)
• 4-6: Varning (personangrepp utan hot, nedsättande språk)
• 7-8: Blockera (hot, upprepad trakasseri, hatpropaganda)
• 9-10: Blockera omedelbart (våldshot, dödsönskningar, allvarlig hatpropaganda)";
    }
}
