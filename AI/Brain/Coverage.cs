using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // Where to put the next defense. The native AI puts every defense within the defense radius of a random city
    // (or of a random building after 100 failed steps) with no memory of what is already covered. 31 Oct v0.6:
    // 21-40 Kinetic Defenses stood, yet 53-85% of the shots landing on the AI's planet found no eligible defense in range.
    //
    // A defense covers what lies within `radius` of it (PlayerDefenseHandler.getDefenses: building-centre distance
    // strictly less than the radius). The value of a building is what losing it costs: cities hold the population that
    // decides the game, the rest is production.
    public struct Point
    {
        public float X, Y, Z;

        public Point(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float DistanceSquared(Point other)
        {
            float dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }

    public static class DefenseCoverage
    {
        // For each target, is it within `radius` of at least one of the defenses?
        public static bool[] Covered(IList<Point> targets, IList<Point> defenses, float radius)
        {
            bool[] covered = new bool[targets.Count];
            float r2 = radius * radius;
            for (int i = 0; i < targets.Count; i++)
            {
                for (int j = 0; j < defenses.Count; j++)
                {
                    if (targets[i].DistanceSquared(defenses[j]) < r2)
                    {
                        covered[i] = true;
                        break;
                    }
                }
            }
            return covered;
        }

        // Same, with each defense's own radius (upgrades change it).
        public static bool[] Covered(IList<Point> targets, IList<Point> defenses, IList<float> radii)
        {
            bool[] covered = new bool[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                for (int j = 0; j < defenses.Count; j++)
                {
                    if (targets[i].DistanceSquared(defenses[j]) < radii[j] * radii[j])
                    {
                        covered[i] = true;
                        break;
                    }
                }
            }
            return covered;
        }

        // Value of the targets within `radius` of `center` that no defense covers yet.
        public static float UncoveredValue(Point center, IList<Point> targets, IList<float> values, bool[] covered, float radius)
        {
            float r2 = radius * radius;
            float total = 0f;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!covered[i] && center.DistanceSquared(targets[i]) < r2) total += values[i];
            }
            return total;
        }

        // Pick the anchor building whose neighbourhood (within the defense radius) holds the most uncovered value: the
        // defense is then placed within its radius of that anchor. Among anchors scoring at least `band` of the best,
        // pick weighted by score squared, so repeated picks spread out instead of stacking. -1 when no anchor has
        // anything uncovered near it.
        public static int PickAnchor(IList<Point> anchors, IList<Point> targets, IList<float> values, bool[] covered, float radius,
            float band, Random rng, ICollection<int> excluded)
        {
            float[] scores = new float[anchors.Count];
            float best = 0f;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (excluded != null && excluded.Contains(i)) continue;
                scores[i] = UncoveredValue(anchors[i], targets, values, covered, radius);
                best = Math.Max(best, scores[i]);
            }
            if (best <= 0f) return -1;
            float total = 0f;
            for (int i = 0; i < scores.Length; i++)
            {
                scores[i] = scores[i] >= band * best ? scores[i] * scores[i] : 0f;
                total += scores[i];
            }
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < scores.Length; i++)
            {
                roll -= scores[i];
                if (roll <= 0 && scores[i] > 0f) return i;
            }
            for (int i = scores.Length - 1; i >= 0; i--)
            {
                if (scores[i] > 0f) return i;
            }
            return -1;
        }

        // `count` anchor draws from one scoring pass (Terra v0.7.1: scoring per draw was 12x the work). Same rule as
        // PickAnchor: anchors scoring at least `band` of the best, weighted by score squared. Empty when no anchor has
        // anything uncovered near it.
        public static List<int> PickAnchors(IList<Point> anchors, IList<Point> targets, IList<float> values, bool[] covered, float radius,
            float band, Random rng, int count)
        {
            List<int> picks = new List<int>();
            float[] weights = new float[anchors.Count];
            float best = 0f;
            for (int i = 0; i < anchors.Count; i++)
            {
                weights[i] = UncoveredValue(anchors[i], targets, values, covered, radius);
                best = Math.Max(best, weights[i]);
            }
            if (best <= 0f) return picks;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = weights[i] >= band * best ? weights[i] * weights[i] : 0f;
                total += weights[i];
            }
            for (int n = 0; n < count; n++)
            {
                double roll = rng.NextDouble() * total;
                int chosen = -1;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] <= 0f) continue;
                    chosen = i;
                    roll -= weights[i];
                    if (roll <= 0) break;
                }
                if (chosen >= 0) picks.Add(chosen);
            }
            return picks;
        }

        // v0.8: how many defenses cover each target (each with its own radius). Stacking matters: n defenses of
        // interception chance p stop a shot with 1-(1-p)^n, so a second Ion Defense over a city lifts 0.8 to 0.96.
        public static int[] CoverCounts(IList<Point> targets, IList<Point> defenses, IList<float> radii)
        {
            int[] counts = new int[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                for (int j = 0; j < defenses.Count; j++)
                {
                    if (targets[i].DistanceSquared(defenses[j]) < radii[j] * radii[j]) counts[i]++;
                }
            }
            return counts;
        }

        // Interception a new defense at `center` adds: each target within reach gains p x (1-p)^k of its value, k = the
        // defenses already over it.
        public static float MarginalValue(Point center, IList<Point> targets, IList<float> values, int[] counts, float radius, float p)
        {
            float r2 = radius * radius;
            float total = 0f;
            for (int i = 0; i < targets.Count; i++)
            {
                if (center.DistanceSquared(targets[i]) < r2) total += values[i] * p * (float)Math.Pow(1f - p, counts[i]);
            }
            return total;
        }

        // PickAnchors on marginal value: `count` draws among anchors scoring at least `band` of the best (score squared).
        public static List<int> PickAnchorsMarginal(IList<Point> anchors, IList<Point> targets, IList<float> values, int[] counts, float radius, float p,
            float band, Random rng, int count)
        {
            List<int> picks = new List<int>();
            float[] weights = new float[anchors.Count];
            float best = 0f;
            for (int i = 0; i < anchors.Count; i++)
            {
                weights[i] = MarginalValue(anchors[i], targets, values, counts, radius, p);
                best = Math.Max(best, weights[i]);
            }
            if (best <= 1e-6f) return picks;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = weights[i] >= band * best ? weights[i] * weights[i] : 0f;
                total += weights[i];
            }
            for (int n = 0; n < count; n++)
            {
                double roll = rng.NextDouble() * total;
                int chosen = -1;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] <= 0f) continue;
                    chosen = i;
                    roll -= weights[i];
                    if (roll <= 0) break;
                }
                if (chosen >= 0) picks.Add(chosen);
            }
            return picks;
        }

        // Share of the total value that is covered (for the decision log).
        public static float CoveredShare(IList<float> values, bool[] covered)
        {
            float total = 0f, inside = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                total += values[i];
                if (covered[i]) inside += values[i];
            }
            return total > 0f ? inside / total : 1f;
        }
    }
}
