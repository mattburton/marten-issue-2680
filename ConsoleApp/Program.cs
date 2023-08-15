using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weasel.Core;

using var host = await Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        services.AddMarten(string.Empty);

        services.AddSingleton<IConfigureMarten, Configuration>();

        services.AddHostedService<Migrator>();
    }).StartAsync();

public class CustomStoreOptions : StoreOptions
{
    public CustomStoreOptions()
    {
        Connection("host=localhost;database=postgres;password=password;username=postgres");
        
        AutoCreateSchemaObjects = AutoCreate.None;
        
        Policies.ForAllDocuments(
            x =>
            {
                x.Metadata.CausationId.Enabled = true;
                x.Metadata.CorrelationId.Enabled = true;
            }
        );
    }
}

public class Configuration : IConfigureMarten
{
    public void Configure(IServiceProvider services, StoreOptions options)
    {
        options.Schema.For<TestDocument>().DatabaseSchemaName("test").Identity(x => x.Id);
    }
}

public record TestDocument(Guid Id, string Value);

public class Migrator : IHostedService
{
    private readonly IEnumerable<IConfigureMarten> _martenConfigurators;
    private readonly IServiceProvider _serviceProvider;

    public Migrator(IEnumerable<IConfigureMarten> martenConfigurators, IServiceProvider serviceProvider)
    {
        _martenConfigurators = martenConfigurators;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = new CustomStoreOptions();
        
        foreach (var configurator in _martenConfigurators)
        {
            configurator.Configure(_serviceProvider, options);
        }
        
        var store = new DocumentStore(options);
        
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        
        var schemas = store.Storage.AllSchemaNames();

        foreach (var schema in schemas)
        {
            //do something with schema
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}