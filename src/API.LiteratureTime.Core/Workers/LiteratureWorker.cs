namespace API.LiteratureTime.Core.Workers;

using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using API.LiteratureTime.Core.Interfaces;
using API.LiteratureTime.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

public class LiteratureWorker : IHostedService
{
    private readonly ILogger<LiteratureWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private ChannelMessageQueue? _channelMessageQueue;

    private const string KEY_PREFIX = "LIT_V3";
    private const string INDEXMARKER = "INDEX";

    private readonly ActionBlock<Func<Task>> _handleBlock;

    public LiteratureWorker(ILogger<LiteratureWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _handleBlock = new(async f => await f());
    }

    public async Task PopulateIndexAsync()
    {
        _logger.LogInformation("Populating index");

        using var scope = _serviceProvider.CreateScope();
        var cacheProvider = scope.ServiceProvider.GetRequiredService<ICacheProvider>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

        var literatureTimeIndex = await cacheProvider.GetAsync<List<LiteratureTimeIndex>>(
            $"{KEY_PREFIX}:{INDEXMARKER}"
        );

        if (literatureTimeIndex == null)
            return;

        var literatureTimeIndexKeys = memoryCache.Get<List<string>>($"{KEY_PREFIX}:{INDEXMARKER}");
        if (literatureTimeIndexKeys != null)
        {
            foreach (var key in literatureTimeIndexKeys)
            {
                memoryCache.Remove(key);
            }
        }

        var lookup = literatureTimeIndex.ToLookup(t => t.Time);
        literatureTimeIndexKeys = new();
        foreach (IGrouping<string, LiteratureTimeIndex> literatureTimesIndexGroup in lookup)
        {
            var hashes = literatureTimesIndexGroup.Select(s => s.Hash).ToList();
            if (hashes != null)
            {
                memoryCache.Set(literatureTimesIndexGroup.Key, hashes);
                literatureTimeIndexKeys.Add(literatureTimesIndexGroup.Key);
            }
        }

        memoryCache.Set($"{KEY_PREFIX}:{INDEXMARKER}", literatureTimeIndexKeys);

        _logger.LogInformation("Done populating index");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _handleBlock.Post(PopulateIndexAsync);

        using var scope = _serviceProvider.CreateScope();
        var connectionMultiplexer =
            scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

        _channelMessageQueue = connectionMultiplexer.GetSubscriber().Subscribe("literature");
        _channelMessageQueue.OnMessage(message =>
        {
            _logger.LogInformation("Recieved message:{message}", message.Message);

            if (message.Message != "index")
                return Task.CompletedTask;

            _handleBlock.Post(PopulateIndexAsync);

            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channelMessageQueue != null)
            await _channelMessageQueue.UnsubscribeAsync();
    }
}