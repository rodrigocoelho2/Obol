using Obol.Library.Validation;
using Obol.Sdk;

namespace Obol.Website.Model;

/// <summary>
/// Base of every Website model: holds the API client and turns API failures into <see cref="AjaxResult"/> errors.
/// Models do the work; controllers only create them and return their result.
/// </summary>
/// <param name="apiClient">The Obol.WebApi client.</param>
public abstract class WebsiteBaseModel(ObolClient apiClient)
{
    /// <summary>What the pages say when the WebApi can't be reached.</summary>
    public const string ApiUnavailableMessage = "Can't reach Obol's engine (WebApi) right now. Check that the obol-webapi container is running.";

    /// <summary>Gets the API client.</summary>
    protected ObolClient ApiClient { get; } = apiClient;

    /// <summary>
    /// Calls the API and wraps the result.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="call">The API call.</param>
    /// <returns>The data, or the validation errors.</returns>
    protected static async Task<AjaxDataResult<T>> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return new AjaxDataResult<T> { Data = await call() };
        }
        catch (ObolApiException exception)
        {
            return new AjaxDataResult<T> { IsValid = false, ValidationErrors = [.. exception.ToValidationErrors()] };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new AjaxDataResult<T> { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, ApiUnavailableMessage)] };
        }
    }

    /// <summary>
    /// Calls an API operation that returns no data.
    /// </summary>
    /// <param name="call">The API call.</param>
    /// <returns>Success, or the validation errors.</returns>
    protected static async Task<AjaxDataResult<bool>> CallAsync(Func<Task> call) =>
        await CallAsync(async () =>
        {
            await call();
            return true;
        });
}
