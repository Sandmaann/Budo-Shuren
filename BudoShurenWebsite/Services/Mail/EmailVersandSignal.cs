namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>
    /// Weckt den Hintergrundversand auf, damit neu eingereihte Mails nicht bis zur nächsten Abfrage warten.
    /// Mehrere Anstöße vor dem nächsten Durchlauf werden zu einem zusammengefasst.
    /// </summary>
    public sealed class EmailVersandSignal
    {
        private readonly SemaphoreSlim _signal = new(0, 1);

        public void Ausloesen()
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Anstoß steht schon aus
            }
        }

        /// <summary>Wartet auf einen Anstoß, höchstens bis zum Ablauf von maximal.</summary>
        public Task WartenAsync(TimeSpan maximal, CancellationToken abbruch) =>
            _signal.WaitAsync(maximal, abbruch);
    }
}
