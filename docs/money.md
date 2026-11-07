---
icon: lucide/banknote
---

# Money

The mod is written in Enforce Script, which only has 32-bit `int` and 32-bit `float`. A `float` stops being
exact to the cent at about Ŧ130,000, and `int` cents wrap around above Ŧ21,474,836.47. The mod never needs to
calculate with a balance, so it never gets one as a number.

## From the Bridge to the mod: display text

Every money value the Bridge sends is a ready-to-show string that includes the currency. The mod displays it
as it is:

| Route | Fields |
|---|---|
| Bank account summaries (`GET bank-accounts/{number}`, the account lists) | `balanceStr` |
| Booking results (`deposit`, `withdraw`, `transaction`) | `amountStr`, `feeStr`, `newBalanceStr` |
| Ledger rows (`GET bank-accounts/{number}/transactions`) | `amountStr`, `feeStr` |

The format is fixed: a space between thousands, a dot before the cents, a space and `Ŧ` after the number,
and a leading `-` for negative values, e.g. `"1 234 567.89 Ŧ"` or `"-42.00 Ŧ"`. A ledger row's `amountStr` is
signed from the viewing account's side: `-` for `Withdrawn` and `TransferredOut`, `+` otherwise. `kind` is
still there for anything else that depends on the direction, such as colour.

The Bridge formats from the exact decimal the Central API returns, so values of any size show to the cent.
The Central API stays the only place that calculates with money.

## From the mod to the Bridge: integer cents

The mod sends amounts as `amountCents`, a whole number of cents (`12345` for Ŧ123.45). These are amounts the
mod caps itself, such as at the ATM, so a 32-bit `int` holds them. Anything below one cent, including a
missing `amountCents`, is refused with `400`.

A bank's fee terms (`transactionFeeBase`, `transactionFeeMultiplier` from `GET banks`) stay numbers. The ATM
uses them to preview the fee for an amount it caps. The fee that's actually booked comes back in `feeStr`.
