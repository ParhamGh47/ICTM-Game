using UnityEngine;

/// <summary>
/// The little stage the tutorial card plays on: the game's own truck, let go a long way back, running down a
/// road at one of the game's own targets, filmed into a texture the card shows.
///
/// <b>What it shows.</b> The truck is let go a long way back and runs the target down; the target is blown
/// apart in front of it, in the gap the truck stops short of, and its pieces scatter and drop. It never
/// overlaps the truck, and the truck never has to be seen going through it.
///
/// <b>The game's own models, dressed for the stage.</b> The truck is the level's own truck and the targets are
/// the game's own Adamaks, so the card shows the player the vehicles and the figures they are about to meet
/// rather than stand-ins for them. What comes off a copy is the level's behaviour: its scripts are removed
/// outright, and everything else it carries - its rigidbodies, colliders, joints, particle systems, trails,
/// lights, cameras and sounds - is switched off where it stands rather than taken off (see <see cref="Dress"/>)
/// because Unity will not let some of them be removed from a running game. A prefab brought onto a stage brings
/// the level's behaviour with it, and a stage is a picture that lives outside the world.
///
/// The copy is made under a switched-off parent, so not one line of the truck's own code - its <c>Awake</c>,
/// its singleton, its damage - runs before the copy is stripped, and afterwards there is nothing left on it to
/// run at all. A model left unassigned falls back on a stand-in built from primitives (see
/// <see cref="BuildTruckModel"/>), so a card whose references have gone missing still shows a truck driving at
/// a target rather than an empty road.
///
/// <b>Where it lives.</b> Below the world, a long way down, with every object on a layer of its own that the
/// camera here films and the level's cameras are told to ignore. Nothing on the stage has a collider, a
/// rigidbody or a script, and nothing on it can make a sound - it is a film set and nothing more, and it
/// cannot collide with the level, be seen by it, be heard by it, or be mistaken for it.
///
/// <b>The light.</b> Point lights, not directionals: a directional light is global in this pipeline and would
/// light the whole level, while these are a few metres long and only reach the models they were put up for.
///
/// <b>Framing.</b> Nothing is placed at a hand-picked distance. The truck is built, measured, and the road, the
/// spacing of the targets, how far it drives and where the camera stands are all worked out from its own size
/// (see <see cref="BuildTruck"/>), then the camera solves its own distance from the box that the whole strip
/// fits in. That is what keeps the shot the same whether the truck is a small car or a long one, and it is what
/// keeps the truck and the targets big enough on the card to read at a glance.
/// </summary>
public class TutorialStage3D
{
    /// <summary>
    /// The card's own colours, handed in so the stage is drawn in the same hand as the card around it: the road
    /// and its paint, and the paper, ink and accent that the truck's load and the targets are made of.
    /// </summary>
    public struct Palette
    {
        public Color road;
        public Color line;
        public Color paper;
        public Color ink;
        public Color accent;
    }

    // ---------------------------------------------------------------- what it is given

    private readonly int targetCount;
    private readonly Vector2 resolution;
    private readonly Palette palette;

    // The game's own models, copied onto the stage (see Model).
    private readonly GameObject truckModel;
    private readonly GameObject targetModel;

    private const int StageLayer = 31;

    // ---------------------------------------------------------------- the strip

    // The sizes the strip is laid out in. They are all recomputed from the truck the moment it is built (see
    // BuildTruck); these are what the stage falls back on if that measurement comes out empty.
    private float roadHalfWidth = 2.6f;
    private float truckStartZ = -5.5f;
    private float hitZ = -0.1f;
    private float targetZ = 2f;
    private float targetSpacing = 1.6f;
    private float roadLength = 16f;
    private float stripHeight = 2.9f;

    // The truck's own two sizes, kept so everything that moves on the stage - the lane change, the turn of the
    // nose, the toss of a struck target - is written in truck lengths rather than in metres.
    private float truckLength = 5.2f;
    private float truckWidth = 2.4f;

    // ---------------------------------------------------------------- what it builds

    private GameObject root;
    private Camera camera;
    private RenderTexture texture;

    private Transform truck;
    private Transform[] targets;
    private Vector3[] targetHomes;
    private Quaternion[] targetFacing;

    // What the struck target comes apart into, and where each of those pieces sits on the model it was copied
    // from - see <see cref="Explode"/>. The throw and the spin are drawn once, when the stage is built, so a
    // piece's flight is one continuous movement rather than a new random shove every frame.
    private Transform[] pieces;
    private Vector3[] pieceHomes;
    private Quaternion[] pieceFacing;
    private Vector3[] pieceThrow;
    private Vector3[] pieceSpin;
    private float[] pieceFall;
    private float blastRange = 3.5f;

    private Vector3 hitPoint;
    private Vector3 truckHome;

    /// <summary>The picture the card shows.</summary>
    public RenderTexture Texture { get { return texture; } }

    /// <summary>The camera filming it, for anything that has to project a point of the stage onto the card.</summary>
    public Camera Camera { get { return camera; } }

    /// <summary>Where the first target stands, in world space: what the hit is about.</summary>
    public Vector3 HitPoint { get { return hitPoint; } }

