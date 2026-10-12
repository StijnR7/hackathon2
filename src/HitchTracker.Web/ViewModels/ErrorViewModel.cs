namespace HitchTracker.Web.ViewModels;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int StatusCode { get; set; } = 500;

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public string Title => StatusCode switch
    {
        404 => "Pagina niet gevonden",
        429 => "Even rustig aan",
        400 => "Ongeldig verzoek",
        _ => "Er ging iets mis"
    };

    public string Message => StatusCode switch
    {
        404 => "Deze pagina of rit bestaat niet (meer). Ritgegevens worden na een tijd automatisch verwijderd.",
        429 => "Je hebt in korte tijd veel routes opgevraagd. Probeer het over een minuut opnieuw.",
        400 => "Het verzoek kon niet worden verwerkt. Laad de pagina opnieuw en probeer het nog eens.",
        _ => "Er is een onverwachte fout opgetreden. Probeer het later opnieuw."
    };
}
