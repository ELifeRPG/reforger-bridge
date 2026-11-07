---
icon: lucide/landmark
---

# Bookings

Deposit, withdraw and transfer (`PUT bank-accounts/{number}/deposit`, `…/withdraw`, `…/transaction`) each make
one booking in the Central API. Amounts go in as `amountCents` and come back as display text; see
[Money](money.md).

## What a booking request carries

| Field | Required | Meaning |
|---|---|---|
| `amountCents` | yes | Whole cents, at least 1 |
| `characterId` | yes | Who acts. On a deposit it is recorded on the ledger row, not checked against the owner; a missing one gets `400`. On a withdrawal or transfer it must be allowed to use the account; a missing one gets `403`. |
| `source` | no | 1 to 128 characters, e.g. the ATM's id. Comes back on the ledger row. Anything else gets `400`. |
| `targetNumber` | transfers | The recipient's account number |

And one header:

| Header | Required | Meaning |
|---|---|---|
| `Idempotency-Key` | yes | 1 to 128 characters, unique per booking, e.g. the ATM's id, the server start and a counter. No commas: the mod's generated client sends headers as a comma-separated list. Without one, or with a longer one, the request gets `400`. |

All of these are checked by the Central API; the Bridge passes its answer on.

## What comes back

- The booking response carries a `transactionId`.
- Every ledger row (`GET bank-accounts/{number}/transactions`) carries the `transactionId` of its booking. Both
  rows of a transfer share it.
- Ledger rows also carry the `source` the request sent.

## Conflicts

A `409` carries `code` and `retryable`, passed on from the Central API:

| `code` | `retryable` | Meaning |
|---|---|---|
| `insufficient_balance` | `false` | Nothing was booked. Don't resend. |
| `concurrent_modification` | `true` | Nothing was booked, because another booking on the account committed first. Resending can succeed. |

An absent `retryable` means `false`.

## Resending

The Bridge passes `Idempotency-Key` on to the Central API unchanged. Within 24 hours:

- **The same request with the same key** books nothing and returns the first response, including its
  `transactionId`. After a timeout or a lost answer, resend with the same key: you get either the booking that
  already happened or the one being made now.
- **The same key with a different body** gets `422` with `code: "idempotency_key_reused"` and
  `retryable: false`. Nothing is booked.
- **A refused request** (`409 insufficient_balance`, `403`, `404`, ...) is not remembered, so resending it with
  the same key is evaluated again.

Keys are scoped to the game server. After 24 hours the same key books again.
