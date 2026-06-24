# 22. Privacy-safe reminder delivery evidence

Date: 2026-06-03
Status: Accepted

## Context

Operators need evidence that reminders were scheduled, queued, consumed, and sent, but Dutch patient-data handling requires minimal exposure of patient identifiers, contact details, and message content.

## Decision

Store operational delivery evidence on scheduled reminders: queue message id, queued timestamp, consumed timestamp, provider message id, attempt count, status, and error code. Admin reminder APIs expose hashed encounter references instead of plaintext encounter ids and never expose recipients, patient names, contact details, or message bodies.

## Consequences

- Support can verify queue/provider progress without viewing patient content.
- Encounter correlation remains possible through deterministic hashes for authorized operators.
- Message content verification is handled through template tests and synthetic examples, not production payload exposure.
