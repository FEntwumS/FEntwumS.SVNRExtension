using System;
using System.Collections.Generic;
using System.Linq;

using FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// Der Schluessel, unter dem sich DIEs einen Abbreviation-Code teilen: Tag,
/// has_children und die Reihenfolge der (Attribut, Form)-Paare.
/// </summary>
/// <remarks>
/// Wertgleichheit ist hier der springende Punkt. Die Abbrev-Tabelle kann damit als
/// <c>Dictionary&lt;AbbrevSignature, int&gt;</c> gefuehrt werden und vergibt Codes
/// ausschliesslich fuer Kombinationen, die im Baum tatsaechlich vorkommen.
/// <c>.debug_abbrev</c> und <c>.debug_info</c> koennen so nicht auseinanderlaufen -
/// ein Verweis auf einen nicht existierenden Abbrev-Code wird unmoeglich.
/// </remarks>
internal sealed class AbbrevSignature : IEquatable<AbbrevSignature>
{
    private readonly AbbrevAttribute[] _attributes;
    private readonly int _hashCode;

    public AbbrevSignature(DwTag tag, bool hasChildren, IReadOnlyList<AbbrevAttribute> attributes)
    {
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));

        Tag = tag;
        HasChildren = hasChildren;
        _attributes = attributes.ToArray();

        var hash = new HashCode();
        hash.Add(tag);
        hash.Add(hasChildren);
        foreach (var attribute in _attributes)
        {
            hash.Add(attribute);
        }
        _hashCode = hash.ToHashCode();
    }

    public DwTag Tag { get; }

    public bool HasChildren { get; }

    public IReadOnlyList<AbbrevAttribute> Attributes => _attributes;

    public bool Equals(AbbrevSignature? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Tag != other.Tag || HasChildren != other.HasChildren) return false;
        if (_attributes.Length != other._attributes.Length) return false;

        for (var i = 0; i < _attributes.Length; i++)
        {
            if (!_attributes[i].Equals(other._attributes[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as AbbrevSignature);

    public override int GetHashCode() => _hashCode;

    public override string ToString() =>
        $"{Tag}{(HasChildren ? " [children]" : string.Empty)}: " +
        string.Join(", ", _attributes.Select(a => a.ToString()));
}
