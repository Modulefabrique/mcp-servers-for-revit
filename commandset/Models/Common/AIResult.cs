namespace RevitMCPCommandSet.Models.Common;

public class AIResult<T>
{
    /// <summary>
    ///     Of de bewerking is geslaagd
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Bericht
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    ///     Geretourneerde gegevens
    /// </summary>
    public T Response { get; set; }
}