using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives one failing run of neon tubing through the arc a dying tube actually
/// follows, rather than uniform random blinking:
///
///   1. steady burn, several seconds
///   2. a short sag as the gas starts to lose the arc
///   3. a stutter storm whose gaps grow and whose strikes weaken as it goes
///   4. blackout, the tube gives up
///   5. one hard re-strike, brighter and sparkier the longer it was dark
///
/// Strike energy drives everything at once: the emission overshoot, how many
/// sparks come off the electrode, and how loud the buzz is. That coupling is
/// what makes the re-strike after a long blackout read as the big one.
///
/// Point this at the "Grp_NeonFlicker" child of the 老街酒馆 model -- the H's
/// right stem. Its tubes need a material with emission enabled in the
/// inspector: a MaterialPropertyBlock can change _EmissionColor but cannot
/// switch the _EMISSION shader keyword on.
/// </summary>
public sealed class NeonSignFlicker : MonoBehaviour
{
    [Header("Tubes")]
    [Tooltip("Renderers of the failing tube run. Left empty, every renderer under this object is used.")]
    [SerializeField] private Renderer[] tubeRenderers;

    [Tooltip("Optional light that shares the tube's rhythm, so the flicker spills onto the facade.")]
    [SerializeField] private Light spillLight;

    [Header("Emission")]
    [SerializeField] private Color emissionColor = new Color(0.31f, 0.76f, 1f);

    [Tooltip("Emission multiplier while the tube is burning normally.")]
    [SerializeField] private float litIntensity = 4f;

    [Tooltip("Emission multiplier while the tube is out. A little above zero keeps the glass reading as glass.")]
    [SerializeField] private float deadIntensity = 0.12f;

    [Tooltip("Emission multiplier on a full-energy strike. Weak strikes scale down from here.")]
    [SerializeField] private float strikeIntensity = 8f;

    [SerializeField] private float spillLightIntensityAtFull = 3f;

    [Header("Rhythm (seconds)")]
    [Tooltip("How long the tube holds a clean burn between episodes.")]
    [SerializeField] private Vector2 steadyDuration = new Vector2(3f, 9f);

    [Tooltip("The dip that warns an episode is coming.")]
    [SerializeField] private float sagDuration = 0.16f;

    [SerializeField, Range(0f, 1f)] private float sagDepth = 0.55f;

    [SerializeField] private Vector2Int strikesPerEpisode = new Vector2Int(3, 8);

    [Tooltip("Dark gap between strikes, at the start and at the end of an episode. It grows as the tube tires.")]
    [SerializeField] private Vector2 stutterGap = new Vector2(0.04f, 0.26f);

    [Tooltip("How long the tube holds after each strike inside an episode.")]
    [SerializeField] private Vector2 stutterHold = new Vector2(0.05f, 0.16f);

    [Tooltip("How far the burn fades across an episode, from the first strike to the last.")]
    [SerializeField, Range(0f, 1f)] private float episodeFade = 0.45f;

    [SerializeField] private Vector2 blackoutDuration = new Vector2(0.5f, 2.8f);

    [Header("Sparks")]
    [Tooltip("Emitters at the failing electrodes. Left empty, one is built at each SparkPoint_* transform found under the model root.")]
    [SerializeField] private ParticleSystem[] sparkEmitters;

    [Tooltip("Additive particle material for the sparks. Without one the emitters fall back to Unity's default and will need a material assigned by hand.")]
    [SerializeField] private Material sparkMaterial;

    [Tooltip("Spark count at full strike energy. Weak strikes scale down and may throw none at all.")]
    [SerializeField] private int sparksAtFullStrike = 10;

    [Tooltip("Strikes below this energy are too weak to throw anything.")]
    [SerializeField, Range(0f, 1f)] private float sparkEnergyThreshold = 0.45f;

    [Header("Sound")]
    [SerializeField] private AudioSource buzzSource;
    [SerializeField] private AudioClip[] buzzClips;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<Renderer> drivenRenderers = new List<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private Coroutine routine;
    private bool powered = true;

    /// <summary>
    /// Cuts power to the run, or lets it resume its own rhythm. Use this when a
    /// scene wants this circuit dark for a beat rather than merely on the blink.
    /// </summary>
    public bool Powered
    {
        get => powered;
        set
        {
            if (powered == value)
            {
                return;
            }

            powered = value;

            if (!isActiveAndEnabled)
            {
                return;
            }

            StopRoutine();

            if (powered)
            {
                routine = StartCoroutine(Run());
            }
            else
            {
                ApplyIntensity(0f);
            }
        }
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        CollectRenderers();

        if (sparkEmitters == null || sparkEmitters.Length == 0)
        {
            sparkEmitters = BuildSparkEmitters();
        }
    }

    private void OnEnable()
    {
        if (powered)
        {
            routine = StartCoroutine(Run());
        }
        else
        {
            ApplyIntensity(0f);
        }
    }

