// Replace the shadow's one big fire volume with a set of small fires bolted to the truck's parts.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const file = path.join(root, 'Assets/Scripts/Player/ShadowRacer.cs');

let text = fs.readFileSync(file, 'utf8');
const wasCrlf = text.includes('\r\n');
if (wasCrlf) text = text.split('\r\n').join('\n');

const startMarker = '    /// <summary>\n    /// The black fire that comes up out of the shadow:';
const endMarker = '        return bounds;\n    }\n';

const start = text.indexOf(startMarker);
const end = text.indexOf(endMarker, start);
if (start < 0 || end < 0) {
  console.error('markers not found', start, end);
  process.exit(1);
}

const block = `    /// <summary>
    /// The black fire that comes off the shadow: small tongues of flame burning from the truck's own parts -
    /// its wheels, its engine, its lamps - rather than one cloud around the whole shape.
    ///
    /// That is what makes it read as a car that is alight rather than as a truck sitting in smoke: fire on a
    /// burning car catches in places, and a handful of small fires fixed to the parts flames belong to look
    /// like the truck itself is burning. Each fire is bolted to a point on the body, so it turns and travels
    /// with the truck and stays on the part it belongs to.
    ///
    /// Every tongue is short-lived and climbs, which is what makes a flame rather than a puff; the rendered
    /// billboards are stretched a little along their own motion so a rising dot reads as a tongue.
    /// </summary>
    private void BuildAura()
    {
        puffTexture = CreateFlameTexture();
        puffMaterial = CreatePuffMaterial(puffTexture);

        // The places a car burns: its wheels, its engine and its lamps. A part a given truck does not have is
        // simply skipped, so this is safe on any model.
        List<Transform> parts = new List<Transform>();

        for (int i = 0; i < BurnPartNames.Length; i++)
        {
            Transform part = FindDeep(body, BurnPartNames[i]);
            if (part != null) parts.Add(part);
        }

        // None of the known parts (a different truck): light the whole body instead, so there is still fire.
        if (parts.Count == 0)
            parts.Add(body);

        for (int i = 0; i < parts.Count; i++)
            BuildFlameEmitter(parts[i]);
    }

    /// <summary>One small fire, bolted to a point on the truck: a low ring of short flames climbing off it.</summary>
    private void BuildFlameEmitter(Transform part)
    {
        GameObject emitter = new GameObject("Flame - " + part.name);
        emitter.transform.SetParent(transform, false);
        emitter.transform.localRotation = Quaternion.identity;
        emitter.transform.localScale = Vector3.one;

        // The part's place on the truck, in the truck's own space, so the fire sits on the part and turns
        // with it.
        emitter.transform.localPosition =
            transform.InverseTransformPoint(part.position) + Vector3.up * particleHeight;

        ParticleSystem system = emitter.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleRadius * 0.12f, particleRadius * 0.28f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        // Left white on purpose: the colour the flames actually show is the gradient over their life, which
        // is what lets a tongue start at the truck's colour and bloom into the flame colour.
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifier = -0.09f;                          // fire climbs, and keeps climbing
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = Mathf.Max(8, GraphicsQuality.ScaleCount(particleMax, GraphicsQuality.ParticleScale, 8));
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = GraphicsQuality.ScaleRate(particleRate, GraphicsQuality.ParticleScale, 4f);

        // Born on a small shell around the part, so the flames start on the part itself.
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = emitterRadius;
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 0.7f;

        // A push in every direction, up included, so the fire comes off the part all the way round rather than
        // standing up in a column. All three axes are set to the same kind of curve - two constants - because
        // a particle system requires its velocity curves to agree on their mode, and mixing them is a
        // runtime error.
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;

        float rise = particleRadius * 1.2f;
        float drift = particleRadius * 0.8f;

        velocity.x = new ParticleSystem.MinMaxCurve(-drift, drift);
        velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.7f, rise * 1.7f);
        velocity.z = new ParticleSystem.MinMaxCurve(-drift, drift);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(particleRadius * 0.1f, particleRadius * 0.3f);
        noise.frequency = 1.3f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, FlameCurve());

        ParticleSystem.ColorOverLifetimeModule colourOverLife = system.colorOverLifetime;
        colourOverLife.enabled = true;
        colourOverLife.color = new ParticleSystem.MinMaxGradient(FlameGradient());

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = puffMaterial;

            // Stretched along its own motion, but only a little: enough to read as a tongue of flame rather
            // than a dot, without the fast-rising ones drawing out into long streaks.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1.2f;
            renderer.cameraVelocityScale = 0f;

            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        system.Play();
    }

    /// <summary>Finds a descendant by name, to hang a fire on.</summary>
    private static Transform FindDeep(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name) return all[i];
        }

        return null;
    }
`;

const patched = text.slice(0, start) + block + text.slice(end + endMarker.length);

fs.writeFileSync(file, wasCrlf ? patched.split('\n').join('\r\n') : patched, 'utf8');
console.log('patched BuildAura; file now ' + patched.split('\n').length + ' lines');
