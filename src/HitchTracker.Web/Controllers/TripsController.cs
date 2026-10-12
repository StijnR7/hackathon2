using HitchTracker.Web.Data;
using HitchTracker.Web.Infrastructure;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services;
using HitchTracker.Web.Services.Routing;
using HitchTracker.Web.Services.Simulation;
using HitchTracker.Web.Services.Trips;
using HitchTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HitchTracker.Web.Controllers;

/// <summary>
/// De drie fases van een rit: schatting (epic 1), live volgen (epic 2) en eindprijs (epic 3).
/// Alle acties die iets wijzigen zijn POST met anti-forgery token (bescherming tegen CSRF).
/// </summary>
[Route("ritten")]
public class TripsController(
    TripService trips,
    TripSimulator simulator,
    PlanTripViewModelFactory planTripViewModels,
    HitchTrackerDbContext db,
    ILogger<TripsController> logger) : Controller
{
    private const string ErrorKey = "Error";

    // ---------------- Epic 1: voor de reis ----------------

    [HttpPost("plannen")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.RoutePlanning)]
    public async Task<IActionResult> Plan([Bind(Prefix = nameof(PlanTripViewModel.Form))] EstimateForm form,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var trip = await trips.PlanTripAsync(
                    form.CityId,
                    new LocationInput(form.PickupPlaceId, form.PickupLat, form.PickupLng),
                    new LocationInput(form.DestinationPlaceId, form.DestinationLat, form.DestinationLng),
                    cancellationToken);
                return RedirectToAction(nameof(Estimate), new { id = trip.Id });
            }
            catch (TripValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        var model = await planTripViewModels.CreateAsync(form.CityId, form, cancellationToken);
        return model is null ? NotFound() : View("~/Views/Home/Index.cshtml", model);
    }

    [HttpGet("{id:guid}/schatting")]
    public async Task<IActionResult> Estimate(Guid id, CancellationToken cancellationToken)
    {
        var trip = await trips.GetTripDetailsAsync(id, cancellationToken);
        if (trip.Status != TripStatus.Planned)
        {
            return RedirectToTripPage(trip);
        }

        return View(await BuildEstimateViewModelAsync(trip, new StartTripForm(), cancellationToken));
    }

    [HttpPost("{id:guid}/start")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.RoutePlanning)]
    public async Task<IActionResult> Start(Guid id, [Bind(Prefix = nameof(TripEstimateViewModel.Form))] StartTripForm form,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && form.TaxiId is { } taxiId)
        {
            try
            {
                var trip = await trips.StartTripAsync(id, taxiId, cancellationToken);
                await StartSimulationAsync(trip, form.SimulateDetour, cancellationToken);
                return RedirectToAction(nameof(Live), new { id });
            }
            catch (TripValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (TripStateException ex)
            {
                TempData[ErrorKey] = ex.Message;
                return RedirectToAction(nameof(Live), new { id });
            }
        }

        var planned = await trips.GetTripDetailsAsync(id, cancellationToken);
        return View(nameof(Estimate), await BuildEstimateViewModelAsync(planned, form, cancellationToken));
    }

    // ---------------- Epic 2: tijdens de reis ----------------

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Live(Guid id, CancellationToken cancellationToken)
    {
        var trip = await trips.GetTripDetailsAsync(id, cancellationToken);
        if (trip.Status != TripStatus.InProgress)
        {
            return RedirectToTripPage(trip);
        }

        var driven = await trips.GetDrivenRouteAsync(id, cancellationToken);
        return View(new LiveTripViewModel { Trip = trip, DrivenRoute = driven });
    }

    [HttpPost("{id:guid}/bericht")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Message(Guid id, [Bind(Prefix = nameof(LiveTripViewModel.Form))] MessageForm form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData[ErrorKey] = FirstModelError();
            return RedirectToAction(nameof(Live), new { id });
        }

        try
        {
            await trips.SendPassengerMessageAsync(id, form.Message, cancellationToken);
            TempData["Success"] = "Je bericht staat nu op het scherm van de chauffeur.";
        }
        catch (Exception ex) when (ex is TripValidationException or TripStateException)
        {
            TempData[ErrorKey] = ex.Message;
        }

        return RedirectToAction(nameof(Live), new { id });
    }

    [HttpPost("{id:guid}/beeindigen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await trips.EndTripEarlyAsync(id, cancellationToken);
        }
        catch (TripStateException ex)
        {
            // Bijv. de taxi was net op de bestemming: dan is de rit al afgerond.
            TempData[ErrorKey] = ex.Message;
        }

        return RedirectToAction(nameof(Summary), new { id });
    }

    // ---------------- Epic 3: na de reis ----------------

    [HttpGet("{id:guid}/eindprijs")]
    public async Task<IActionResult> Summary(Guid id, CancellationToken cancellationToken)
    {
        var trip = await trips.GetTripDetailsAsync(id, cancellationToken);
        if (!trip.IsFinished)
        {
            return RedirectToTripPage(trip);
        }

        // Was de prijs hoger dan geschat, dan staat "melden" alvast aangevinkt.
        var form = new FeedbackForm { ReportOvercharge = trip.IsPriceAboveEstimate };
        return View(await BuildSummaryViewModelAsync(trip, form, cancellationToken));
    }

    [HttpPost("{id:guid}/beoordeling")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(Guid id, [Bind(Prefix = nameof(TripSummaryViewModel.Form))] FeedbackForm form,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await trips.SubmitFeedbackAsync(id, form.Stars, form.Comment, form.ReportOvercharge, cancellationToken);
                TempData["Success"] = form.ReportOvercharge
                    ? "Bedankt. Je melding over de ritprijs is doorgestuurd naar ons klachtenteam."
                    : "Bedankt voor je beoordeling!";
                return RedirectToAction(nameof(Summary), new { id });
            }
            catch (Exception ex) when (ex is TripValidationException or TripStateException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        var trip = await trips.GetTripDetailsAsync(id, cancellationToken);
        return View(nameof(Summary), await BuildSummaryViewModelAsync(trip, form, cancellationToken));
    }

    // ---------------- Chauffeursscherm (demo) ----------------

    [HttpGet("{id:guid}/chauffeur")]
    public async Task<IActionResult> Driver(Guid id, CancellationToken cancellationToken)
    {
        var trip = await trips.GetTripDetailsAsync(id, cancellationToken);
        var driven = await trips.GetDrivenRouteAsync(id, cancellationToken);
        return View(new LiveTripViewModel { Trip = trip, DrivenRoute = driven });
    }

    // ---------------- Hulpmethodes ----------------

    private async Task StartSimulationAsync(Trip trip, bool withDetour, CancellationToken cancellationToken)
    {
        try
        {
            await simulator.CreateAsync(trip, withDetour, cancellationToken);
        }
        catch (RoutingException ex)
        {
            // Zonder omweg-route rijdt de demo-taxi gewoon de geplande route.
            logger.LogWarning(ex, "Omweg voor simulatie van rit {TripId} niet beschikbaar.", trip.Id);
            await simulator.CreateAsync(trip, withDetour: false, cancellationToken);
        }
    }

    private async Task<TripEstimateViewModel> BuildEstimateViewModelAsync(Trip trip, StartTripForm form,
        CancellationToken cancellationToken)
    {
        var taxis = await db.Taxis.AsNoTracking()
            .Include(t => t.Driver)
            .Where(t => t.CityId == trip.CityId && t.IsAvailable)
            .OrderBy(t => t.LicensePlate)
            .ToListAsync(cancellationToken);
        return new TripEstimateViewModel { Trip = trip, AvailableTaxis = taxis, Form = form };
    }

    private async Task<TripSummaryViewModel> BuildSummaryViewModelAsync(Trip trip, FeedbackForm form,
        CancellationToken cancellationToken) =>
        new()
        {
            Trip = trip,
            DrivenRoute = await trips.GetDrivenRouteAsync(trip.Id, cancellationToken),
            Form = form
        };

    private RedirectToActionResult RedirectToTripPage(Trip trip) => trip.Status switch
    {
        TripStatus.Planned => RedirectToAction(nameof(Estimate), new { id = trip.Id }),
        TripStatus.InProgress => RedirectToAction(nameof(Live), new { id = trip.Id }),
        _ => RedirectToAction(nameof(Summary), new { id = trip.Id })
    };

    private string FirstModelError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault()
        ?? "Ongeldige invoer.";
}
