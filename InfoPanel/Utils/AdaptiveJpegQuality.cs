using System;

namespace InfoPanel.Utils
{
    /// <summary>
    /// Carries the JPEG quality that last fit a device's payload limit from one frame to the next,
    /// so panels with a size cap don't re-run the whole quality-reduction ladder on every frame.
    /// </summary>
    internal static class AdaptiveJpegQuality
    {
        /// <summary>Encoded size below this fraction of the limit counts as headroom to step back up.</summary>
        internal const double HeadroomFraction = 0.75;

        /// <summary>Quality step used when probing back up towards the configured quality.</summary>
        internal const int StepUp = 5;

        /// <summary>
        /// Quality to start encoding the next frame at: the remembered quality, never above the
        /// user's configured quality (which may have been lowered since).
        /// </summary>
        public static int GetStartQuality(int? rememberedQuality, int configuredQuality)
        {
            return Math.Min(rememberedQuality ?? configuredQuality, configuredQuality);
        }

        /// <summary>
        /// Quality to remember after a frame encoded at <paramref name="usedQuality"/> produced
        /// <paramref name="encodedSize"/> bytes: one step up (capped at the configured quality) when
        /// there is clear headroom, otherwise the same quality.
        /// </summary>
        public static int GetNextQuality(int usedQuality, long encodedSize, int maxBytes, int configuredQuality)
        {
            if (encodedSize < maxBytes * HeadroomFraction)
            {
                return Math.Min(usedQuality + StepUp, configuredQuality);
            }

            return usedQuality;
        }
    }
}
