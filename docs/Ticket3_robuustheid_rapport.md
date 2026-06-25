# Robuustheid en schaalbaarheid - OpenMRS Communicatiemodule

## Inleiding

Dit document legt uit hoe de communicatiemodule omgaat met fouten, wat er live te meten valt, en welke concrete stappen er zijn gezet om de software betrouwbaarder te maken. De analyse sluit direct aan op de opgeleverde code en architectuur.

---

## 1. Hoe de applicatie faalt - en hoe ze dat opvangt

De FMEA (zie bijlage) analyseert de risico's per component. Hieronder wordt per relevant risico beschreven hoe de code dat in de praktijk afhandelt.

### Webhook-ontvangst

OpenMRS stuurt een HTTP POST zodra een arts een afspraak aanmaakt. Als dat verzoek binnenkomt, doet de backend drie dingen voordat er ook maar iets in de database belandt: het controleert of de `X-OpenMRS-Organization-Id` header aanwezig is, of de HMAC-SHA256 handtekening klopt met het geheime sleutel van die specifieke organisatie, en of het event-ID al eerder verwerkt is.

Als de handtekening niet klopt, stuurt de applicatie een `401` terug en logt hij de weigering. Er wordt niets opgeslagen. Het event bestaat voor de module simpelweg niet.

Naast de handtekening is er een rate limiter actief op de auth-endpoints (`AuthPolicy`, geconfigureerd in `Program.cs`). Bij meer dan 5 verzoeken per minuut van hetzelfde IP geeft de API een `429 Too Many Requests` terug. Dit is getest in een live Docker-omgeving: de eerste 5 verzoeken gaan door, de zesde niet.

### Afspraakverzending via de queue

Zodra een webhook geaccepteerd is, slaat de module de *intentie* op in PostgreSQL, niet meer dan dat. Wanneer een herinnering verstuurd moet worden, pakt een achtergrondwerker (`ReminderWorker`) die intentie op en zet hij een `SendReminderCommand` op RabbitMQ.

De consumer die dat bericht oppakt (`SendReminderConsumer`) kijkt als eerste of de herinnering al eerder succesvol verstuurd is:

```csharp
if (await reminderLogRepository.AlreadySentAsync(cmd.EncounterId, cmd.ReminderWindow, ct))
    return;
```

Dit voorkomt dubbele berichten bij een retry. De consumer voert de logica maar één keer echt uit, ongeacht hoeveel keer RabbitMQ het bericht aanbiedt.

### Providerfouten

Als de externe provider (bijv. SwiftSend of FakeComWorld) een fout teruggeeft, onderscheidt de code twee gevallen:

- **Tijdelijke fout** (timeout, 429, 5xx, verbindingsprobleem): MassTransit plaatst het bericht terug op de queue met exponential backoff. De configuratie in `Program.cs` is: 3 pogingen, startend bij 2 seconden, oplopend tot maximaal 30 seconden.
- **Permanente fout** (400, patiënt niet gevonden, geen contactgegevens): de status wordt direct op `SEND_PERMANENT` gezet. Er wordt niet opnieuw geprobeerd.

```csharp
cfg.UseMessageRetry(r =>
    r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
```

Er is bewust gekozen om *niet* automatisch te switchen naar een andere provider bij een fout. Dit staat beschreven in ADR-0019: het wisselen van provider kan patiëntvoorkeuren schenden en bemoeilijkt de audittrail.

### Multi-tenant isolatie

De module bedient meerdere ziekenhuizen tegelijk. Elke organisatie heeft een eigen webhook-secret. Eerder was er een bug waarbij de `HttpClient` sessie-cookies deelde tussen organisaties die op dezelfde host draaiden (maar op verschillende poorten). Omdat cookies geen poortnummer bijhouden, lekte de `JSESSIONID` van organisatie A naar organisatie B, wat resulteerde in een `401` bij het ophalen van patiëntdata.

Dit is opgelost door `UseCookies = false` te zetten op de gedeelde `HttpClient`. De client gebruikt uitsluitend Basic auth per verzoek. Er is een regressietest geschreven die bevestigt dat cookies niet worden meegestuurd, en die ook verifieert dat de test zichzelf niet valideert door ook het tegenovergestelde scenario te testen met een client mét cookies.

---

