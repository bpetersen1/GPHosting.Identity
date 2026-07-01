// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace GPHosting.Identity.EntityFramework.Mappers;
/// <summary>
/// Extension methods to map to/from entity/model for clients.
/// </summary>
public static class ClientMappers
{
    static ClientMappers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddProfile<ClientMapperProfile>());
        Mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    internal static IMapper Mapper { get; }

    /// <summary>
    /// Maps an entity to a model.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns></returns>
    public static Models.Client ToModel(this Entities.Client entity)
    {
        return Mapper.Map<Models.Client>(entity);
    }

    /// <summary>
    /// Maps a model to an entity.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns></returns>
    public static Entities.Client ToEntity(this Models.Client model)
    {
        return Mapper.Map<Entities.Client>(model);
    }
}
