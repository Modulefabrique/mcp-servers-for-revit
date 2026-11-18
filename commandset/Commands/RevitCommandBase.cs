using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    /// <summary>
    /// Basisklasse voor alle commands in de commandset. Koppelt het command aan een
    /// <see cref="WaitableExternalEventHandlerBase"/> en zorgt dat elk verzoek vóór het raisen
    /// wordt klaargezet (<see cref="WaitableExternalEventHandlerBase.Arm"/>), zodat de abandoned-regel
    /// werkt zonder dat elk command daar zelf aan hoeft te denken.
    /// </summary>
    public abstract class RevitCommandBase : ExternalEventCommandBase
    {
        protected RevitCommandBase(WaitableExternalEventHandlerBase handler, UIApplication uiApp)
            : base(handler, uiApp)
        {
        }

        /// <summary>
        /// Zet het verzoek klaar, raist het externe event en wacht op het resultaat.
        /// Verbergt bewust de (niet-virtuele) methode van <see cref="ExternalEventCommandBase"/>, zodat
        /// bestaande aanroepen in de commands automatisch via deze versie lopen.
        /// Roep dit pas aan nadat alle parameters op de handler zijn gezet.
        /// </summary>
        /// <returns><c>false</c> bij een time-out; het verzoek is dan niet uitgevoerd en wordt ook later niet meer uitgevoerd.</returns>
        protected new bool RaiseAndWaitForCompletion(int timeoutMilliseconds)
        {
            var handler = (WaitableExternalEventHandlerBase)Handler;
            handler.Arm();
            if (!base.RaiseAndWaitForCompletion(timeoutMilliseconds))
                return false;

            if (handler.UnhandledException != null)
                throw new Exception($"{handler.GetName()}: {handler.UnhandledException.Message}", handler.UnhandledException);

            return true;
        }
    }

    /// <summary>
    /// Time-outs per soort command: hoe lang een command wacht tot Revit aan het verzoek begint.
    /// Houd de client-time-out in server/src/utils/SocketClient.ts (CLIENT_TIMEOUT_MS) ruimer dan
    /// de langste waarde hier.
    /// </summary>
    public static class CommandTimeouts
    {
        /// <summary>Gegevens lezen (queries, parameters, weergave-informatie).</summary>
        public const int Read = 60_000;

        /// <summary>Het model wijzigen (aanmaken, wijzigen, verwijderen, taggen).</summary>
        public const int Write = 60_000;

        /// <summary>Zwaar of onvoorspelbaar werk (dynamische code, grote analyses).</summary>
        public const int Heavy = 120_000;
    }
}
