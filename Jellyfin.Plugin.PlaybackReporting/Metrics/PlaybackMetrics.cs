using System;
using Prometheus;
using PMetrics = Prometheus.Metrics;

namespace Jellyfin.Plugin.PlaybackReporting.Metrics
{
    internal static class PlaybackMetrics
    {
        private static readonly Counter PlaybackStarts = PMetrics.CreateCounter(
            name: "jellyfin_playback_starts_total",
            help: "Total number of playback starts.",
            labelNames: new[] { "item_type", "play_mode" }
        );

        private static readonly Counter PlaybackStops = PMetrics.CreateCounter(
            name: "jellyfin_playback_stops_total",
            help: "Total number of playback stops.",
            labelNames: new[] { "item_type", "play_mode" }
        );

        private static readonly Gauge ActivePlaybacks = PMetrics.CreateGauge(
            name: "jellyfin_active_playbacks",
            help: "Current number of active playbacks.",
            labelNames: new[] { "item_type", "play_mode" }
        );

        // Mode-only metrics for easy ratio dashboards (keeps cardinality minimal).
        private static readonly Counter ModeStarts = PMetrics.CreateCounter(
            name: "jellyfin_playback_mode_starts_total",
            help: "Total number of playback starts by mode only.",
            labelNames: new[] { "play_mode" }
        );

        private static readonly Counter ModeStops = PMetrics.CreateCounter(
            name: "jellyfin_playback_mode_stops_total",
            help: "Total number of playback stops by mode only.",
            labelNames: new[] { "play_mode" }
        );

        private static readonly Gauge ActivePlaybacksByMode = PMetrics.CreateGauge(
            name: "jellyfin_active_playbacks_by_mode",
            help: "Current number of active playbacks (mode only).",
            labelNames: new[] { "play_mode" }
        );

        // Keep histogram label-free to prevent cardinality growth.
        private static readonly Histogram PlaybackDurationSeconds = PMetrics.CreateHistogram(
            name: "jellyfin_playback_duration_seconds",
            help: "Observed playback duration on stop in seconds.",
            new HistogramConfiguration
            {
                // Buckets roughly: 15s, 1m, 5m, 15m, 1h, 2h, 4h
                Buckets = new double[] { 15, 60, 300, 900, 3600, 7200, 14400 }
            }
        );

        public static void RecordStart(string itemType, string playMode)
        {
            var (t, m) = (Normalize(itemType), NormalizePlayMode(playMode));
            PlaybackStarts.WithLabels(t, m).Inc();
            ActivePlaybacks.WithLabels(t, m).Inc();
            ModeStarts.WithLabels(m).Inc();
            ActivePlaybacksByMode.WithLabels(m).Inc();
        }

        public static void RecordStop(string itemType, string playMode, double? durationSeconds)
        {
            var (t, m) = (Normalize(itemType), NormalizePlayMode(playMode));
            PlaybackStops.WithLabels(t, m).Inc();
            ActivePlaybacks.WithLabels(t, m).Dec();
            ModeStops.WithLabels(m).Inc();
            ActivePlaybacksByMode.WithLabels(m).Dec();
            if (durationSeconds.HasValue && durationSeconds.Value >= 0)
            {
                PlaybackDurationSeconds.Observe(durationSeconds.Value);
            }
        }

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return "unknown";
            }
            return s.Trim().ToLowerInvariant();
        }

        private static string NormalizePlayMode(string? playMode)
        {
            // Map Jellyfin play methods to low-cardinality values.
            // Transcode -> "transcode"; everything else -> "direct"; unknown -> "unknown"
            if (string.IsNullOrWhiteSpace(playMode))
            {
                return "unknown";
            }

            var pm = playMode.Trim();
            if (pm.Equals("Transcode", StringComparison.OrdinalIgnoreCase))
            {
                return "transcode";
            }

            if (pm.Equals("DirectPlay", StringComparison.OrdinalIgnoreCase) ||
                pm.Equals("DirectStream", StringComparison.OrdinalIgnoreCase))
            {
                return "direct";
            }

            // Some callers may pass already-normalized values like "direct"/"transcode"
            if (pm.Equals("direct", StringComparison.OrdinalIgnoreCase) ||
                pm.Equals("transcode", StringComparison.OrdinalIgnoreCase))
            {
                return pm.ToLowerInvariant();
            }

            return "unknown";
        }
    }
}
