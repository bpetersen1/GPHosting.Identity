---
sidebar_position: 1
---

# Which flow do I need?

If you're new to OAuth/OIDC, the hardest part isn't configuring GPHosting.Identity — it's figuring
out which flow applies to the thing you're building. This page is a lookup table, not an
explanation: find your situation below, then jump straight to its 5-minute setup. For the "why"
behind each flow, see [Flows](/docs/fundamentals/flows).

## By what you're building

| What you're building | Flow | Setup |
|---|---|---|
| A website with server-side code (ASP.NET MVC, Rails, Django, Express, etc.) where users log in | Authorization code (confidential client) | [Authorization code](./authorization-code) |
| A single-page app (React, Angular, Vue) calling an API from the browser | Authorization code + PKCE (public client) | [Authorization code](./authorization-code) |
| A native mobile app (iOS, Android) | Authorization code + PKCE (public client) | [Authorization code](./authorization-code) |
| A backend service, scheduled job, or API calling another API — no user involved at all | Client credentials | [Client credentials](./client-credentials) |
| A smart TV, game console, or CLI tool with no convenient browser (or a shared/limited-input screen) | Device flow | [Device flow](./device-flow) |
| Any of the above, but it also needs to keep working after the user closes the browser/app | Add refresh tokens on top of whichever flow above applies | [Refresh tokens](./refresh-tokens) |

If your situation doesn't fit neatly into one row — most do — the decision tree below covers the
reasoning, including the cases people most often get wrong.

## The three questions that decide it

**1. Is there a real user involved, or is this machine-to-machine?**

No user, no login screen, just one service calling another (or your own backend calling your own
API) → **client credentials**, full stop. Nothing else on this page applies. This is the flow
people most often skip past by accident, then wonder why they're building a login screen for a
cron job.

If there *is* a user, keep going.

**2. Does your app run somewhere you control, where a secret stays private?**

Server-side code you deploy (a web app's backend, a server-rendered MVC app) can hold a client
secret safely — nobody can extract it by reading the browser's network tab or decompiling an app.
That's a **confidential client** → authorization code flow, with a secret.

Anything that ships to the end user's device — a SPA running in the browser, a mobile app, a
desktop app — can't keep a secret. Whatever you put in client-side JavaScript or an app bundle is
readable by anyone. That's a **public client** → authorization code flow *without* a secret, using
PKCE instead (see [PKCE](/docs/security/pkce) for why this is still secure without one).

Same flow either way — the difference is just whether the client has a secret. Don't reach for
implicit flow for a SPA because "it doesn't have a secret so it can't do code flow" — that reasoning
was true before PKCE existed and is now outdated; implicit flow is deprecated for exactly this reason.

**3. Can the device you're building for actually show a browser and receive a redirect?**

This is the one exception to question 2's logic. A smart TV, a game console, or a CLI tool with no
easy way to open a browser (or catch a redirect back) can't do a normal browser-based login. For
these, use **device flow**: the device shows a code, the user enters it on their phone or laptop,
and the device polls for completion. A CLI tool that *can* open a local browser window (many
`gcloud`, `gh`, `aws` style CLIs do this) can use authorization code + PKCE with a loopback
redirect instead — device flow is specifically for when even that isn't practical.

## Adding refresh tokens

Whatever flow you land on above, if the answer to "should this stay logged in / keep working
without the user re-authenticating constantly" is yes, layer [refresh tokens](./refresh-tokens) on
top. It's not a separate flow — it's an add-on to authorization code or device flow.

## Next steps

- [Flows](/docs/fundamentals/flows) — the full conceptual explanation behind each of these
- [Architecture](/docs/fundamentals/architecture) — how clients, resources, and grants fit together
  if you're still building a mental model
