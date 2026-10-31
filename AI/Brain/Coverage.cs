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
