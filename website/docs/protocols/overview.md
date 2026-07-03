---
sidebar_position: 1
---

# Flows vs. security standards

If you're new to OAuth, it's easy to lump PAR, DPoP, RAR, JARM, and FAPI 2.0 in with "flows" like
authorization code or client credentials — they all sound like the same kind of thing. They're
not, and the distinction matters for understanding what you're turning on.

## A flow answers "how does the client get a token?"

Authorization code, client credentials, and device flow (see
[Which flow do I need?](/docs/quick-setups/which-flow)) are about the handshake itself — what
gets sent, in what order, who's involved. Pick exactly one flow per client, based on what you're
building.

## PAR, DPoP, RAR, JARM answer "how safe is that handshake?"

These aren't flows — they're security add-ons that sit on top of whichever flow you picked. None
of them replace authorization code or client credentials; they change how protected the
request/response traveling through that flow is. Think of the flow as the route a letter travels,
and these as extra security features on the envelope — a wax seal, tamper-evident tape — regardless
of which route it takes.

- **[PAR](./par-and-dpop#pushed-authorization-requests-par-rfc-9126)** — moves the authorization
  request's parameters server-to-server instead of through the browser's URL, so they can't be
  read or tampered with in transit.
- **[DPoP](./par-and-dpop#dpop--demonstration-of-proof-of-possession-rfc-9449)** — protects the
  **token itself** after it's issued. A normal token is like a bus pass: whoever holds it can use
  it. DPoP ties the token to a specific key pair only your app holds, so a stolen token alone is
  useless to an attacker — they'd also need the private key, which never leaves your app.
- **[RAR](./rar)** — replaces flat scope strings ("read orders") with structured data describing
  exactly what's being authorized ("read order #12345 only," "transfer up to $500") — useful
  when a plain scope isn't precise enough.
- **[JARM](./jarm)** — protects the **response coming back from login**. Normally that response
  is plain, unsigned text in a URL — nothing stops it from being tampered with or spoofed. JARM
  wraps it in a JWT signed by the server, so your app can verify the response is genuine before
  trusting anything in it.

**[FAPI 2.0](./fapi2)** isn't a fifth standard — it's a bundle that turns PAR, DPoP, and strict
PKCE on together for a client with one flag, the way a bank or health-data integration would want.

## Why this is worth knowing

None of these are things you need to understand to get a basic login working — the
[quickstart](/docs/getting-started/quickstart) and [5-minute setups](/docs/quick-setups/which-flow)
don't touch any of them. They matter once you're asking "is this secure enough for X" — and
they're also genuinely rare: IdentityServer4 (which GPHosting.Identity modernizes) never
implemented any of them. If you're evaluating identity providers and one of your requirements is
"can this handle FAPI-grade security," this is the part of the docs that answers it.

## Next steps

- [PAR & DPoP](./par-and-dpop) — the two protecting the authorization code exchange
- [RAR](./rar) — fine-grained authorization details
- [JARM](./jarm) — a verifiable login response
- [FAPI 2.0](./fapi2) — bundling all of the above into one profile flag
