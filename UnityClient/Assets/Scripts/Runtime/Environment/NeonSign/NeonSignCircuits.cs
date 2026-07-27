using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the BLUE HOUR sign says depends on which of its four tube circuits are
/// carrying current. The glyphs were cut so the residues are words:
///
///   all four                  BLUE HOUR
///   H's right stem out        BLUE TOUR   (the H collapses to a "t")
///   the whole H out           BLUE OUR
///   H and "our" out           BLUE
///   "Blue" out                HOUR
///   "Blue" and H's stem out   TOUR
///   "Blue" and the whole H out OUR
///
/// The failing run -- H's right stem -- is on its own circuit and normally
/// driven by <see cref="NeonSignFlicker"/>, so the sign drifts between BLUE HOUR
/// and BLUE TOUR on its own. Call <see cref="Show"/> when a scene wants to hold
/// a particular reading instead.
///
/// Attach to the model root. The circuits are found by the group names the
/// Blender build emits, so nothing needs wiring by hand.
/// </summary>
public sealed class NeonSignCircuits : MonoBehaviour
{
    public enum Reading
    {
        BlueHour,
        BlueTour,
        BlueOur,
        Blue,
        Hour,
        Tour,
        Our,
        Dark,
    }

    [System.Flags]
    private enum Circuit
    {
        None = 0,
        Blue = 1 << 0,
        HStem = 1 << 1,
        HRight = 1 << 2,
        Our = 1 << 3,
    }

    private const string BlueGroup = "Grp_NeonBlue";
    private const string HStemGroup = "Grp_NeonHStem";
    private const string HRightGroup = "Grp_NeonFlicker";
    private const string OurGroup = "Grp_NeonOur";

    [Header("Circuits")]
    [Tooltip("Left empty, each circuit is collected from the matching Grp_Neon* child of this object.")]
    [SerializeField] private Renderer[] blueCircuit;
    [SerializeField] private Renderer[] hStemCircuit;
    [SerializeField] private Renderer[] hRightCircuit;
    [SerializeField] private Renderer[] ourCircuit;

    [Tooltip("Drives the failing circuit (H's right stem). Left empty, it is looked up in the children.")]
    [SerializeField] private NeonSignFlicker flicker;

    [Header("Emission")]
    [SerializeField] private Color emissionColor = new Color(0.31f, 0.76f, 1f);
    [SerializeField] private float litIntensity = 4f;
    [SerializeField] private float darkIntensity = 0.12f;

    [Header("Startup")]
    [SerializeField] private Reading initialReading = Reading.BlueHour;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock propertyBlock;
    private Reading current;

    /// <summary>The reading the sign is currently holding, ignoring the flicker.</summary>
    public Reading Current => current;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        blueCircuit = Resolve(blueCircuit, BlueGroup);
        hStemCircuit = Resolve(hStemCircuit, HStemGroup);
        hRightCircuit = Resolve(hRightCircuit, HRightGroup);
        ourCircuit = Resolve(ourCircuit, OurGroup);

        if (flicker == null)
        {
            flicker = GetComponentInChildren<NeonSignFlicker>(true);
        }
    }

    private void Start()
    {
        Show(initialReading);
    }

    /// <summary>Hold a reading. The failing circuit only flickers when the reading needs it lit.</summary>
    public void Show(Reading reading)
    {
        current = reading;
        Circuit live = LiveCircuits(reading);

        SetCircuit(blueCircuit, (live & Circuit.Blue) != 0);
        SetCircuit(hStemCircuit, (live & Circuit.HStem) != 0);
        SetCircuit(ourCircuit, (live & Circuit.Our) != 0);

        bool hRightLive = (live & Circuit.HRight) != 0;

        if (flicker != null)
        {
            // handing the circuit back to the flicker means it owns those
            // renderers again, so do not write to them here as well
            flicker.Powered = hRightLive;
        }
        else
        {
            SetCircuit(hRightCircuit, hRightLive);
        }
    }

    private static Circuit LiveCircuits(Reading reading)
    {
        switch (reading)
        {
            case Reading.BlueHour: return Circuit.Blue | Circuit.HStem | Circuit.HRight | Circuit.Our;
            case Reading.BlueTour: return Circuit.Blue | Circuit.HStem | Circuit.Our;
            case Reading.BlueOur: return Circuit.Blue | Circuit.Our;
            case Reading.Blue: return Circuit.Blue;
            case Reading.Hour: return Circuit.HStem | Circuit.HRight | Circuit.Our;
            case Reading.Tour: return Circuit.HStem | Circuit.Our;
            case Reading.Our: return Circuit.Our;
            default: return Circuit.None;
        }
    }

    private Renderer[] Resolve(Renderer[] assigned, string groupName)
    {
        if (assigned != null && assigned.Length > 0)
        {
            return assigned;
        }

        Transform group = FindGroup(groupName);

        if (group == null)
        {
            Debug.LogWarning($"{nameof(NeonSignCircuits)} on '{name}' found no '{groupName}' child.", this);
            return new Renderer[0];
        }

        return group.GetComponentsInChildren<Renderer>(true);
    }

    private Transform FindGroup(string groupName)
    {
        foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == groupName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void SetCircuit(IReadOnlyList<Renderer> circuit, bool live)
    {
        Color emission = emissionColor * (live ? litIntensity : darkIntensity);

        for (int index = 0; index < circuit.Count; index++)
        {
            Renderer renderer = circuit[index];

            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, emission);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
