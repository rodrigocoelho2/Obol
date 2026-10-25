using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Obol.Library.Enums;
using Obol.Library.Services;
using Obol.Library.Validation;
using Obol.WebApi.Extensions.Globalization;
using Obol.WebApi.Features.Demo.Services;
using Obol.WebApi.Features.Users.Models.DTOs;
using Obol.WebApi.Features.Users.Models.Entities;
using Obol.WebApi.Features.Users.Repositories;

namespace Obol.WebApi.Features.Users.Services;

/// <summary>
/// The accounts: create, sign in, change password, delete. Passwords are PBKDF2 hashes.
/// </summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="demoSeeder">Fills new accounts with the sample portfolio in demo mode.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="options">Module options.</param>
internal sealed partial class UserService(
    IUserRepository userRepository,
    IDemoSeeder demoSeeder,
    IGlobalizationHelper globalizationHelper,
    IOptions<ObolOptions> options) : IUserService
{
    /// <summary>Shortest password accepted.</summary>
    internal const int MinPasswordLength = 8;

    /// <summary>The demo account's user name.</summary>
    internal const string DemoUserName = "demo";

    private const int MaxPasswordLength = 200;
    private const string FieldUserName = "UserName";
    private const string FieldPassword = "Password";
    private const string FieldCurrentPassword = "CurrentPassword";
    private const string FieldNewPassword = "NewPassword";
    private const string FieldIdentifier = "Identifier";

    // Spends the same time on an unknown user name as on a wrong password.
    private static readonly string DummyHash = PasswordHasher.Hash("not-a-real-password");

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IDemoSeeder _demoSeeder = demoSeeder;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly ObolOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserStatusDTO>> StatusAsync() => ServiceApiResponse<UserStatusDTO>.Success(new UserStatusDTO
    {
        UserCount = await _userRepository.CountAsync(),
        DemoMode = _options.DemoMode,
        DemoUserName = _options.DemoMode ? DemoUserName : null,
        DemoPassword = _options.DemoMode ? _options.DemoPassword : null,
    });

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<UserDTO>>> ListAsync() =>
        ServiceApiResponse<List<UserDTO>>.Success((await _userRepository.ListAsync())
            .OrderBy(static user => user.Username, StringComparer.OrdinalIgnoreCase).Select(ToDTO).ToList());

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> GetAsync(string identifier)
    {
        var user = Guid.TryParse(identifier, out var id) ? await _userRepository.GetAsync(id) : null;
        return user is null
            ? ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier ?? string.Empty))
            : ServiceApiResponse<UserDTO>.Success(ToDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> CreateAsync(UserCreateRequest item)
    {
        var userName = (item.UserName ?? string.Empty).Trim();
        var password = item.Password ?? string.Empty;

        var validationErrors = new List<ApiValidationError>();
        if (!UserNamePattern().IsMatch(userName))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.UserNameInvalid)));
        }

        validationErrors.AddRange(await ValidatePasswordAsync(FieldPassword, password));
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<UserDTO>()).AddErrorRange(validationErrors);
        }

        var user = await _userRepository.CreateAsync(userName, PasswordHasher.Hash(password), NewStamp());
        if (user is null)
        {
            return (await ValidationProblemsAsync<UserDTO>())
                .AddError(FieldUserName, (await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.AlreadyExists), userName)).Message);
        }

        await _demoSeeder.SeedAsync(user.UserUniqueID);
        return ServiceApiResponse<UserDTO>.Success(ToDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> SignInAsync(UserSignInRequest item)
    {
        var userName = (item.UserName ?? string.Empty).Trim();
        var user = userName.Length is > 0 and <= 32 ? await _userRepository.GetByNameAsync(userName) : null;

        var valid = PasswordHasher.Verify(item.Password ?? string.Empty, user?.PasswordHash ?? DummyHash) && user is not null;
        if (!valid)
        {
            var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials));
            return ServiceApiResponse<UserDTO>.ValidationProblems(message).AddError(FieldPassword, message);
        }

        await _userRepository.TouchLoginAsync(user!.UserUniqueID);
        return ServiceApiResponse<UserDTO>.Success(ToDTO((await _userRepository.GetAsync(user.UserUniqueID)) ?? user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> ChangePasswordAsync(UserPasswordRequest item)
    {
        var user = Guid.TryParse(item.Id, out var id) ? await _userRepository.GetAsync(id) : null;
        if (user is null)
        {
            return ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.Id ?? string.Empty));
        }

        var validationErrors = new List<ApiValidationError>();
        if (!PasswordHasher.Verify(item.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldCurrentPassword, nameof(TranslationKeys.WrongCurrentPassword)));
        }

        validationErrors.AddRange(await ValidatePasswordAsync(FieldNewPassword, item.NewPassword ?? string.Empty));
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<UserDTO>()).AddErrorRange(validationErrors);
        }

        await _userRepository.SetPasswordAsync(user.UserUniqueID, PasswordHasher.Hash(item.NewPassword!), NewStamp());
        return ServiceApiResponse<UserDTO>.Success(ToDTO((await _userRepository.GetAsync(user.UserUniqueID))!));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string identifier)
    {
        var users = await _userRepository.ListAsync();
        var user = Guid.TryParse(identifier, out var id) ? users.FirstOrDefault(user => user.UserUniqueID == id) : null;
        if (user is null)
        {
            return ServiceApiResponse<string>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier ?? string.Empty));
        }

        if (user.IsAdmin && users.Count(static other => other.IsAdmin) == 1)
        {
            var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.LastAdmin));
            return ServiceApiResponse<string>.ValidationProblems(message).AddError(FieldIdentifier, message);
        }

        await _userRepository.DeleteAsync(user.UserUniqueID);
        return ServiceApiResponse<string>.Success(user.UserUniqueID.ToString("N"));
    }

    /// <summary>
    /// Maps an account to its DTO.
    /// </summary>
    /// <param name="user">The account.</param>
    /// <returns>The DTO.</returns>
    internal static UserDTO ToDTO(UserAccount user) => new()
    {
        Id = user.UserUniqueID.ToString("N"),
        UserName = user.Username,
        Role = user.IsAdmin ? UserRole.Admin : UserRole.User,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc)),
        LastSignInAt = user.LastLoginAt is { } at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : null,
        SecurityStamp = user.SecurityStamp,
    };

    private static string NewStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private async Task<List<ApiValidationError>> ValidatePasswordAsync(string field, string password)
    {
        var errors = new List<ApiValidationError>();
        if (password.Length < MinPasswordLength)
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync(field, nameof(TranslationKeys.PasswordTooShort), MinPasswordLength));
        }
        else if (password.Length > MaxPasswordLength)
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync(field, nameof(TranslationKeys.PasswordTooLong)));
        }

        return errors;
    }

    private async Task<ServiceApiResponse<T>> ValidationProblemsAsync<T>() =>
        ServiceApiResponse<T>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)));

    [GeneratedRegex("^[A-Za-z0-9._-]{3,32}$")]
    private static partial Regex UserNamePattern();
}
