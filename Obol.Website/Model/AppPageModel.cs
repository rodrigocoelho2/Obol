using Obol.Library.Enums;
using Obol.Sdk.Models;

namespace Obol.Website.Model;

/// <summary>The screens of the app (picks the highlighted tab).</summary>
public enum AppSection
{
    /// <summary>Overview.</summary>
    Overview,

    /// <summary>Holdings (and a position).</summary>
    Holdings,

    /// <summary>Insights: performance, exposure, realized gains, goals and the coach.</summary>
    Insights,

    /// <summary>Income.</summary>
    Income,

    /// <summary>Activity.</summary>
    Activity,

    /// <summary>The Trading 212 sync and CSV import.</summary>
    Sync,

    /// <summary>Settings.</summary>
    Settings,
}

/// <summary>
/// What every signed-in screen needs for the shell: who's signed in, which tab is on, how the sync is.
/// </summary>
public class AppPageModel
{
    /// <summary>The product name.</summary>
    public const string ProductName = "Obol";

    /// <summary>The version shown in the app.</summary>
    public const string CurrentVersion = "v1.0";

    /// <summary>Gets or sets the signed-in user name.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the signed-in account is an admin.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Gets or sets the screen.</summary>
    public AppSection Section { get; set; }

    /// <summary>Gets or sets a message when the WebApi can't be reached (the screen shows it instead of data).</summary>
    public string? Unavailable { get; set; }

    /// <summary>Gets or sets the Trading 212 connection (for the sync chip), when it could be read.</summary>
    public ConnectionDTO? Connection { get; set; }

    /// <summary>Gets the initials shown in the avatar.</summary>
    public string Initials => UserName.Length == 0 ? "O" : UserName.Length == 1 ? UserName.ToUpperInvariant() : UserName[..2].ToUpperInvariant();

    /// <summary>Gets the name shown in the greeting (first letter up).</summary>
    public string DisplayName => UserName.Length == 0 ? string.Empty : char.ToUpperInvariant(UserName[0]) + UserName[1..];

    /// <summary>Gets the greeting for the time of day.</summary>
    public string Greeting => Helpers.Clock.Now.Hour switch
    {
        < 5 => "Good night",
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening",
    };

    /// <summary>Gets the sync chip: "Synced 2 min ago", "Syncing…", "Sync failed", "Not connected".</summary>
    public string SyncLabel => Connection switch
    {
        null => "Sync",
        { Connected: false } => "Not connected",
        { State: SyncState.Running } => "Syncing…",
        { State: SyncState.Failed } => "Sync failed",
        { LastSyncAt: { } at } => "Synced " + Helpers.Fmt.Ago(at),
        _ => "Waiting to sync",
    };

    /// <summary>Gets the state of the sync dot: ok, run, bad or off.</summary>
    public string SyncDot => Connection switch
    {
        { Connected: true, State: SyncState.Running } => "run",
        { Connected: true, State: SyncState.Failed } => "bad",
        { Connected: true } => "ok",
        _ => "off",
    };
}

/// <summary>The Overview.</summary>
public sealed class OverviewPageModel : AppPageModel
{
    /// <summary>Gets or sets the data.</summary>
    public OverviewDTO? Data { get; set; }

    /// <summary>Gets or sets the chart range.</summary>
    public ChartRange Range { get; set; } = ChartRange.Year;
}

/// <summary>The Holdings screen.</summary>
public sealed class HoldingsPageModel : AppPageModel
{
    /// <summary>Gets or sets the data.</summary>
    public HoldingsDTO? Data { get; set; }
}

/// <summary>One position.</summary>
public sealed class PositionPageModel : AppPageModel
{
    /// <summary>Gets or sets the data.</summary>
    public PositionDetailDTO? Data { get; set; }

    /// <summary>Gets or sets the instrument key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the chart range.</summary>
    public ChartRange Range { get; set; } = ChartRange.Year;
}

