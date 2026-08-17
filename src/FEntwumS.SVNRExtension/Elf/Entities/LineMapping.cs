namespace FEntwumS.SVNRExtension.Elf.Entities;

/// <summary>
/// Ein Schritt im Line Number Program: Zeilen- und Adressvorschub relativ zum
/// vorherigen Eintrag. Entspricht <c>LineMapping</c> aus <c>elfgen.py</c>.
/// </summary>
/// <param name="LineIncrement">
/// Differenz zur vorherigen Quellzeile. Darf negativ sein - wird als SLEB128 kodiert,
/// nicht als einzelnes Byte.
/// </param>
/// <param name="AddressIncrement">Differenz zur vorherigen Adresse, als ULEB128 kodiert.</param>
public readonly record struct LineMapping(int LineIncrement, uint AddressIncrement);
