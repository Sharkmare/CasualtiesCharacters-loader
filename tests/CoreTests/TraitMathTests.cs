using System;
using System.Collections.Generic;
using CasualtiesCharacters.Core;

namespace CasualtiesCharacters.Tests
{
    public static class TraitMathTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("gain damping only scales increases", () =>
            {
                Check.Near(11f, TraitMath.DampenGain(10f, 12f, 0.5f), "damped gain");
                Check.Near(8f, TraitMath.DampenGain(10f, 8f, 0.5f), "loss untouched");
                Check.Near(12.5f, TraitMath.DampenGain(10f, 12f, 1.25f), "amplified");
            });

            suite.Add("insulation keeps the game floor and heat factor needs the threshold", () =>
            {
                Check.Near(0.5f, TraitMath.ApplyInsulation(0.2f, 1.6f, 0.5f), "floor");
                Check.Near(1.6f, TraitMath.ApplyInsulation(1f, 1.6f, 0.5f), "scaled");
                Check.Near(1f, TraitMath.HeatThirstFactor(37.5f, 37.5f, 1.8f), "at threshold");
                Check.Near(1.8f, TraitMath.HeatThirstFactor(38f, 37.5f, 1.8f), "above");
                Check.Near(2.25f, TraitMath.StackMultipliers(new[] { 1.5f, 1.5f }), "stack");
            });
        }
    }
}
