using System.Globalization;
using Obol.Library.Enums;
using Obol.Library.Validation;

namespace Obol.WebApi.Extensions.Globalization;

/// <summary>
/// Translates message keys and field identifiers into user-facing text.
/// </summary>
internal interface IGlobalizationHelper
{
    /// <summary>
    /// Translates a key.
    /// </summary>
    /// <param name="key">A <see cref="TranslationKeys"/> name or a field identifier.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The translated text, or the key itself when there is no translation.</returns>
    Task<string> TranslateAsync(string key, params object[] arguments);

    /// <summary>
    /// Builds a validation error "{field label} {message}".
    /// </summary>
    /// <param name="field">The field identifier.</param>
    /// <param name="messageKey">The message key.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The validation error.</returns>
    Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments);
}

/// <summary>
/// The user-facing messages (English). The app has one language, so the texts live in code rather than in a
/// translation table.
/// </summary>
internal sealed class GlobalizationHelper : IGlobalizationHelper
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    private static readonly Dictionary<string, string> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(TranslationKeys.ValidationProblems)] = "Some of the data sent is not valid.",
        [nameof(TranslationKeys.RequiredField)] = "is required.",
        [nameof(TranslationKeys.AlreadyExists)] = "'{0}' is already taken.",
        [nameof(TranslationKeys.NoRecordsFound)] = "'{0}' does not exist.",
        [nameof(TranslationKeys.UserNameInvalid)] = "must be 3–32 letters, numbers, dots, dashes or underscores.",
        [nameof(TranslationKeys.PasswordTooShort)] = "must have at least {0} characters.",
        [nameof(TranslationKeys.PasswordTooLong)] = "is too long.",
        [nameof(TranslationKeys.WrongCredentials)] = "Wrong user name or password.",
        [nameof(TranslationKeys.WrongCurrentPassword)] = "is wrong.",
        [nameof(TranslationKeys.LastAdmin)] = "The last admin can't be deleted.",
        [nameof(TranslationKeys.OutOfRange)] = "must be between {0} and {1}.",
        [nameof(TranslationKeys.BrokerKeyRejected)] = "Trading 212 refused this key. Check the key and the secret, that the key is for this kind of account (real money or practice) and that it can read the account, history and portfolio.",
        [nameof(TranslationKeys.BrokerUnreachable)] = "Can't reach Trading 212 right now ({0}). Try again in a minute.",
        [nameof(TranslationKeys.BrokerRateLimited)] = "Trading 212 asked to slow down. Obol tries again on its own.",
        [nameof(TranslationKeys.NotConnected)] = "Connect Trading 212 first (Sync › Connect).",
        [nameof(TranslationKeys.SyncRunning)] = "A sync is already running.",
        [nameof(TranslationKeys.CsvInvalid)] = "This isn't a Trading 212 CSV export (History › Export, as CSV).",
        [nameof(TranslationKeys.CsvEmpty)] = "The file had no trades, dividends or transfers Obol keeps.",

        ["UserName"] = "User name",
        ["Password"] = "Password",
        ["CurrentPassword"] = "Current password",
        ["NewPassword"] = "New password",
        ["Identifier"] = "Account",
        ["ApiKey"] = "API key",
        ["ApiSecret"] = "API secret",
        ["Environment"] = "Account type",
        ["File"] = "CSV file",
        ["Instrument"] = "Instrument",
        ["Year"] = "Year",
    };

    /// <inheritdoc />
    public Task<string> TranslateAsync(string key, params object[] arguments)
    {
        var text = Translations.TryGetValue(key, out var value) ? value : key;
        return Task.FromResult(arguments.Length == 0 ? text : string.Format(English, text, arguments));
    }

    /// <inheritdoc />
    public async Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments)
    {
        var label = await TranslateAsync(field);
        var message = await TranslateAsync(messageKey, arguments);
        return new ApiValidationError(field, $"{label} {message}");
    }
}
