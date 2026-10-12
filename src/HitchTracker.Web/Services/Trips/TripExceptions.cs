namespace HitchTracker.Web.Services.Trips;

/// <summary>Basis voor fouten waarvan de melding veilig aan de gebruiker getoond kan worden.</summary>
public abstract class TripException(string message) : Exception(message);

/// <summary>De rit bestaat niet (of niet meer, na het verlopen van de bewaartermijn).</summary>
public class TripNotFoundException(Guid tripId) : TripException("Deze rit bestaat niet of is verlopen.")
{
    public Guid TripId { get; } = tripId;
}

/// <summary>De gevraagde actie past niet bij de huidige status van de rit, bijv. een afgeronde rit opnieuw starten.</summary>
public class TripStateException(string message) : TripException(message);

/// <summary>De invoer van de gebruiker is ongeldig, bijv. een bestemming buiten het werkgebied.</summary>
public class TripValidationException(string message) : TripException(message);
