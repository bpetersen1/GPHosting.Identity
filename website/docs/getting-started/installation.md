---
sidebar_position: 1
---

# Installation

## Requirements

- .NET 10 SDK or later
- ASP.NET Core 10

## Install the core package

:::note
Not yet published to nuget.org — see [Packages](/docs/#packages) on the intro page. The command
below is what installation will look like once released.
:::

```shell
dotnet add package GPHosting.Identity
```

## Optional packages

Add these depending on how you want to store clients, resources, and grants:

```shell
# Persist clients/resources/grants in a relational database via EF Core
dotnet add package GPHosting.Identity.EntityFramework

# Use ASP.NET Core Identity as your user store
dotnet add package GPHosting.Identity.AspNetIdentity
```

If you're starting out, you don't need either of these — `AddInMemoryClients()` and friends
(covered in the [Quickstart](./quickstart)) are enough to get a working server running locally
with zero database setup.

## Next step

Continue to [Quickstart](./quickstart) for a minimal working token server.
