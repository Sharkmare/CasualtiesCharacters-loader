using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CasualtiesCharacters.Core
{
    public static class TraitMath
    {
        public static float DampenGain(float previous, float current, float multiplier)
        {
            float gain = current - previous;
            return gain > 0f ? previous + gain * multiplier : current;
        }

        public static float StackMultipliers(IEnumerable<float> multipliers)
        {
            float product = 1f;
            foreach (float multiplier in multipliers) product *= multiplier;
            return product;
        }

        public static float ApplyInsulation(float gameValue, float multiplier, float floor)
        {
            return Math.Max(floor, gameValue * multiplier);
        }

        public static float HeatThirstFactor(float temperature, float threshold, float mult)
        {
            return temperature > threshold ? mult : 1f;
        }
    }
}
