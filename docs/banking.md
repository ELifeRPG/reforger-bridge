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
| `characterId` | yes | Who acts; a missing one gets `400`. On a deposit it is recorded on the ledger row, not checked against the owner. On a withdrawal or transfer it must be allowed to use the account. |
| `source` | no | 1 to 128 characters, e.g. the ATM's id. Comes back on the ledger row. Anything else gets `400`. |
| `targetNumber` | transfers | The recipient's account number |

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