    public TutorialStage3D(int targetCount, Vector2 resolution, Palette palette, GameObject truckModel, GameObject targetModel)
    {
        this.targetCount = Mathf.Max(1, targetCount);
        this.resolution = new Vector2(Mathf.Max(64f, resolution.x), Mathf.Max(64f, resolution.y));
        this.palette = palette;

        this.truckModel = truckModel;
        this.targetModel = targetModel;
    }

    public void Build()
    {
        root = new GameObject("Tutorial Stage");
        root.transform.SetParent(null, false);

        // A long way below the world. Far enough that no level camera has it in frame, near enough to keep the
        // shapes' own proportions honest (a stage shrunk to fit a card would need its own scale, and a scale is
        // one more thing to keep in step with the models).
        root.transform.position = new Vector3(0f, -1500f, 0f);

        Layer(root, StageLayer);

        // The truck goes down first and is measured as it lands, because everything else on the strip - the
        // width of the road, how far apart the targets stand, how far the truck drives and how the camera frames
        // all of it - is built out of its own size. Then the road is laid under it.
        BuildTruck();
        BuildRoad();
        BuildTargets();
        BuildCamera();
        BuildLights();

        // The level's cameras are told not to film this layer, so the stage can never turn up in the game's own
        // picture - including the cameras that are switched on later, which is why it is done to every camera in
        // the scene rather than to the one that happens to be active now.
        HideFromOtherCameras();
    }

    // ---------------------------------------------------------------- the models

    /// <summary>
    /// A copy of one of the game's own prefabs, dressed for the stage: parented to the stage root so the whole
    /// thing can be moved and measured as a unit, on the stage's own layer so only this stage's camera films it,
    /// and with everything that is not the model silenced and switched off (see <see cref="Dress"/>).
    ///
    /// The copy is made under a parent that is switched off, and that is the whole trick. A prefab's components
    /// wake the moment a plain <c>Instantiate</c> leaves them active, and the truck's own scripts include a
    /// static singleton and a controller that reads the player's input - so a copy made the ordinary way would
    /// run the level's code once, on a stage, in the frame before it could be dressed. Under a switched-off
    /// parent the copy is not active in the hierarchy, so nothing wakes; by the time it is moved onto the stage
    /// there is nothing left on it to wake.
    /// </summary>
    private GameObject Model(GameObject prefab, string name)
    {
        GameObject model = new GameObject(name);
        model.transform.SetParent(root.transform, false);

        GameObject holder = new GameObject(name + " (held)");
        holder.SetActive(false);
        holder.transform.SetParent(model.transform, false);

        // Placed at the stage's own origin: a prefab keeps the level coordinates it was last saved at, which are
        // hundreds of metres from a set parked below the world.
        GameObject copy = Object.Instantiate(prefab, holder.transform);
        copy.name = name + " (copy)";
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = Vector3.one;

        Dress(copy);

        // Belt and braces: not one line of the level's own code may reach the stage. If anything of it survived
        // the dressing - which could only be Unity refusing to let a component go - the copy is not switched on
        // at all, because a component's Awake runs the moment its object becomes active whether or not the
        // component is enabled. The stage then shows its road without the model, which is worth more than a
        // film prop driving the level for one frame.
        if (copy.GetComponentInChildren<MonoBehaviour>(true) != null)
        {
            Debug.LogWarning("[TutorialStage3D] A model copy still carries the level's scripts; it is " +
                             "dropped rather than switched on.", model);

            Object.Destroy(copy);
            Object.Destroy(holder);

            return model;
        }

        // Out of the holder and onto the stage, before the holder is thrown away.
        copy.transform.SetParent(model.transform, false);
        Object.Destroy(holder);

        Layer(model, StageLayer);

        return model;
    }

