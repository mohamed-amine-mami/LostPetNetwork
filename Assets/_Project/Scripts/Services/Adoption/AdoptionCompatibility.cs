using System;
using System.Collections.Generic;
using System.Linq;

namespace LostPetNetwork.Services.Adoption
{
    public readonly struct CompatibilityFactor
    {
        public readonly string Label;
        public readonly int Points;
        public readonly int Max;
        public readonly string Note;

        public CompatibilityFactor(string label, int points, int max, string note)
        {
            Label = label; Points = points; Max = max; Note = note;
        }
    }

    public class CompatibilityResult
    {
        public int Score;
        public string Verdict;
        public List<CompatibilityFactor> Factors = new List<CompatibilityFactor>();
        /// <summary>Floor space the pet needs to live comfortably, in square metres.</summary>
        public int RecommendedSpaceM2;
    }

    /// <summary>
    /// An explainable 0–100 score for how well a pet fits a home. Each factor shows its points and a plain
    /// sentence, so users see why. It is guidance only, not a decision.
    /// </summary>
    public static class AdoptionCompatibility
    {
        public const int SpaceMax = 30, ActivityMax = 25, GardenMax = 15, KidsMax = 15, PetsMax = 15;

        /// <summary>Square metres a pet needs: grows with size and with energy.</summary>
        public static int RecommendedSpaceM2(AdoptionListing pet)
        {
            int[] bySize = pet.IsCat ? new[] { 20, 30, 40 } : new[] { 25, 45, 70 };
            int space = bySize[Math.Max(0, Math.Min(2, pet.Size))];
            if (pet.Energy == Level.High) space += 10;
            if (pet.Energy == Level.Low) space -= 5;
            return space;
        }

        public static CompatibilityResult Score(AdoptionListing pet, AdopterProfile home)
        {
            var result = new CompatibilityResult { RecommendedSpaceM2 = RecommendedSpaceM2(pet) };

            // Space: full points when the home is big enough, shrinking as it gets smaller. A garden helps a little.
            int needed = result.RecommendedSpaceM2;
            double ratio = Math.Min(1.0, home.HomeSizeM2 / (double)needed);
            int space = (int)Math.Round(SpaceMax * ratio);
            if (ratio < 1.0 && home.HasGarden) space = Math.Min(SpaceMax, space + 5);
            result.Factors.Add(new CompatibilityFactor("Space", space, SpaceMax,
                ratio >= 1.0 ? $"Your {home.HomeSizeM2} m² home is enough ({needed} m² suggested)."
                             : $"{pet.Name} would like about {needed} m²; your home has {home.HomeSizeM2} m²."));

            // Activity: how close the pet's energy is to the household's.
            int gap = Math.Abs((int)pet.Energy - (int)home.Activity);
            int activity = gap == 0 ? ActivityMax : gap == 1 ? 14 : 4;
            result.Factors.Add(new CompatibilityFactor("Activity", activity, ActivityMax,
                gap == 0 ? "Your energy levels match."
                         : pet.Energy > home.Activity ? $"{pet.Name} is more energetic than your household."
                                                      : $"{pet.Name} is calmer than your household."));

            int garden = !pet.NeedsGarden ? GardenMax : home.HasGarden ? GardenMax : 0;
            result.Factors.Add(new CompatibilityFactor("Garden", garden, GardenMax,
                !pet.NeedsGarden ? "No garden needed." : home.HasGarden ? "You have the garden it needs." : $"{pet.Name} needs a garden and you don't have one."));

            int kids = !home.HasKids ? KidsMax : pet.GoodWithKids ? KidsMax : 0;
            result.Factors.Add(new CompatibilityFactor("Children", kids, KidsMax,
                !home.HasKids ? "No children at home." : pet.GoodWithKids ? "Good with children." : $"{pet.Name} is not recommended with children."));

            int pets = !home.HasOtherPets ? PetsMax : pet.GoodWithPets ? PetsMax : 3;
            result.Factors.Add(new CompatibilityFactor("Other pets", pets, PetsMax,
                !home.HasOtherPets ? "No other pets at home." : pet.GoodWithPets ? "Gets along with other pets." : $"{pet.Name} may not get along with other pets."));

            result.Score = result.Factors.Sum(f => f.Points);
            result.Verdict = result.Score >= 80 ? "Great match" : result.Score >= 60 ? "Good match" : result.Score >= 40 ? "Possible, check the notes" : "Not the best fit";
            return result;
        }
    }
}
