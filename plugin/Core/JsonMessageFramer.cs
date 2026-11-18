using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Knipt de binnenkomende tekststroom van één TCP-verbinding op in losse JSON-RPC-berichten.
    ///
    /// Formaat: elk bericht is één regel JSON, afgesloten met "\n". JSON zonder opmaak bevat zelf geen
    /// kale newlines (een newline in een string wordt als \n ge-escaped), dus "\n" is een veilig
    /// scheidingsteken.
    ///
    /// Overgangsvorm: een oudere MCP-server stuurt berichten zonder "\n". Zolang er geen "\n" in de
    /// buffer staat en de buffer eruitziet als een afgerond JSON-object, wordt geprobeerd die als één
    /// bericht te lezen. Zo werken een nieuwe plugin en een oude server (of andersom) tijdens de uitrol
    /// gewoon samen.
    /// </summary>
    internal sealed class JsonMessageFramer
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        // Positie tot waar de buffer al op "\n" is doorzocht, zodat elk stuk maar één keer gescand wordt
        private int _scanFrom;

        /// <summary>
        /// Voegt een binnengekomen stuk tekst toe en geeft alle berichten terug die daarmee compleet zijn.
        /// </summary>
        public List<string> Append(string chunk)
        {
            var messages = new List<string>();
            _buffer.Append(chunk);

            // Nieuw formaat: alle volledige regels
            int lineStart = 0;
            for (int i = _scanFrom; i < _buffer.Length; i++)
            {
                if (_buffer[i] != '\n')
                    continue;

                string line = _buffer.ToString(lineStart, i - lineStart).Trim();
                if (line.Length > 0)
                    messages.Add(line);
                lineStart = i + 1;
            }

            if (lineStart > 0)
                _buffer.Remove(0, lineStart);
            _scanFrom = _buffer.Length;

            // Overgangsvorm: bericht zonder "\n" van een oudere server
            if (_buffer.Length > 0 && EndsWithClosingBrace())
            {
                string candidate = _buffer.ToString().Trim();
                if (IsCompleteJson(candidate))
                {
                    messages.Add(candidate);
                    _buffer.Clear();
                    _scanFrom = 0;
                }
            }

            return messages;
        }

        private bool EndsWithClosingBrace()
        {
            for (int i = _buffer.Length - 1; i >= 0; i--)
            {
                char c = _buffer[i];
                if (char.IsWhiteSpace(c))
                    continue;
                return c == '}';
            }
            return false;
        }

        private static bool IsCompleteJson(string text)
        {
            try
            {
                JToken.Parse(text);
                return true;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false; // nog niet compleet, wachten op meer data
            }
        }
    }
}