/// <summary>The Income screen.</summary>
public sealed class IncomePageModel : AppPageModel
{
    /// <summary>Gets or sets the data.</summary>
    public IncomeDTO? Data { get; set; }
}

/// <summary>The Activity screen.</summary>
public sealed class ActivityPageModel : AppPageModel
{
    /// <summary>The lines per page.</summary>
    public const int PageSize = 40;

    /// <summary>Gets or sets the data.</summary>
    public ActivityPageDTO? Data { get; set; }

    /// <summary>Gets or sets the kind shown (null: everything).</summary>
    public ActivityKind? Kind { get; set; }

    /// <summary>Gets or sets the page (from 1).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>The Sync screen.</summary>
public sealed class SyncPageModel : AppPageModel
{
    /// <summary>Gets or sets the data.</summary>
    public ConnectionDTO? Data { get; set; }
}

/// <summary>The Settings screen.</summary>
public sealed class SettingsPageModel : AppPageModel
{
    /// <summary>Gets or sets every account (admins only).</summary>
    public List<UserDTO> Users { get; set; } = [];

    /// <summary>Gets or sets the signed-in account's id.</summary>
    public string UserId { get; set; } = string.Empty;
}

/// <summary>The public page that explains Obol.</summary>
public sealed class LandingModel
{
    /// <summary>Gets or sets a value indicating whether the visitor is signed in.</summary>
    public bool SignedIn { get; set; }

    /// <summary>Gets or sets a value indicating whether anyone may create an account.</summary>
    public bool CanCreateAccount { get; set; }
}

/// <summary>The pages of the Insights tab.</summary>
public enum InsightsTab
{
    /// <summary>Returns against the indexes.</summary>
    Performance,

    /// <summary>Currencies, sectors, countries.</summary>
    Exposure,

    /// <summary>Realized gains.</summary>
    Realized,

    /// <summary>The projection.</summary>
    Goals,

    /// <summary>The AI coach.</summary>
    Coach,
}

/// <summary>A page of the Insights tab.</summary>
public abstract class InsightsPageModel : AppPageModel
{
    /// <summary>Gets the page.</summary>
    public abstract InsightsTab Tab { get; }
}

/// <summary>Insights › Performance.</summary>
public sealed class PerformancePageModel : InsightsPageModel
{
    /// <inheritdoc />
    public override InsightsTab Tab => InsightsTab.Performance;

    /// <summary>Gets or sets the data.</summary>
    public PerformanceDTO? Data { get; set; }

    /// <summary>Gets or sets the range.</summary>
    public ChartRange Range { get; set; } = ChartRange.Year;
}

/// <summary>Insights › Exposure.</summary>
public sealed class ExposurePageModel : InsightsPageModel
{
    /// <inheritdoc />
    public override InsightsTab Tab => InsightsTab.Exposure;

    /// <summary>Gets or sets the data.</summary>
    public ExposureDTO? Data { get; set; }
}

/// <summary>Insights › Realized.</summary>
public sealed class RealizedPageModel : InsightsPageModel
{
    /// <inheritdoc />
    public override InsightsTab Tab => InsightsTab.Realized;

    /// <summary>Gets or sets the data.</summary>
    public RealizedDTO? Data { get; set; }
}

/// <summary>Insights › Goals.</summary>
public sealed class GoalsPageModel : InsightsPageModel
{
    /// <inheritdoc />
    public override InsightsTab Tab => InsightsTab.Goals;

    /// <summary>Gets or sets the data.</summary>
    public GoalsDTO? Data { get; set; }
}

/// <summary>Insights › Coach.</summary>
public sealed class CoachPageModel : InsightsPageModel
{
    /// <inheritdoc />
    public override InsightsTab Tab => InsightsTab.Coach;

    /// <summary>Gets or sets the data.</summary>
    public CoachDTO? Data { get; set; }
}
