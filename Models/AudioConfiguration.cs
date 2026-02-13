namespace TraducaoRealtime.Models;

public class AudioConfiguration
{
    public bool UserWantsToHear { get; set; }
    public bool OthersWantToHear { get; set; }
    public string? SelectedOutputDevice { get; set; }
}
