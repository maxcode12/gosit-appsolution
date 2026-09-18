using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoSit.Infrastructure.Video;

public static class VideoInterviewRegistration
{
    /// <summary>
    /// LiveKit and recording storage together, or neither.
    ///
    /// Every interview is recorded, so a video service with nowhere to put the recording is
    /// not half a feature — it is a way to hold an unrecorded call. Without both, the
    /// refusing stand-in is registered and interviews cannot be arranged.
    /// </summary>
    public static IServiceCollection AddVideoInterviews(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var liveKitSection = configuration.GetSection(LiveKitOptions.SectionName);
        var recordingSection = configuration.GetSection(InterviewRecordingOptions.SectionName);

        services.Configure<LiveKitOptions>(liveKitSection);
        services.Configure<InterviewRecordingOptions>(recordingSection);

        var liveKit = liveKitSection.Get<LiveKitOptions>() ?? new LiveKitOptions();
        var recording = recordingSection.Get<InterviewRecordingOptions>() ?? new InterviewRecordingOptions();

        services.AddHttpClient(LiveKitVideoMeetingProvider.HttpClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(15));

        if (recording.IsConfigured)
        {
            services.AddSingleton<IRecordingStore, BlobRecordingStore>();
        }
        else
        {
            services.AddSingleton<IRecordingStore, UnconfiguredRecordingStore>();
        }

        if (liveKit.IsConfigured && recording.IsConfigured)
        {
            services.AddSingleton<IVideoMeetingProvider, LiveKitVideoMeetingProvider>();
        }
        else
        {
            services.AddSingleton<IVideoMeetingProvider, UnconfiguredVideoMeetingProvider>();
        }

        services.AddSingleton<InterviewRecordingRetentionSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<InterviewRecordingRetentionSweeper>());

        return services;
    }
}
