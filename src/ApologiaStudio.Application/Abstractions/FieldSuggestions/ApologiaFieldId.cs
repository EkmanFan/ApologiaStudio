using System.Text.RegularExpressions;

namespace ApologiaStudio.Application.Abstractions.FieldSuggestions;

/// <summary>
/// Identity of an Apologia metadata field that machine assistance can address.
/// </summary>
/// <remarks>
/// Deliberately not a closed enum: a new assisted field must not force a change
/// to the suggestion contract. It is not open either — the value has to be
/// canonical, so that two spellings of the same field can never coexist.
///
/// Nothing is normalised silently. <c>"Genre_Form"</c> and <c>" genre_form "</c>
/// are rejected rather than folded into <c>genre_form</c>: quietly accepting a
/// near-miss is how a caller ends up believing it addressed a field it did not.
/// </remarks>
public readonly partial record struct ApologiaFieldId
{
    #region Variables and Constants

    /// <summary>
    /// Canonical value of the only field V1 declares.
    /// </summary>
    public const string GenreFormValue = "genre_form";

    #endregion

    #region Constructors

    public ApologiaFieldId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!CanonicalForm().IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a canonical Apologia field identifier. " +
                "Expected lower snake_case, for example 'genre_form'.",
                nameof(value));
        }

        Value = value;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the canonical field value.
    /// </summary>
    /// <remarks>
    /// Null on <c>default(ApologiaFieldId)</c>, which no constructor can
    /// produce. Contract validation refuses such a value rather than letting it
    /// travel as a field nobody named.
    /// </remarks>
    public string Value { get; }

    /// <summary>
    /// Gets the Genre/Form field.
    /// </summary>
    public static ApologiaFieldId GenreForm { get; } = new(GenreFormValue);

    #endregion

    #region Methods

    public override string ToString() => Value ?? string.Empty;

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$")]
    private static partial Regex CanonicalForm();

    #endregion
}
