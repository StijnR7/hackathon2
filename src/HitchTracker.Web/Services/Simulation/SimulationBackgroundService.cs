using Microsoft.Extensions.Options;

namespace HitchTracker.Web.Services.Simulation;

/// <summary>Laat de gesimuleerde taxi's elke tick een stukje verder rijden.</summary>
public class SimulationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SimulationOptions> options,
    ILogger<SimulationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Taxisimulatie staat uit.");
            return;
        }

        var interval = TimeSpan.FromMilliseconds(options.Value.TickMilliseconds);
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var simulator = scope.ServiceProvider.GetRequiredService<TripSimulator>();
                await simulator.StepAllAsync(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Eén mislukte tick mag de simulatie niet stoppen.
                logger.LogError(ex, "Fout tijdens de taxisimulatie.");
            }
        }
    }
}
