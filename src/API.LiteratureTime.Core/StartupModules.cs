namespace Api.LiteratureTime.Core;

using Irrbloss;
using Irrbloss.Interfaces;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

public class StartupModule : IStartupModule
{
    public void AddStartups(IEndpointRouteBuilder app)
    {
        var redisConnection = app.ServiceProvider.GetRequiredService<RedisConnection>();
        redisConnection.Initalize();
    }
}