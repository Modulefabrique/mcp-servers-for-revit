using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>
    /// Hulpfuncties voor transacties in de handlers.
    ///
    /// Rollback bij een exception: alle transacties in de commandset staan in een <c>using</c>-blok.
    /// Revit rolt een gestarte, niet-afgeronde transactie bij <c>Dispose</c> automatisch terug, dus een
    /// exception midden in een transactie laat geen halve wijziging achter. <see cref="RollBackIfStarted"/>
    /// is er voor plekken die de transactie expliciet willen afbreken en daarna doorgaan.
    ///
    /// Commit: <see cref="Transaction.Commit"/> gooit geen exception als Revit de wijzigingen alsnog
    /// terugdraait (bijv. door een fout die bij het afronden wordt gevonden), maar geeft dan een andere
    /// status terug. <see cref="CommitOrThrow"/> maakt daar een exception van, zodat een handler niet
    /// "gelukt" meldt terwijl er niets is opgeslagen.
    /// </summary>
    public static class TransactionExtensions
    {
        /// <summary>
        /// Rondt de transactie af en gooit een exception als Revit de wijzigingen niet heeft doorgevoerd.
        /// </summary>
        public static void CommitOrThrow(this Transaction transaction)
        {
            TransactionStatus status = transaction.Commit();
            if (status != TransactionStatus.Committed)
            {
                throw new InvalidOperationException(
                    $"Transactie '{transaction.GetName()}' is niet doorgevoerd (status: {status}); " +
                    "Revit heeft de wijzigingen teruggedraaid.");
            }
        }

        /// <summary>
        /// Draait de transactie terug als die nog loopt; doet niets als die al is afgerond.
        /// </summary>
        public static void RollBackIfStarted(this Transaction transaction)
        {
            if (transaction.GetStatus() == TransactionStatus.Started)
                transaction.RollBack();
        }
    }
}
