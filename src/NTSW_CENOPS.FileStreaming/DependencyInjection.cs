using Microsoft.Extensions.DependencyInjection;
using NTSW_CENOPS.FileStreaming.Contracts;
using NTSW_CENOPS.FileStreaming.Services;
using NTSW_CENOPS.FileStreaming.Storage;

namespace NTSW_CENOPS.FileStreaming;

public static class DependencyInjection
{
    /// <summary>
    /// Register the module when the host is ready to integrate uploads.
    /// No existing CENOPS service currently invokes this method.
    /// </summary>
    public static IServiceCollection AddCenopsFileStreaming(
        this IServiceCollection services,
        Action<FileStreamingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new FileStreamingOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IFileMetadataStore, JsonFileMetadataStore>();
        services.AddScoped<IFileStreamingService, FileStreamingService>();
        services.AddScoped<IFilePreviewReader, FilePreviewReader>();
        return services;
    }
}
