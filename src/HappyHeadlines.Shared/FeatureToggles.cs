using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;

namespace HappyHeadlines.Shared;

public static class FeatureToggles
{
    /// <summary>Release toggle: hides the whole SubscriberService.</summary>
    public const string SubscriberService = "SubscriberService";

    /// <summary>
    /// The toggles live in features/features.json, mounted into the containers. The file is re-read when it changes,
    /// so a toggle is switched on or off without redeploying anything.
    /// </summary>
    public static IHostApplicationBuilder AddFeatureToggles(this IHostApplicationBuilder builder)
    {
        var file = builder.Configuration["FeaturesFile"] ?? "/features/features.json";
        builder.Configuration.AddJsonFile(file, optional: true, reloadOnChange: true);
        builder.Services.AddFeatureManagement();
        return builder;
    }
}
