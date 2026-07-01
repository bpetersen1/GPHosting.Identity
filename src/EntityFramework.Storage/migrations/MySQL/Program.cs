// Copyright (c) GP Hosting. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

// Uses Oracle's MySQL.EntityFrameworkCore driver. Switch to Pomelo.EntityFrameworkCore.MySql
// once a Pomelo 10.x release is available (UseMySQL → UseMySql, add ServerVersion parameter).
using GPHosting.Identity.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cn = builder.Configuration.GetConnectionString("db")!;

builder.Services.AddConfigurationDbContext(options =>
    options.ConfigureDbContext = b =>
        b.UseMySQL(cn, o => o.MigrationsAssembly(typeof(Program).Assembly.FullName)));

builder.Services.AddOperationalDbContext(options =>
    options.ConfigureDbContext = b =>
        b.UseMySQL(cn, o => o.MigrationsAssembly(typeof(Program).Assembly.FullName)));

var app = builder.Build();
app.Run();
