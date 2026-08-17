namespace FEntwumS.SVNRExtension.Sbdp;

/// <summary>
/// Findet die serielle Schnittstelle, an der ein SVNR antwortet.
/// </summary>
/// <remarks>
/// Ein leeres Setting bedeutet "such selbst": Alle Kandidaten werden geoeffnet und mit
/// <see cref="SvnrBootloaderClient.TestCommunication" /> befragt - dasselbe Vorgehen wie in
/// <c>remote_stub.py</c>. Ein gesetztes Setting hat Vorrang und wird nicht geprueft, damit ein
/// Fehlschlag dort eine verstaendliche Meldung ergibt statt einer stillen Suche.
/// </remarks>
public static class SvnrPortLocator
{
    public static ISbdpTransport Open(string? configuredPort)
    {
        if (!string.IsNullOrWhiteSpace(configuredPort))
            return new SerialSbdpTransport(configuredPort);

        var candidates = SerialSbdpTransport.ListCandidatePorts();

        if (candidates.Count == 0)
            throw new IOException(
                "Keine serielle Schnittstelle gefunden. Unter Linux muss der Benutzer in der " +
                "Gruppe 'dialout' sein, unter macOS erscheint das Board als /dev/cu.usbserial-*.");

        foreach (var candidate in candidates)
        {
            SerialSbdpTransport? transport = null;

            try
            {
                transport = new SerialSbdpTransport(candidate);

                if (new SvnrBootloaderClient(transport).TestCommunication()) return transport;

                transport.Dispose();
            }
            catch (Exception)
            {
                transport?.Dispose();
            }
        }

        throw new IOException(
            $"Auf keiner der {candidates.Count} Schnittstellen hat ein SVNR geantwortet. " +
            "Der Port laesst sich unter Tools -> SVNR fest eintragen.");
    }
}
