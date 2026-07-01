// Copyright (c) GP Hosting. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cn = builder.Configuration.GetConnectionString("db")!;

builder.Services.AddIdentityServer()
    .AddConfigurationStore(options =>
        options.ConfigureDbContext = b =>
            b.UseSqlite(cn, o => o.MigrationsAssembly(typeof(Program).Assembly.FullName)))
    .AddOperationalStore(options =>
        options.ConfigureDbContext = b =>
            b.UseSqlite(cn, o => o.MigrationsAssembly(typeof(Program).Assembly.FullName)));

var app = builder.Build();
app.Run();
