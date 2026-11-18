using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Basisklasse voor alle handlers in de commandset. Regelt centraal de synchronisatie tussen
    /// de socket-thread (het command dat wacht) en Revit's API-thread (de handler die het werk doet),
    /// zodat afzonderlijke handlers alleen nog het eigenlijke Revit-werk bevatten (<see cref="ExecuteCore"/>).
    ///
    /// Abandoned-regel: een verzoek dat een time-out kreeg vóórdat Revit eraan begon, wordt
    /// overgeslagen als Revit het event later alsnog afhandelt (bijv. nadat een openstaand dialoog
    /// is gesloten). Daarmee blijft "time-out" betekenen: niet uitgevoerd. Zonder deze regel werd
    /// bijvoorbeeld een verwijderopdracht die als mislukt was gemeld, later toch uitgevoerd, of kreeg
    /// een volgend verzoek het (halve) resultaat van een eerder verzoek terug.
    ///
    /// Volgorde per verzoek (zie <see cref="Commands.RevitCommandBase"/>):
    /// 1. het command zet de parameters op de handler (de handler is dan niet "armed", dus een
    ///    achtergebleven event van een eerder verzoek voert niets uit met half-ingestelde parameters);
    /// 2. <see cref="Arm"/>: dit verzoek mag precies één keer uitgevoerd worden;
    /// 3. raise + <see cref="WaitForCompletion"/>.
    /// </summary>
    public abstract class WaitableExternalEventHandlerBase : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private enum RequestState
        {
            Idle,     // geen verzoek, of verzoek afgelopen/afgebroken
            Armed,    // verzoek klaargezet, wacht op Revit
            Running,  // Revit voert het verzoek nu uit
            Done      // uitgevoerd, resultaat staat klaar
        }

        private readonly object _sync = new object();
        private readonly ManualResetEvent _completed = new ManualResetEvent(false);
        private RequestState _state = RequestState.Idle;

        /// <summary>
        /// Exception die <see cref="ExecuteCore"/> niet zelf heeft afgevangen, of <c>null</c>.
        /// <see cref="Commands.RevitCommandBase"/> gooit deze opnieuw op de socket-thread, zodat hij als
        /// gewone foutmelding in het antwoord terechtkomt in plaats van bij Revit.
        /// </summary>
        public Exception UnhandledException { get; private set; }

        /// <summary>
        /// Zet een nieuw verzoek klaar. Wordt door <see cref="Commands.RevitCommandBase"/> aangeroepen
        /// vlak vóór het raisen van het externe event, ná het instellen van de parameters.
        /// </summary>
        public void Arm()
        {
            lock (_sync)
            {
                _completed.Reset();
                UnhandledException = null;
                _state = RequestState.Armed;
            }
        }

        /// <summary>
        /// Wordt door Revit op de API-thread aangeroepen. Voert het werk alleen uit als er een
        /// klaargezet verzoek is; een event zonder (nog geldig) verzoek doet niets.
        /// </summary>
        public void Execute(UIApplication app)
        {
            lock (_sync)
            {
                if (_state != RequestState.Armed)
                    return; // verzoek kreeg al een time-out, of dit event is een dubbele afhandeling
                _state = RequestState.Running;
            }

            try
            {
                ExecuteCore(app);
            }
            catch (Exception ex)
            {
                UnhandledException = ex;
            }
            finally
            {
                lock (_sync)
                {
                    _state = RequestState.Done;
                }
                _completed.Set();
            }
        }

        /// <summary>
        /// Wacht tot het verzoek is uitgevoerd. Geeft <c>false</c> als Revit er binnen de time-out
        /// niet aan begonnen is; het verzoek wordt dan afgebroken en later niet meer uitgevoerd.
        /// Is Revit er wél al mee bezig, dan wordt gewacht tot het klaar is: Revit-werk is niet af te
        /// breken, en "mislukt" melden terwijl de wijziging toch doorgaat is precies wat deze klasse
        /// moet voorkomen.
        /// </summary>
        public bool WaitForCompletion(int timeoutMilliseconds)
        {
            if (_completed.WaitOne(timeoutMilliseconds))
                return true;

            lock (_sync)
            {
                switch (_state)
                {
                    case RequestState.Armed:
                        _state = RequestState.Idle; // afgebroken: wordt overgeslagen als Revit het event later afhandelt
                        return false;
                    case RequestState.Idle:
                        return false; // nooit klaargezet
                    case RequestState.Done:
                        return true;  // net op tijd klaar
                }
            }

            // Running: wachten op het echte resultaat
            _completed.WaitOne();
            return true;
        }

        /// <summary>Het eigenlijke Revit-werk van de handler, op Revit's API-thread.</summary>
        protected abstract void ExecuteCore(UIApplication app);

        public abstract string GetName();
    }
}
