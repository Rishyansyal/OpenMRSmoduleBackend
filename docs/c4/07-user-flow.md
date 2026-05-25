# User Flow — Zorgmedewerker

De stappen die een zorgmedewerker in de webinterface doorloopt voor de belangrijkste taken.

## Globale navigatie

```mermaid
flowchart TD
    classDef start fill:#dcfce7,stroke:#166534,stroke-width:2px,color:#000
    classDef page fill:#dbeafe,stroke:#1e40af,stroke-width:1px,color:#000
    classDef decision fill:#fef3c7,stroke:#92400e,stroke-width:1px,color:#000
    classDef done fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#000

    Start([Open applicatie]):::start
    Login["/login pagina"]:::page
    AuthCheck{JWT geldig?}:::decision
    Dashboard["/dashboard — overzicht"]:::page

    Berichten["/dashboard/berichten"]:::page
    Geschiedenis["/dashboard/geschiedenis"]:::page
    Patienten["/dashboard/patienten"]:::page

    Start --> AuthCheck
    AuthCheck -->|nee| Login
    AuthCheck -->|ja| Dashboard
    Login -->|login/register ok| Dashboard
    Dashboard --> Berichten
    Dashboard --> Geschiedenis
    Dashboard --> Patienten
    Berichten -->|terug| Dashboard
    Geschiedenis -->|terug| Dashboard
    Patienten -->|terug| Dashboard
    Dashboard -->|logout| Login
```

## Flow 1 — Bericht versturen

```mermaid
flowchart TD
    classDef start fill:#dcfce7,stroke:#166534,stroke-width:2px,color:#000
    classDef action fill:#dbeafe,stroke:#1e40af,stroke-width:1px,color:#000
    classDef decision fill:#fef3c7,stroke:#92400e,stroke-width:1px,color:#000
    classDef error fill:#fee2e2,stroke:#991b1b,stroke-width:1px,color:#000
    classDef done fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#000

    Start([Klik 'Berichten' op dashboard]):::start
    LoadProv["GET /api/messages/providers"]:::action
    SelectProv["Selecteer provider<br/>(SwiftSend / SecurePost / LegacyLink / AsyncFlow)"]:::action
    FillForm["Vul ontvangers, type (SMS/email/push) en bericht in"]:::action
    Send["POST /api/messages"]:::action
    Rate{"Rate limit<br/>(10/min)?"}:::decision
    Provider{"Provider call<br/>succesvol?"}:::decision
    Log["Message-log opgeslagen<br/>(provider, type, success, recipient_count)"]:::action
    Result(["Toon resultaat in UI"]):::done
    Err429(["⚠️ 429 — wacht 1 min"]):::error
    ErrProv(["❌ Foutmelding tonen"]):::error

    Start --> LoadProv --> SelectProv --> FillForm --> Send
    Send --> Rate
    Rate -->|nee, limit hit| Err429
    Rate -->|ja| Provider
    Provider -->|ja| Log --> Result
    Provider -->|nee| ErrProv
```

## Flow 2 — Patiënt zoeken en details inzien

```mermaid
flowchart TD
    classDef start fill:#dcfce7,stroke:#166534,stroke-width:2px,color:#000
    classDef action fill:#dbeafe,stroke:#1e40af,stroke-width:1px,color:#000
    classDef decision fill:#fef3c7,stroke:#92400e,stroke-width:1px,color:#000
    classDef done fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#000

    Start([Klik 'Patiënten' op dashboard]):::start
    Input["Typ zoekterm (naam)"]:::action
    Search["GET /api/openmrs/patients?q=..."]:::action
    Fhir["Backend → OpenMRS FHIR R4"]:::action
    List["Toon lijst met patiënten<br/>(naam, telefoon, email)"]:::action
    Detail{Klik op<br/>patiënt?}:::decision
    Get["GET /api/openmrs/patients/{id}"]:::action
    Show(["Toon contactgegevens"]):::done
    Appoint{Klik op<br/>'Afspraken'?}:::decision
    GetApp["GET /api/openmrs/appointments"]:::action
    ShowApp(["Toon aankomende afspraken"]):::done

    Start --> Input --> Search --> Fhir --> List
    List --> Detail
    Detail -->|ja| Get --> Show
    Show --> Appoint
    Appoint -->|ja| GetApp --> ShowApp
```

## Flow 3 — Geschiedenis bekijken

```mermaid
flowchart TD
    classDef start fill:#dcfce7,stroke:#166534,stroke-width:2px,color:#000
    classDef action fill:#dbeafe,stroke:#1e40af,stroke-width:1px,color:#000
    classDef decision fill:#fef3c7,stroke:#92400e,stroke-width:1px,color:#000
    classDef done fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#000

    Start([Klik 'Geschiedenis' op dashboard]):::start
    Load["GET /api/messages/history"]:::action
    Render["Tabel: provider, type, success, sent_at, recipient_count"]:::action
    Async{Async bericht<br/>met trackingId?}:::decision
    Poll["GET /api/messages/status/{trackingId}"]:::action
    UpdateStatus(["Status bijwerken in UI"]):::done
    Done(["Lijst getoond"]):::done

    Start --> Load --> Render
    Render --> Async
    Async -->|ja| Poll --> UpdateStatus
    Async -->|nee| Done
```

## Flow 4 — Login / registratie

```mermaid
flowchart TD
    classDef start fill:#dcfce7,stroke:#166534,stroke-width:2px,color:#000
    classDef action fill:#dbeafe,stroke:#1e40af,stroke-width:1px,color:#000
    classDef decision fill:#fef3c7,stroke:#92400e,stroke-width:1px,color:#000
    classDef error fill:#fee2e2,stroke:#991b1b,stroke-width:1px,color:#000
    classDef done fill:#e0e7ff,stroke:#4338ca,stroke-width:2px,color:#000

    Start([Open /login]):::start
    Choice{Account?}:::decision
    Reg["Vul e-mail + wachtwoord<br/>(min 12 tekens, cijfer+letter)"]:::action
    PostReg["POST /auth/register"]:::action
    Login["Vul e-mail + wachtwoord"]:::action
    PostLogin["POST /auth/login"]:::action
    Rate{"Rate limit<br/>(5/min)?"}:::decision
    Valid{"Credentials<br/>geldig?"}:::decision
    Lock{"Account<br/>gelocked?"}:::decision
    Store["JWT opslaan in localStorage"]:::action
    Redirect(["Redirect → /dashboard"]):::done
    Err429(["⚠️ Te veel pogingen"]):::error
    ErrAuth(["❌ Verkeerde credentials"]):::error
    ErrLock(["🔒 Account 5 min gelocked"]):::error

    Start --> Choice
    Choice -->|nee| Reg --> PostReg --> Store
    Choice -->|ja| Login --> PostLogin --> Rate
    Rate -->|hit| Err429
    Rate -->|ok| Lock
    Lock -->|ja| ErrLock
    Lock -->|nee| Valid
    Valid -->|ja| Store --> Redirect
    Valid -->|nee| ErrAuth
```
