using System;
using LostPetNetwork.Services.Pets;
using LostPetNetwork.UI;

namespace LostPetNetwork.AR
{
    /// <summary>Small calculations used by the AR scenes (pure logic, covered by tests).</summary>
    public static class ArMath
    {
        /// <summary>Radius in metres of a circle covering the given floor area.</summary>
        public static float RadiusForArea(float squareMetres) => (float)Math.Sqrt(Math.Max(0f, squareMetres) / Math.PI);

        /// <summary>Approximate real-world scale factor for a pet built by PetFactory (its chosen size is already part of the model).</summary>
        public static float PetScale(PetConfig pet) => PetFactory.LifeSizeScale(pet);

        /// <summary>The avatar is built about 2.2 units tall; this makes it a 1.7 m person.</summary>
        public const float AvatarScale = 0.77f;

        /// <summary>Whole centimetres, for a readable "about 55 cm tall".</summary>
        public static int ToCentimetres(float metres) => (int)Math.Round(metres * 100f);
    }
}
