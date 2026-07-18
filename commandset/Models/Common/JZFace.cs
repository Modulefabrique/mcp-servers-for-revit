using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
///     3D-vlak
/// </summary>
public class JZFace
{
    /// <summary>
    ///     Constructor
    /// </summary>
    public JZFace()
    {
        InnerLoops = new List<List<JZLine>>();
        OuterLoop = new List<JZLine>();
    }

    /// <summary>
    ///     Buitenlus (van het type List&lt;List&lt;JZLine&gt;&gt;)
    /// </summary>
    [JsonProperty("outerLoop")]
    public List<JZLine> OuterLoop { get; set; }

    /// <summary>
    ///     Binnenlussen (van het type List&lt;JZLine&gt;, representeert een of meer binnenlussen)
    /// </summary>
    [JsonProperty("innerLoops")]
    public List<List<JZLine>> InnerLoops { get; set; }
}