    private void OnDisable()
    {
        StopRoutine();
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void CollectRenderers()
    {
        drivenRenderers.Clear();

        if (tubeRenderers != null && tubeRenderers.Length > 0)
        {
            foreach (Renderer renderer in tubeRenderers)
            {
                if (renderer != null)
                {
                    drivenRenderers.Add(renderer);
                }
            }
        }
        else
        {
            GetComponentsInChildren(true, drivenRenderers);
        }

        if (drivenRenderers.Count == 0)
        {
            Debug.LogWarning($"{nameof(NeonSignFlicker)} on '{name}' found no tube renderers to drive.", this);
        }
    }

    private ParticleSystem[] BuildSparkEmitters()
    {
        Transform root = transform.parent != null ? transform.parent : transform;
        List<ParticleSystem> built = new List<ParticleSystem>();

        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name.StartsWith("SparkPoint", System.StringComparison.Ordinal))
            {
                built.Add(CreateSparkEmitter(candidate));
            }
        }

        return built.ToArray();
    }

    private ParticleSystem CreateSparkEmitter(Transform anchor)
    {
        GameObject holder = new GameObject($"Sparks_{anchor.name}");
        holder.transform.SetParent(anchor, false);

        ParticleSystem system = holder.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // The system idles at a zero emission rate and is kept playing, because
        // Emit() only simulates on a system that is running.
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.032f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.86f, 0.55f), new Color(1f, 0.55f, 0.22f));
        main.gravityModifier = 1.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.01f;
        // spit outward from the facade and slightly down, the way a shorting
        // electrode throws material off the tube
        shape.rotation = new Vector3(70f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer particleRenderer = holder.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = 0.06f;
        particleRenderer.lengthScale = 2.5f;

        if (sparkMaterial != null)
        {
            particleRenderer.material = sparkMaterial;
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(NeonSignFlicker)} on '{name}' built spark emitters without a spark material; "
                + "assign one or the particles will use Unity's built-in default.", this);
        }

        system.Play();
        return system;
    }

    private IEnumerator Run()
    {
        while (true)
        {
            ApplyIntensity(litIntensity);
            yield return new WaitForSeconds(Random.Range(steadyDuration.x, steadyDuration.y));

            // the sag: the arc starts to slip before it lets go
            ApplyIntensity(Mathf.Lerp(litIntensity, deadIntensity, sagDepth));
            yield return new WaitForSeconds(sagDuration);

            int strikes = Random.Range(strikesPerEpisode.x, strikesPerEpisode.y + 1);

            for (int index = 0; index < strikes; index++)
            {
                // progress runs 0 -> 1 across the episode: gaps lengthen, the
                // burn weakens, and the strikes lose their punch
                float progress = strikes > 1 ? index / (float)(strikes - 1) : 0f;

                ApplyIntensity(deadIntensity);
                yield return new WaitForSeconds(Mathf.Lerp(stutterGap.x, stutterGap.y, progress));

                Strike(Mathf.Lerp(1f, 0.3f, progress));
                yield return null;

                ApplyIntensity(Mathf.Lerp(litIntensity, litIntensity * (1f - episodeFade), progress));
                yield return new WaitForSeconds(Random.Range(stutterHold.x, stutterHold.y));
            }

            // the tube gives up
            ApplyIntensity(deadIntensity);
            float blackout = Random.Range(blackoutDuration.x, blackoutDuration.y);
            yield return new WaitForSeconds(blackout);

            // and comes back hard -- the longer it was out, the bigger the hit
            Strike(1f + Mathf.InverseLerp(blackoutDuration.x, blackoutDuration.y, blackout) * 0.6f);
            yield return null;
        }
    }

    /// <summary>
    /// One arc across the broken joint. Energy above 1 is a re-strike after a
    /// blackout; well below 1 is the tube guttering out mid-episode.
    /// </summary>
    private void Strike(float energy)
    {
        ApplyIntensity(strikeIntensity * energy);

        if (energy >= sparkEnergyThreshold)
        {
            int count = Mathf.RoundToInt(sparksAtFullStrike * energy * Random.Range(0.6f, 1.2f));

            if (count > 0)
            {
                foreach (ParticleSystem system in sparkEmitters)
                {
                    if (system != null)
                    {
                        system.Emit(count);
                    }
                }
            }
        }

        if (buzzSource != null && buzzClips != null && buzzClips.Length > 0)
        {
            AudioClip clip = buzzClips[Random.Range(0, buzzClips.Length)];

            if (clip != null)
            {
                buzzSource.pitch = Random.Range(0.88f, 1.14f);
                buzzSource.PlayOneShot(clip, Mathf.Clamp01(energy));
            }
        }
    }

    private void ApplyIntensity(float intensity)
    {
        Color emission = emissionColor * intensity;

        foreach (Renderer renderer in drivenRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, emission);
            renderer.SetPropertyBlock(propertyBlock);
        }

        if (spillLight != null)
        {
            spillLight.color = emissionColor;
            spillLight.intensity = spillLightIntensityAtFull * Mathf.Clamp01(intensity / Mathf.Max(litIntensity, 0.0001f));
        }
    }
}