    /// <summary>
    /// Takes the level's behaviour off a copy and leaves the model: what draws stays, and nothing else can do
    /// anything.
    ///
    /// <b>Scripts are removed.</b> They are the one thing that has to go, because a script's <c>Awake</c> runs
    /// when the copy becomes active whether or not the script is enabled - so the truck's own, which include a
    /// driving controller, a damage model, a light toggle and a static singleton, would wake up on a film prop
    /// the moment it was switched on.
    ///
    /// <b>Everything else is switched off, not removed.</b> Unity will not take an <c>AudioSource</c> or a
    /// <c>Rigidbody</c> off an object while the engine is running - they belong to the audio and physics threads
    /// - and asking leaves the component in place and an error in the log. None of it needs removing to be
    /// harmless: a rigidbody made kinematic with no gravity and no collisions sits in the physics scene doing
    /// nothing, and a switched-off collider, joint, particle system, audio source, light or camera is an empty
    /// component on a prop. None of it is measured either - <see cref="DrawnBounds"/> counts meshes and nothing
    /// else - and none of it can run, fall, emit or sound, because the copy is not active until every one of
    /// them has been dealt with.
    ///
    /// Whatever is left over - a part of a kind not listed here - is queued for removal rather than taken off
    /// straight away, which is always allowed and matters only for that one frame: it is never a script, since
    /// those are all gone above.
    /// </summary>
    private static void Dress(GameObject copy)
    {
        Component[] parts = copy.GetComponentsInChildren<Component>(true);

        for (int i = 0; i < parts.Length; i++)
        {
            Component part = parts[i];

            if (part == null) continue;
            if (part is Transform) continue;
            if (part is MeshFilter) continue;
            if (part is MeshRenderer) continue;
            if (part is SkinnedMeshRenderer) continue;

            MonoBehaviour script = part as MonoBehaviour;

            if (script != null)
            {
                Object.DestroyImmediate(script);

                continue;
            }

            Rigidbody body = part as Rigidbody;

            if (body != null)
            {
                // Zeroed first: a velocity written to a kinematic body is not supported, and Unity says so.
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.detectCollisions = false;
                body.isKinematic = true;
                body.Sleep();

                continue;
            }

            AudioSource audio = part as AudioSource;

            if (audio != null)
            {
                audio.Stop();
                audio.clip = null;
                audio.playOnAwake = false;
                audio.enabled = false;

                continue;
            }

            ParticleSystem particles = part as ParticleSystem;

            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = particles.main;
                main.playOnAwake = false;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = false;

                continue;
            }

            Collider collider = part as Collider;

            if (collider != null)
            {
                collider.enabled = false;

                continue;
            }

            // Lights, cameras, trails and anything else with an on/off of its own.
            Behaviour behaviour = part as Behaviour;

            if (behaviour != null)
            {
                behaviour.enabled = false;

                continue;
            }

            // A joint, or something of a kind not listed: harmless with the bodies inert, and gone by the frame's
            // end. Queued rather than taken off now, because this is the path any component Unity refuses to
            // remove immediately would take, and a queued removal is never refused.
            Object.Destroy(part);
        }
    }

    /// <summary>
    /// The truck: the level's own truck, copied and dressed for the stage.
    ///
    /// It is built facing +z, which is the way it drives - the same way round the game's own prefab is authored,
    /// so a copy needs no turning - and it is built as one object so the whole thing can be moved and measured as
    /// a unit.
    ///
    /// With no prefab to copy, a stand-in is built instead: a box body with a cab and a windscreen at the front,
    /// four wheels, two headlights and the ice cream on the roof, in the game's own colours.
    /// </summary>
    private GameObject BuildTruckModel()
    {
        if (truckModel != null) return Model(truckModel, "Truck Model");

        Debug.LogWarning("[TutorialStage3D] No truck model is assigned to the card; it draws a stand-in.", root);

        GameObject model = new GameObject("Truck Model");
        model.transform.SetParent(root.transform, false);

        Color body = new Color(0.09f, 0.36f, 0.40f);
        Color roof = new Color(0.06f, 0.26f, 0.30f);
        Color tyre = new Color(0.08f, 0.08f, 0.10f);
        Color glass = new Color(0.62f, 0.82f, 0.90f);
        Color lamp = new Color(1f, 0.93f, 0.72f);

        // The body, and the cab standing on the front of it.
        Block("Body", model.transform, new Vector3(0f, 1.05f, -0.35f), new Vector3(2.3f, 1.05f, 3.9f), body);
        Block("Cab", model.transform, new Vector3(0f, 1.8f, 1.5f), new Vector3(2.2f, 0.95f, 1.4f), body);
        Block("Roof", model.transform, new Vector3(0f, 2.32f, 1.5f), new Vector3(2.1f, 0.12f, 1.3f), roof);

        // The windscreen, let into the front of the cab, which is most of what makes the shape read as a cab.
        Block("Windscreen", model.transform, new Vector3(0f, 1.95f, 2.19f), new Vector3(1.9f, 0.6f, 0.06f), glass);

        // The ice cream on the roof: the cone, and the scoop sitting on it.
        Drum("Cone", model.transform, new Vector3(0f, 2.6f, 1.5f), 0.34f, 0.5f, new Color(0.78f, 0.6f, 0.34f));
        Ball("Scoop", model.transform, new Vector3(0f, 2.98f, 1.5f), 0.7f, palette.paper);

        // The wheels, on their sides: the cylinder's own axis runs along y, so it is turned to run across the
        // truck and then scaled to a tyre.
        Wheel("Wheel FL", model.transform, new Vector3(-1.16f, 0.55f, 1.45f), 0.55f, 0.36f, tyre);
        Wheel("Wheel FR", model.transform, new Vector3(1.16f, 0.55f, 1.45f), 0.55f, 0.36f, tyre);
        Wheel("Wheel RL", model.transform, new Vector3(-1.16f, 0.55f, -1.5f), 0.55f, 0.36f, tyre);
        Wheel("Wheel RR", model.transform, new Vector3(1.16f, 0.55f, -1.5f), 0.55f, 0.36f, tyre);

        // The headlights, which are what the truck lights the road with.
        Block("Lamp L", model.transform, new Vector3(-0.78f, 1.25f, 2.22f), new Vector3(0.42f, 0.24f, 0.1f), lamp);
        Block("Lamp R", model.transform, new Vector3(0.78f, 1.25f, 2.22f), new Vector3(0.42f, 0.24f, 0.1f), lamp);

        return model;
    }

    /// <summary>
    /// The target: the game's own Adamak, copied and dressed for the stage - the very figure the player is being
    /// told to hit, at the size they will meet it.
    ///
    /// With no prefab to copy, a stand-in is built instead: a figure with a hat, drawn in the card's paper with
    /// an accent hat, so it reads as something the truck is meant to hit.
    /// </summary>
    private GameObject BuildTargetModel()
    {
        if (targetModel != null) return Model(targetModel, "Target Model");

        GameObject model = new GameObject("Target Model");
        model.transform.SetParent(root.transform, false);

        Color baseplate = new Color(0.34f, 0.36f, 0.42f);
        Color skin = new Color(0.94f, 0.86f, 0.76f);

        Block("Stand", model.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.34f), baseplate);
        Block("Torso", model.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.62f, 0.6f, 0.4f), palette.paper);
        Ball("Head", model.transform, new Vector3(0f, 1.38f, 0f), 0.44f, skin);
        Drum("Hat", model.transform, new Vector3(0f, 1.66f, 0f), 0.64f, 0.1f, palette.accent);
        Drum("Crown", model.transform, new Vector3(0f, 1.78f, 0f), 0.42f, 0.2f, palette.accent);

        return model;
    }

    // ---------------------------------------------------------------- the strip

    /// <summary>
    /// The truck, built, measured, put on the road - and the strip laid out around it.
    ///
    /// The proportions are what the card was framed with, kept as proportions so that whatever the truck
    /// measures it comes out the same shot: the target stands a little over a third of a truck length past the
    /// truck's own stopping point, the truck is let go a length and more behind that, and the camera is brought
    /// in close enough that the truck and the target are a good part of the picture rather than specks on an
    /// empty road.
    /// </summary>
    private void BuildTruck()
    {
        GameObject truckObject = BuildTruckModel();
        truck = truckObject.transform;

        Vector3 size = DrawnSize(truckObject);

        // A model that measures nothing is a model that did not build: the stage falls back on the sizes it was
        // first laid out with, so the card shows a road rather than an empty hole in itself.
        float length = size.z > 0.05f ? size.z : 5.2f;
        float width = size.x > 0.05f ? size.x : 2.4f;

        truckLength = length;
        truckWidth = width;

        roadHalfWidth = Mathf.Max(width * 0.95f, length * 0.24f);

        // It starts a whole truck-length and more back from the target, and that is the run: the frame is
        // only as wide as the target and the hit need, so the truck is a long way out when it is let go and
        // crosses most of the road to reach what it is aimed at.
        truckStartZ = -length * 1.35f;
        targetZ = length * 0.38f;

        // Its nose stops short of the target instead of in it. The two are never seen overlapping: the truck
        // arrives, and the target comes apart in the gap in front of it - which is where the blast happens.
        // A first stop, for a target that never gets measured at all. The real one is pulled up to the target's
        // own face once it has been built and measured (see Measure), because how near the truck may come is the
        // model's business rather than the truck's.
        hitZ = targetZ - (length * 0.5f + length * 0.2f);

        targetSpacing = Mathf.Max(length * 0.3f, 1f);
        roadLength = (targetZ + (targetCount - 1) * targetSpacing - truckStartZ) + length * 1.6f;
        stripHeight = length * 0.56f;

        // Sat on the road rather than at the model's own origin, whatever that origin is: the bottom of what the
        // model actually draws is what goes on the tarmac.
        truckHome = new Vector3(0f, RestingHeight(truckObject), truckStartZ);
        truck.localPosition = truckHome;
    }

    private void BuildRoad()
    {
        float length = roadLength;
        float paint = Mathf.Max(0.06f, roadLength * 0.012f);      // line widths, in the strip's own scale

        Block("Road", root.transform, new Vector3(0f, -0.15f, 0f), new Vector3(roadHalfWidth * 2f, 0.3f, length), palette.road);

        // The edge lines and the broken centre line, which are most of what makes a grey rectangle read as a
        // road from this angle.
        Block("Edge L", root.transform, new Vector3(-roadHalfWidth + paint * 2f, 0.01f, 0f),
              new Vector3(paint, 0.04f, length - paint * 4f), palette.line);
        Block("Edge R", root.transform, new Vector3(roadHalfWidth - paint * 2f, 0.01f, 0f),
              new Vector3(paint, 0.04f, length - paint * 4f), palette.line);

        float dash = roadLength * 0.05f;

        for (float z = -length / 2f + dash * 1.5f; z < length / 2f - dash * 1.5f; z += dash * 2.4f)
            Block("Dash", root.transform, new Vector3(0f, 0.01f, z), new Vector3(paint * 0.9f, 0.04f, dash), palette.line);
    }

    private void BuildTargets()
    {
        targets = new Transform[targetCount];
        targetHomes = new Vector3[targetCount];
        targetFacing = new Quaternion[targetCount];

        for (int i = 0; i < targetCount; i++)
        {
            GameObject targetObject = BuildTargetModel();

            Transform target = targetObject.transform;

            // Standing on the road, along it, with a little scatter so the row reads as people rather than as a
            // line of pins - and never off the tarmac, whatever size the road came out.
            float z = targetZ + i * targetSpacing;
            float x = Mathf.Clamp(Mathf.Sin(i * 2.4f) * roadHalfWidth * 0.45f,
                                  -roadHalfWidth * 0.6f, roadHalfWidth * 0.6f);

            target.localPosition = new Vector3(x, RestingHeight(targetObject), z);

            // Turned mostly towards the truck, with a little scatter of its own.
            target.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(i * 1.9f) * 40f, 0f);

            targets[i] = target;
            targetHomes[i] = target.localPosition;
            targetFacing[i] = target.localRotation;

            // The one the truck reaches is the one that comes apart. Only it: there is one target on the stage
            // today, and a row of them would only be there to be hit in turn, which is not what the card shows.
            if (i == 0)
            {
                Measure(targetObject);
                GatherPieces(targetObject);
            }
        }

        hitPoint = root.transform.TransformPoint(targets[0].localPosition);
    }

    /// <summary>
    /// What the target's own size settles: how far a piece of it is thrown, and how far short of it the truck
    /// has to stop.
    ///
    /// Both come from the model rather than from numbers picked here, so they hold whether the figure is a
    /// small one or a large one. The blast clears the target it came from instead of landing back on top of
    /// where it stood, and the stop is pulled up to the target's own front face with a hand's width to spare -
    /// which is what keeps the two from ever being seen overlapping, whatever the Adamak on the stage is
    /// shaped like, while still putting the truck right on top of it.
    /// </summary>
    private void Measure(GameObject targetObject)
    {
        Bounds drawn;

        if (!DrawnBounds(targetObject, out drawn)) return;

        Vector3 size = drawn.size;
        float across = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z));
        float up = Mathf.Abs(size.y);

        blastRange = Mathf.Clamp(Mathf.Max(across, up * 0.6f) * 2.4f + 1.1f, 1.5f, 6f);

        // How far back the target reaches towards the truck. Measured off the model, but kept off the extremes:
        // a model's bounds can take in something lying flat on the road - a base, a shadow plate - that reaches
        // much further than the figure standing on it, and stopping for that would leave the truck stranded
        // short of what it is meant to hit. So the reach is the smaller of what the model measures and what its
        // own width suggests, and never more than a quarter of a truck.
        float measured = targetZ - (drawn.min.z - root.transform.position.z);
        float reach = Mathf.Clamp(Mathf.Min(measured, across * 0.5f), 0f, truckLength * 0.25f);

        // Its nose stops a hand's width short of the face the target turns towards it: close enough to read as
        // the moment of contact, and never a hand's width inside it.
        float gap = Mathf.Max(0.12f, truckLength * 0.035f);

        hitZ = targetZ - reach - gap - truckLength * 0.5f;
    }

    /// <summary>
    /// Finds the pieces a target is made of and decides what each of them does when it is hit.
    ///
    /// The pieces are walked down to rather than named. A copy of one of the game's own prefabs is a wrapper
    /// around whatever its model is - the Adamak's own root holds a single <c>Parts</c> object, and it is that
    /// object's children (the body, the head, the hat) that read as pieces - so the first level of the tree
    /// with more than one child is the one used, and a model that is already a plain list of parts is it. A
    /// model with nothing to scatter comes back with nothing and simply stands where it is.
    /// </summary>
    private void GatherPieces(GameObject targetObject)
    {
        Transform current = targetObject.transform;

        for (int depth = 0; depth < 4; depth++)
        {
            if (current.childCount == 0) break;
            if (current.childCount > 1) break;

            current = current.GetChild(0);
        }

        pieces = new Transform[current.childCount];
        pieceHomes = new Vector3[pieces.Length];
        pieceFacing = new Quaternion[pieces.Length];
        pieceThrow = new Vector3[pieces.Length];
        pieceSpin = new Vector3[pieces.Length];
        pieceFall = new float[pieces.Length];

        Vector3 middle = Vector3.zero;

        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i] = current.GetChild(i);
            pieceHomes[i] = pieces[i].localPosition;
            pieceFacing[i] = pieces[i].localRotation;

            middle += pieceHomes[i];
        }

        if (pieces.Length > 0) middle /= pieces.Length;

        for (int i = 0; i < pieces.Length; i++)
        {
            // Where the piece leaves on: mostly its own random direction, so the parts do not all leave in a
            // line - which is what a stack of parts (a body, a head, a hat) would do if it went by where they
            // sit. Pulled outwards from the middle of the target and upwards, so it reads as a blast rather
            // than as a drop.
            Vector3 away = pieceHomes[i] - middle;
            away = away.sqrMagnitude > 1e-5f ? away.normalized : Vector3.up;

            float share = Random.Range(0.7f, 1.35f);

            // The throw is a distance in the world, but a piece hangs under a parent that is often scaled - the
            // Adamak's parts are several times over - and a local offset is multiplied by that parent's scale.
            // So the distance is brought into the parent's own units first, or a model scaled up twice would
            // throw its pieces twice as far as the blast reaches.
            Vector3 parentScale = pieces[i].parent != null ? pieces[i].parent.lossyScale : Vector3.one;
            float uniform = (Mathf.Abs(parentScale.x) + Mathf.Abs(parentScale.y) + Mathf.Abs(parentScale.z)) / 3f;
            float units = uniform > 1e-4f ? 1f / uniform : 1f;

            pieceThrow[i] = (Random.onUnitSphere + away * 0.5f + Vector3.up * 0.5f).normalized * (share * units);
            pieceSpin[i] = Random.onUnitSphere * Random.Range(240f, 720f);
            pieceFall[i] = share * units;
        }
    }

    /// <summary>
    /// The strike, as it is played: the target's pieces thrown out of the middle of it and tumbling, then
    /// dropping as they slow - a blast, and then falling debris rather than a jump.
    ///
    /// The speed is all in the first moment (the thrown part is a curve that is steep at the start and flat by
    /// the end), which is what tells the eye "this was hit" rather than "this was pushed". Before the hit, and
    /// over the seam of the loop, every piece is simply put back where the model has it.
    ///
    /// The blast is timed against the run (<paramref name="hitPhase"/>) rather than in seconds of its own, so it
    /// stays a third of the time the truck took however long the card's loop is set to: a piece of it is thrown
    /// and done in a moment, not left to drift.
    /// </summary>
    private void Explode(float sinceHit, float hitPhase)
    {
        if (pieces == null) return;

        float blast = Mathf.Clamp01(sinceHit / Mathf.Max(0.02f, hitPhase * 0.33f));
        float thrown = 1f - (1f - blast) * (1f - blast);
        float fall = 1.8f * blast * blast;

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null) continue;

            pieces[i].localPosition = pieceHomes[i]
                + pieceThrow[i] * (blastRange * thrown)
                + Vector3.down * (fall * pieceFall[i]);

            pieces[i].localRotation = pieceFacing[i] * Quaternion.Euler(pieceSpin[i] * thrown);
        }
    }

    /// <summary>Every piece back where the model has it, ready for the next pass of the loop.</summary>
    private void Repose()
    {
        if (pieces == null) return;

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null) continue;

            pieces[i].localPosition = pieceHomes[i];
            pieces[i].localRotation = pieceFacing[i];
        }
    }

    private void BuildCamera()
    {
        GameObject cameraObject = new GameObject("Tutorial Camera", typeof(Camera));

        camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;

        // Transparent, so the stage is drawn on the card's own paper rather than on a black box.
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = 1 << StageLayer;
        camera.fieldOfView = 34f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 200f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.depth = -10f;               // it draws into its own texture and never takes part in the level's

        texture = new RenderTexture((int)resolution.x, (int)resolution.y, 24, RenderTextureFormat.ARGB32);
        texture.name = "Tutorial Stage";
        texture.filterMode = FilterMode.Bilinear;
        texture.Create();

        camera.targetTexture = texture;
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.layer = StageLayer;

        FrameCamera();
    }

    /// <summary>
    /// Where the models are lit. Point lights, because a directional light would light the level as well: these
    /// reach a few dozen metres and stop, and they are 1500 metres below anything that matters.
    /// </summary>
    private void BuildLights()
    {
        // Put up against the strip's own size, so a big truck is not lit from inside its own bodywork and a
        // small one is not left in the dark.
        float reach = stripHeight;
        float range = Mathf.Max(8f, roadLength * 0.9f);

        AddLight("Key", new Vector3(roadHalfWidth * 1.8f, reach * 1.7f, truckStartZ * 0.6f), 1.6f, range, new Color(1f, 0.97f, 0.92f));
        AddLight("Fill", new Vector3(-roadHalfWidth * 2.4f, reach * 1.1f, targetZ * 0.8f), 0.8f, range, new Color(0.86f, 0.92f, 1f));
        AddLight("Rim", new Vector3(0f, reach * 1.3f, targetZ + targetSpacing), 0.6f, range, new Color(0.9f, 0.95f, 1f));
    }

    private void AddLight(string name, Vector3 position, float intensity, float range, Color colour)
    {
        GameObject lightObject = new GameObject(name + " Light", typeof(Light));
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = position;

        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = colour;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;   // a stage this small does not need them, and they cost
        light.renderMode = LightRenderMode.ForcePixel;

        lightObject.layer = StageLayer;
    }

    /// <summary>
    /// Points the camera at the strip from a settled angle and works out how far back it has to be for the whole
    /// strip to be inside the frame.
    ///
    /// From the camera towards the strip: it stands off the truck's flank, a little above the road, so the truck
    /// crosses the picture from the left to the targets on the right - the way the lesson reads. Seen end-on
    /// instead, a truck driving away shows the card almost nothing, and the drive is the whole point of the
    /// stage. Side-on, the strip's length becomes the picture's width, which is what lets the shot come this
    /// close.
    ///
    /// The distance is solved rather than guessed: with the camera's direction fixed, a point's place in the
    /// frame only depends on how far back the camera is (<c>tan = offset / (distance + depth)</c>), so each
    /// corner of the strip's box asks for a distance of its own and the largest is the one that fits them all.
    /// The frame's shape comes from the texture, so the stage frames itself for whatever card it is put in.
    /// </summary>
    private void FrameCamera()
    {
        Vector3 viewDirection = new Vector3(-0.86f, -0.34f, 0.2f).normalized;

        float farTargetZ = targetZ + (targetCount - 1) * targetSpacing;

        Vector3 centre = new Vector3(0f, stripHeight * 0.3f, (truckStartZ + farTargetZ) * 0.5f);

        // The box the picture has to hold: the strip the truck and the targets stand on, with a little slack at
        // each end. Tight rather than generous - this is the one number that decides how big the truck comes out,
        // and the road runs out of frame at both ends, which is what a road is supposed to do.
        Vector3 size = new Vector3(roadHalfWidth * 2f + truckWidth * 0.5f, stripHeight * 1.25f,
            Mathf.Abs(farTargetZ - truckStartZ) + truckLength * 0.25f);

        Vector3 half = size * 0.5f;

        Vector3 right = Vector3.Cross(Vector3.up, viewDirection).normalized;
        Vector3 up = Vector3.Cross(viewDirection, right).normalized;

        float tanVertical = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanHorizontal = tanVertical * Mathf.Max(0.05f, resolution.x / resolution.y);

        float distance = 6f;   // never closer than this, however small the models turn out to be

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 offset = new Vector3(
                (corner & 1) == 0 ? -half.x : half.x,
                (corner & 2) == 0 ? -half.y : half.y,
                (corner & 4) == 0 ? -half.z : half.z);

            float depthAlongView = Vector3.Dot(viewDirection, offset);
            float sideOffset = Mathf.Abs(Vector3.Dot(right, offset));
            float upOffset = Mathf.Abs(Vector3.Dot(up, offset));

            // tan = offset / (distance + depth), so the distance that just fits this corner is offset/tan -
            // depth. The furthest corner decides where the camera has to stand.
            distance = Mathf.Max(distance,
                sideOffset / tanHorizontal - depthAlongView,
                upOffset / tanVertical - depthAlongView);
        }

        distance *= 1.02f;   // a little air around the strip

        // The strip is described in the stage's own space while the camera is a child of the stage root, which
        // is parked 1500 metres below the world, so the two points it is placed and aimed by are put through the
        // root first. Without this the camera is set down near the world's own origin and films nothing but
        // empty sky: the models are all on the stage's layer, a kilometre and a half under it.
        camera.transform.position = root.transform.TransformPoint(centre) - viewDirection * distance;
        camera.transform.LookAt(root.transform.TransformPoint(centre + Vector3.up * 0.2f));
    }

    // ---------------------------------------------------------------- the loop

    /// <summary>
    /// One frame of the demonstration, at <paramref name="t"/> through the cycle. Same beats as the card: the
    /// truck runs the road down, reaches the first target at <paramref name="hitPhase"/> - stopping short of it,
    /// never overlapping it - and the target comes apart in front of it.
    /// </summary>
    public void Animate(float t, float hitPhase)
    {
        if (root == null) return;

        bool hit = t >= hitPhase;
        float sinceHit = hit ? t - hitPhase : 0f;

        float travel = Mathf.Clamp01(t / hitPhase);

        // Pulling away and putting its foot down. A share of the travel is linear so the truck is moving from
        // the first frame, and the rest grows with the square of the time, so it is faster at the end than it
        // was at the start - which is what makes the run read as a run rather than as a drift across a stage.
        // A smooth step, which starts and ends slow, would have it creeping up on the target and touching it,
        // and the whole point of the move is that it arrives at it.
        float eased = travel * (0.35f + 0.65f * travel);

        // Up the road, working its way across its lane, settling onto the target as it arrives. The lane change
        // and the turn of the nose that goes with it are both written in truck widths, so the drive reads the
        // same whatever the truck on the stage measures.
        float z = Mathf.Lerp(truckStartZ, hitZ, eased);
        float sway = Mathf.Sin(travel * Mathf.PI * 2.6f) * truckWidth * 0.22f * (1f - travel);

        if (truck != null)
        {
            truck.localPosition = new Vector3(truckHome.x + sway, truckHome.y, z);

            // Aimed slightly into the sway, the way a car is held straight on a road it is drifting across.
            truck.localRotation = Quaternion.Euler(0f, sway / truckLength * -45f, 0f);
        }

        // The target it meets: it comes apart the moment the truck is on it, and its own ground is left empty.
        if (targets != null && targets.Length > 0 && targets[0] != null)
        {
            Transform target = targets[0];

            target.localPosition = targetHomes[0];
            target.localRotation = targetFacing[0];
        }

        if (hit) Explode(sinceHit, hitPhase);
        else Repose();
    }

    public void Dispose()
    {
        if (root != null) Object.Destroy(root);

        if (texture != null)
        {
            if (camera != null) camera.targetTexture = null;
            texture.Release();

            Object.Destroy(texture);
        }

        root = null;
        camera = null;
        texture = null;
    }

    // ---------------------------------------------------------------- the shapes

    /// <summary>A flat box, which is what most of the two models and the whole of the road are made of.</summary>
    private GameObject Block(string name, Transform parent, Vector3 position, Vector3 size, Color colour)
    {
        return Shape(PrimitiveType.Cube, name, parent, position, size, colour, Quaternion.identity);
    }

    /// <summary>
    /// A cylinder, given its diameter and its height. Unity's own cylinder is one unit across and <em>two</em>
    /// units tall, so the height is halved here rather than at every call site - the drum's scale is in its own
    /// axes, where y is the half-height.
    /// </summary>
    private GameObject Drum(string name, Transform parent, Vector3 position, float diameter, float height, Color colour)
    {
        return Shape(PrimitiveType.Cylinder, name, parent, position,
            new Vector3(diameter, height * 0.5f, diameter), colour, Quaternion.identity);
    }

    /// <summary>A sphere, the size given being its diameter.</summary>
    private GameObject Ball(string name, Transform parent, Vector3 position, float diameter, Color colour)
    {
        return Shape(PrimitiveType.Sphere, name, parent, position,
            new Vector3(diameter, diameter, diameter), colour, Quaternion.identity);
    }

    /// <summary>
    /// A wheel: a cylinder turned onto its side so its axis runs across the truck, given its radius and its
    /// width.
    ///
    /// Turned and scaled in its own axes, so the cylinder's height - which was its y - becomes the tyre's width
    /// once the turn is applied, and both the radius and the width are halved the same way the drum's height is
    /// (the cylinder is two units tall and one across).
    /// </summary>
    private GameObject Wheel(string name, Transform parent, Vector3 position, float radius, float width, Color colour)
    {
        return Shape(PrimitiveType.Cylinder, name, parent, position,
            new Vector3(radius * 2f, width * 0.5f, radius * 2f), colour, Quaternion.Euler(0f, 0f, 90f));
    }

    /// <summary>
    /// One primitive, dressed as part of a model: parented, placed, scaled, coloured, taken off the physics
    /// layer it would otherwise be on, and stripped of the collider <c>CreatePrimitive</c> hands it - a film set
    /// has no colliders.
    /// </summary>
    private GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 size,
                             Color colour, Quaternion rotation)
    {
        GameObject shape = GameObject.CreatePrimitive(type);

        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localRotation = rotation;
        shape.transform.localScale = size;

        Collider collider = shape.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        MeshRenderer renderer = shape.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = Flat(colour);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        shape.layer = StageLayer;

        return shape;
    }

    private static Material Flat(Color colour)
    {
        string[] candidates = { "Standard", "Legacy Shaders/Diffuse", "Unlit/Color" };

        Shader shader = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            shader = Shader.Find(candidates[i]);
            if (shader != null) break;
        }

        Material material = new Material(shader);
        material.name = "Tutorial Stage (Runtime)";

        if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

        return material;
    }

    // ---------------------------------------------------------------- measuring a model

    /// <summary>
    /// How far up a model has to be lifted to stand on the road, with a floor under the answer.
    ///
    /// Measuring what the model draws answers it, but a measurement taken from a prefab can be wrong in ways
    /// that are invisible until the object is a speck off the top of the card: one stray renderer whose bounds
    /// reach far below the bodywork would bury the truck, and a bounds that came out as a NaN - which a
    /// component that is no longer quite all there will do - would hide it altogether. So no model is ever
    /// moved more than its own height, and a measurement that is not a number is read as no measurement.
    /// </summary>
    private static float RestingHeight(GameObject instance)
    {
        float lowest = LowestPoint(instance);

        if (float.IsNaN(lowest) || float.IsInfinity(lowest)) return 0f;

        Bounds drawn;
        float ownHeight = DrawnBounds(instance, out drawn) ? Mathf.Abs(drawn.size.y) : 0f;
        float limit = Mathf.Max(1f, ownHeight * 2f);

        return -Mathf.Clamp(lowest, -limit, limit);
    }

    /// <summary>
    /// How far the model's own drawing reaches below its origin, so it can be put down on the road rather than
    /// wherever its pivot happens to be. Measured before the object is moved, so this is its own local extents.
    /// </summary>
    private static float LowestPoint(GameObject instance)
    {
        Bounds drawn;

        if (!DrawnBounds(instance, out drawn)) return 0f;

        return instance.transform.InverseTransformPoint(drawn.min).y;
    }

    /// <summary>The size of what the model actually draws, in the model's own axes.</summary>
    private static Vector3 DrawnSize(GameObject instance)
    {
        Bounds drawn;

        if (!DrawnBounds(instance, out drawn)) return Vector3.zero;

        // Bounds are world-space boxes and the instance is put down unrotated and unscaled, so the box is
        // already measured in the model's own axes and its size can be read straight off - unless a measurement
        // came out as something that is not a number, which is read as no measurement at all.
        Vector3 size = drawn.size;

        if (float.IsNaN(size.x) || float.IsNaN(size.y) || float.IsNaN(size.z)) return Vector3.zero;
        if (float.IsInfinity(size.x) || float.IsInfinity(size.y) || float.IsInfinity(size.z)) return Vector3.zero;

        return size;
    }

    /// <summary>
    /// Everything the model draws that is geometry: its mesh renderers, and nobody else.
    ///
    /// Mesh renderers only, and that matters. A prefab can carry the renderers of the particle systems it no
    /// longer has - a smoking exhaust, a collision puff - and a particle renderer's bounds are not the model's
    /// bounds: they are the box a volume that is no longer emitted would have filled. Measured along with the
    /// bodywork, one of those puts the model's own "lowest point" tens of metres under the road, which is the
    /// same as hanging it tens of metres over it - and off the top of the card.
    /// </summary>
    private static bool DrawnBounds(GameObject instance, out Bounds bounds)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

        bounds = new Bounds();
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null) continue;
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;

                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return found;
    }

    private static void Layer(GameObject instance, int layer)
    {
        instance.layer = layer;

        Transform[] all = instance.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
    }

    /// <summary>
    /// Takes the stage's layer out of every camera in the scene that is not this one, so nothing here can ever
    /// be filmed into the game's own picture. The cinematic camera and the top-down camera are separate objects
    /// that can be switched on long after this runs, so every camera is asked, not just the active one.
    /// </summary>
    private void HideFromOtherCameras()
    {
        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null || cameras[i] == camera) continue;

            cameras[i].cullingMask &= ~(1 << StageLayer);
        }
    }
}
