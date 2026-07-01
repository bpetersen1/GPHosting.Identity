// Copyright (c) GP Hosting. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using GPHosting.Identity.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cn = builder.Configuration.GetConnectionString("db")!;

builder.Services.AddConfigurationDbContext(options =>
    options.ConfigureDbContext = b =>
        b.UseNpgsql(cn, o => o.MigrationsAssembly(typeof(Program).Assembly.FullName)));

builder.Services.AddOperationalDbContext(options =>
    options.ConfigureDbContext = b =>
        b.UseNpgsql(cn, o => o.MigrationsAssembly(typeof(Program).Assembly.FullName)));

var app = builder.Build();
app.Run();