## 2. Wat er live te meten valt

Grafana draait op de infrastructuur met een kant-en-klaar dashboard (`reminder-pipeline.json`). Het dashboard ververst elke 15 seconden en toont standaard de afgelopen 60 minuten.

Wat er live zichtbaar is:

- **Verzonden herinneringen**: opgesplitst per venster (24u / 1u) en per succes/fout, via de Prometheus counter `messaging_reminders_sent_total`.
- **Verzendsnelheid**: via `rate(messaging_reminders_sent_total[5m])`. Pieken of dalingen in dit getal wijzen op problemen bij de provider of de worker.
- **Uitgaande HTTP-calls**: gesplitst per HTTP-statuscode. Als er plotseling veel `429` of `503` responses van OpenMRS of een provider binnenkomen, is dat direct zichtbaar.
- **RabbitMQ queue-diepte**: via de RabbitMQ Prometheus-plugin. Als berichten zich ophopen en niet geconsumeerd worden, duidt dat op een vastgelopen consumer.
- **Rate-limiting activiteit**: inkomende 429-responses op de auth-endpoints zijn zichtbaar in het dashboard.

Prometheus scrapt de API elke 15 seconden via `/metrics`. RabbitMQ is als aparte scrape-target toegevoegd in `prometheus.yml`.

---

## 3. Wat er de afgelopen periode is verbeterd

### Van polling naar webhooks

De applicatie haalde eerder patiëntdata proactief op via REST-calls naar OpenMRS (een pull-model). Dit betekende dat de module regelmatig inlogde bij OpenMRS, data opvroeg, en lokaal opsloeg, ook als er niets te sturen viel. Die aanpak is volledig vervangen door een push-model: OpenMRS stuurt een webhook zodra er een afspraak is. De module reageert alleen als er iets te verwerken valt. De volledige `OpenMrsController.cs` (163 regels) en bijbehorende servicemethoden zijn verwijderd.

### Cookie-isolatie fix

De bug waarbij sessie-cookies lekten tussen organisaties is gevonden via een mislukte integratiepoll van de tweede organisatie die steeds een `401` gaf. Nadat de oorzaak gevonden was (`UseCookies` stond standaard op `true`), is de fix één regel code. De bijbehorende test (`OpenMrsHttpClientCookieTests`) borgt dat dit niet opnieuw kan binnensluipen.

### Rate limiting getest in productie-achtige omgeving

De rate limiter stond al in de code, maar was nooit getest buiten een lokale omgeving zonder database. In een volledige Docker-stack (met PostgreSQL en RabbitMQ live) zijn 8 opeenvolgende login-pogingen verstuurd. Verzoeken 1 t/m 5 kregen een `400` (ongeldige credentials, logisch). Verzoeken 6, 7 en 8 kregen een `429`. De limiet werkt.

### Ongebruikte endpoints verwijderd

Endpoints voor retry en template-updates (`RemindersController`) en proxy-endpoints voor patiënten en locaties (`OpenMrsController`) zijn verwijderd. Ze waren nooit in gebruik genomen. Minder code betekent minder aanvalsoppervlak en minder onderhoud.

### Testdekking uitgebreid

De testsuite dekt nu:
- AES-256-GCM encryptie van gevoelige velden
- JWT-autorisatie en horizontale toegangsisolatie
- Webhook HMAC-validatie, timestamp-replay-bescherming en idempotentie
- Retry-ledger gedrag (wachttijden, dead-letter overgangen)
- Multi-tenant webhook-isolatie (organisatie A kan geen webhooks namens organisatie B smeden)
- HTTP cookie-isolatie tussen organisaties

---

## 4. Openstaand punt

Bij afmelding van een afspraak in OpenMRS stuurt het systeem een webhook met status `cancelled`. De module stopt dan met het versturen van herinneringen. Er is echter een klein tijdvenster (een "race condition") waarbij de annulering-webhook binnenkomt terwijl de consumer al bezig is het bericht te versturen. In dat geval krijgt de patiënt toch een SMS.

De kans hierop is laag, maar niet nul. Een "just-in-time" statuscheck bij OpenMRS vlak vóór het versturen zou dit oplossen. Die check is nog niet gebouwd. Het staat op de backlog als verbeterpunt voor een volgende iteratie.
