using HitchTracker.Web.Services.Trips;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HitchTracker.Web.Infrastructure;

/// <summary>
/// Vertaalt domeinfouten die niet in de controller zelf worden afgehandeld naar de juiste HTTP-status:
/// een onbekende rit wordt een nette 404-pagina in plaats van een serverfout.
/// </summary>
public class TripExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.Result = context.Exception switch
        {
            TripNotFoundException => new NotFoundResult(),
            TripStateException ex => new ConflictObjectResult(new ProblemDetails { Title = ex.Message, Status = 409 }),
            TripValidationException ex => new BadRequestObjectResult(new ProblemDetails { Title = ex.Message, Status = 400 }),
            _ => context.Result
        };
        context.ExceptionHandled = context.Result is not null;
    }
}
