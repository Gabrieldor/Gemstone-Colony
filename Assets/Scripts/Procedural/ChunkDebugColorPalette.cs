using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Assigns a visually distinct color to each index in a sequence, for debug
    /// visualizations where neighboring cells/chunks need to be told apart at a
    /// glance. Uses golden-angle hue stepping, which spreads hues out evenly no
    /// matter how many indices are requested.
    /// </summary>
    public static class ChunkDebugColorPalette
    {
        private const float GoldenRatioConjugate = 0.618033988749895f;

        public static Color ForIndex(int index)
        {
            float hue = (index * GoldenRatioConjugate) % 1f;
            return Color.HSVToRGB(hue, 0.65f, 0.95f);
        }
    }
}
