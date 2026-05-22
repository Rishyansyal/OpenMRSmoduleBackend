# Auth API — Frontend Integratie

**Base URL (lokaal):** `http://localhost:5111`

Alle request bodies zijn `application/json`. De token is een **JWT Bearer token** die je in de `Authorization` header meestuurt bij beveiligde endpoints.

---

## 1. Registreren

**`POST /auth/register`**

### Request
```json
{
  "email": "gebruiker@example.com",
  "password": "MinimaalAchtTekens1!"
}
```

| Veld | Verplicht | Validatie |
|---|---|---|
| `email` | ja | geldig e-mailadres |
| `password` | ja | minimaal 8 tekens |

### Response `200 OK`
```json
{
  "token": "eyJhbGci...",
  "expiresAt": "2026-05-23T01:22:03.239551Z"
}
```

### Foutresponses
| Status | Betekenis |
|---|---|
| `400 Bad Request` | validatiefout (e-mail ongeldig of wachtwoord te kort) |
| `409 Conflict` | e-mailadres al in gebruik |

---

## 2. Inloggen

**`POST /auth/login`**

### Request
```json
{
  "email": "gebruiker@example.com",
  "password": "MinimaalAchtTekens1!"
}
```

### Response `200 OK`
```json
{
  "token": "eyJhbGci...",
  "expiresAt": "2026-05-23T01:22:03.239551Z"
}
```

### Foutresponses
| Status | Betekenis |
|---|---|
| `400 Bad Request` | verplichte velden ontbreken |
| `401 Unauthorized` | e-mail of wachtwoord onjuist |

---

## 3. Ingelogde gebruiker ophalen (beveiligd)

**`GET /auth/me`** — vereist geldig JWT token

### Request headers
```
Authorization: Bearer eyJhbGci...
```

### Response `200 OK`
```json
{
  "id": "e43ad01a-d81a-4e70-be90-2d92acf2cefe",
  "email": "gebruiker@example.com"
}
```

### Foutresponses
| Status | Betekenis |
|---|---|
| `401 Unauthorized` | geen of verlopen token |

---

## Frontend implementatie (voorbeeld)

### Token opslaan na login/register
```ts
const res = await fetch('http://localhost:5111/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password }),
});
const { token, expiresAt } = await res.json();
localStorage.setItem('token', token);
```

### Token meesturen bij beveiligde requests
```ts
const token = localStorage.getItem('token');
const res = await fetch('http://localhost:5111/auth/me', {
  headers: { Authorization: `Bearer ${token}` },
});
```

### Token verwijderen bij uitloggen
```ts
localStorage.removeItem('token');
```

---

## Token formaat (JWT claims)

De JWT bevat de volgende claims (zichtbaar via [jwt.io](https://jwt.io)):

| Claim | Waarde |
|---|---|
| `sub` (NameIdentifier) | user UUID |
| `email` | e-mailadres van de gebruiker |
| `exp` | verloopdatum (Unix timestamp) |
| `iss` | `OpenMRSmoduleBackend` |
| `aud` | `OpenMRSmoduleBackend` |

Standaard geldigheid: **60 minuten**.
