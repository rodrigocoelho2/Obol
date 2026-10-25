namespace Obol.Library.Enums;

/// <summary>
/// Keys of the user-facing messages. Use with <c>nameof(TranslationKeys.X)</c>, never a magic string.
/// </summary>
public enum TranslationKeys
{
    /// <summary>Generic title of a validation failure.</summary>
    ValidationProblems,

    /// <summary>A required field is empty.</summary>
    RequiredField,

    /// <summary>Another record already uses the value.</summary>
    AlreadyExists,

    /// <summary>The record does not exist.</summary>
    NoRecordsFound,

    /// <summary>A user name with characters or a length that is not allowed.</summary>
    UserNameInvalid,

    /// <summary>A password shorter than the minimum.</summary>
    PasswordTooShort,

    /// <summary>A password longer than the maximum.</summary>
    PasswordTooLong,

    /// <summary>Wrong user name or password (never says which).</summary>
    WrongCredentials,

    /// <summary>The current password is wrong.</summary>
    WrongCurrentPassword,

    /// <summary>The last admin cannot be deleted.</summary>
    LastAdmin,

    /// <summary>A value outside its allowed range.</summary>
    OutOfRange,

    /// <summary>Trading 212 refused the API key and secret.</summary>
    BrokerKeyRejected,

    /// <summary>Trading 212 could not be reached.</summary>
    BrokerUnreachable,

    /// <summary>Trading 212 asked to slow down.</summary>
    BrokerRateLimited,

    /// <summary>No Trading 212 connection yet.</summary>
    NotConnected,

    /// <summary>A sync is already running.</summary>
    SyncRunning,

    /// <summary>The file isn't a Trading 212 CSV export.</summary>
    CsvInvalid,

    /// <summary>The CSV export had nothing Obol keeps.</summary>
    CsvEmpty,
}
