using System.Collections.Generic;
using RoadArchitect;
using UnityEngine;

/// <summary>
/// Every road in a scene, sampled into a grid, so a tool can ask "how far is this spot from a road?" a few
/// thousand times while it builds a plan.
///
/// This is the question both of the roadside tools keep needing and neither can afford to ask the hard way.
/// A wall thirty metres out from one road lands on the carriageway of the next one as soon as a level hairpins
/// or a second road runs alongside - which is a wall the player drives into on a road they are meant to be
/// driving on - and the only way to know is to measure the spot against <b>every</b> road, not just the one
/// being walled. Asking <see cref="SplineC.GetClosestParam"/> for each of those measurements is far too slow
/// to do inside a window's OnGUI, so the roads are sampled once into a grid of buckets and every query after
/// that is a lookup of the few buckets around the point.
///
/// Distances are measured flat, in the XZ plane: a road passing overhead is not a road you can drive into, and
/// what these tools are keeping clear of is the asphalt, not the air above it.
/// </summary>
public class RoadField
{
    /// <summary>The side of one bucket, in metres.</summary>
    private const float CellSize = 20f;

    /// <summary>How many buckets out from a point are searched. Three cells of twenty metres is sixty metres
    /// of guarantee, which is further than any sensible offset a painter stands its props at.</summary>
    private const int SearchCells = 3;

    /// <summary>
    /// How much a measurement can be optimistic by, in metres: the roads are sampled every few metres, so the
    /// nearest sample is up to half that away from the nearest point of the road itself. A tool that has to
    /// promise "nothing stands within a metre of a road" adds this to the metre it asks for, and then the
    /// promise holds whatever the sampling missed.
    /// </summary>
    public const float Slack = 1f;

    private struct Sample
    {
        public Vector3 centre;      // a point on the road's centre line
        public float half;          // asphalt plus shoulders, to one side of that centre
        public Road road;
    }

    private readonly Dictionary<long, List<Sample>> buckets = new Dictionary<long, List<Sample>>();
    private readonly List<Sample> samples = new List<Sample>();

    /// <summary>How many points the roads were sampled at, for the tools to report.</summary>
    public int SampleCount { get { return samples.Count; } }

    /// <summary>
    /// Samples every road given, a few metres at a time. A road with no built spline is skipped, as it has no
    /// asphalt to keep clear of.
    /// </summary>
    public RoadField(IEnumerable<Road> roads, float step = 4f)
    {
        step = Mathf.Max(1f, step);

        foreach (Road road in roads)
        {
            if (road == null || road.spline == null) continue;
            if (road.spline.distance <= 0.01f || road.spline.GetNodeCount() < 2) continue;

            SplineC spline = road.spline;
            float half = RoadRoute.RoadEdge(road);
            float length = spline.distance;

            int count = Mathf.Max(2, Mathf.CeilToInt(length / step));

            for (int i = 0; i <= count; i++)
            {
                Vector3 position, tangent;
                spline.GetSplineValueBoth(i / (float)count, out position, out tangent);

                Sample sample = new Sample();
                sample.centre = position;
                sample.half = half;
                sample.road = road;

                samples.Add(sample);

                long key = Key(position.x, position.z);
                List<Sample> bucket;
                if (!buckets.TryGetValue(key, out bucket))
                {
                    bucket = new List<Sample>(4);
                    buckets.Add(key, bucket);
                }

                bucket.Add(sample);
            }
        }
    }

    public static RoadField ForScene(float step = 4f)
    {
        return new RoadField(RoadRoute.FindRoadsInScene().ConvertAll(entry => entry.road), step);
    }

    /// <summary>
    /// How much room there is at a point before it is standing on a road: the distance to the nearest edge of
    /// the nearest asphalt, shoulders included. Positive is off the road, zero is exactly on its edge, and
    /// negative is on the road itself. <paramref name="nearest"/> is the road that edge belongs to.
    ///
    /// A point with no road within the search distance comes back as <see cref="Far"/>.
    /// </summary>
    public const float Far = 9999f;

    /// <param name="except">
    /// A road to leave out of the measurement. A painter asking "is anything else in the way here" about a
    /// spot that is, by construction, a known distance from its own road wants this: the answer it is looking
    /// for is the <b>other</b> road that has turned up under it.
    /// </param>
    public float Clearance(Vector3 point, out Road nearest, Road except = null)
    {
        nearest = null;

        float best = Far;
        int cx = Mathf.FloorToInt(point.x / CellSize);
        int cz = Mathf.FloorToInt(point.z / CellSize);

        for (int x = cx - SearchCells; x <= cx + SearchCells; x++)
        {
            for (int z = cz - SearchCells; z <= cz + SearchCells; z++)
            {
                List<Sample> bucket;
                if (!buckets.TryGetValue(Key(x, z), out bucket)) continue;

                for (int i = 0; i < bucket.Count; i++)
                {
                    Sample sample = bucket[i];

                    if (sample.road == except) continue;

                    float dx = point.x - sample.centre.x;
                    float dz = point.z - sample.centre.z;

                    float edge = Mathf.Sqrt(dx * dx + dz * dz) - sample.half;

                    if (edge < best)
                    {
                        best = edge;
                        nearest = sample.road;
                    }
                }
            }
        }

        return best;
    }

    public float Clearance(Vector3 point)
    {
        Road ignored;
        return Clearance(point, out ignored);
    }

    /// <summary>
    /// The shortest distance along a straight line between two points to any road. This is what tells a tool
    /// whether a wall it is about to lay <b>across</b> the land - a join at a junction, a cap at the end of the
    /// level - would cross a road on the way, which is not always obvious from its two ends being clear.
    /// </summary>
    public float ClearanceAlong(Vector3 from, Vector3 to, int samples = 8)
    {
        return ClearanceAlong(from, to, null, samples);
    }

    public float ClearanceAlong(Vector3 from, Vector3 to, Road except, int samples = 8)
    {
        float best = Far;

        for (int i = 0; i <= samples; i++)
        {
            float clearance = Clearance(Vector3.Lerp(from, to, i / (float)samples), out Road ignored, except);
            if (clearance < best) best = clearance;
        }

        return best;
    }

    private static long Key(float x, float z)
    {
        return Key(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize));
    }

    private static long Key(int x, int z)
    {
        return ((long)x << 32) ^ (uint)z;
    }
}
