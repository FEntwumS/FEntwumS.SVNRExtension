namespace FEntwumS.SVNRExtension.Sbdp.Constants;

/// <summary>
/// Die Register des SVNR, benannt wie in <c>Assets/target.xml</c>.
/// </summary>
/// <remarks>
/// Die Namensgleichheit ist Absicht: Der Stub muss die GDB-Registernummer aus
/// <c>target.xml</c> auf genau diese Werte abbilden.
/// </remarks>
public enum SvnrRegister
{
    Akku,
    Programmzaehler,
    Befehlsregister,
    Hilfsregister,

    /// <summary>Nur lesbar - die Hardware dekodiert kein Schreibkommando dafuer.</summary>
    AluFlags
}
