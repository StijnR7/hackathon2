using System.Diagnostics;
using HitchTracker.Web.Services;
using HitchTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HitchTracker.Web.Controllers;

public class HomeController(PlanTripViewModelFactory planTripViewModels) : Controller
{
    /// <summary>Epic 1: de reiziger kiest stad, ophaallocatie en bestemming.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int? cityId, CancellationToken cancellationToken)
    {
        var model = await planTripViewModels.CreateAsync(cityId, cancellationToken: cancellationToken);
        return model is null ? Problem("Er zijn nog geen steden ingesteld.") : View(model);
    }

    [HttpGet]
    public IActionResult HowItWorks() => View();

    [HttpGet]
    public IActionResult Privacy() => View();

    // Geen [HttpGet]: bij een fout in een POST wordt deze actie ook met POST opnieuw uitgevoerd.
    public IActionResult HttpStatus(int code) =>
        View("Error", new ErrorViewModel { StatusCode = code, RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
