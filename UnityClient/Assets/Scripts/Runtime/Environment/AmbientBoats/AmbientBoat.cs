using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves one ambient boat in a straight line, fades it in and out,
/// then destroys it. Attach this component to the root of a boat prefab.
/// </summary>
public sealed class AmbientBoat : MonoBehaviour
{
    [Header("Material Overrides")]
    [SerializeField] private Material bodyTransparentMaterial;
    [SerializeField] private Material lineTransparentMaterial;

    [Header("Fade")]
    [Tooltip("Use alpha fading when the boat materials support transparency.")]
    [SerializeField] private bool fadeUsingMaterialAlpha = true;

    [Tooltip("Optional fallback or additional stylized fade. Useful when an outline shader is opaque.")]
    [SerializeField] private bool fadeUsingScale = false;

    [Tooltip("If left empty, renderers are collected automatically from this object and its children.")]
    [SerializeField] private Renderer[] renderersToFade;

    private readonly List<MaterialColorBinding> materialBindings = new List<MaterialColorBinding>();
    private readonly List<Material> instantiatedMaterials = new List<Material>();

    private Vector3 direction = Vector3.forward;
    private Vector3 initialScale;
    private float speed = 1f;
    private float lifetime = 20f;
    private float fadeInDuration = 3f;
    private float fadeOutDuration = 4f;
    private float age;
    private bool initialized;
    private Action<AmbientBoat> onFinished;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int LineColorId = Shader.PropertyToID("_LineColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private struct MaterialColorBinding
    {
        public Material material;
        public int propertyId;
        public Color originalColor;

        public MaterialColorBinding(Material material, int propertyId, Color originalColor)
        {
            this.material = material;
            this.propertyId = propertyId;
            this.originalColor = originalColor;
        }
    }

    private void Awake()
    {
        initialScale = transform.localScale;
        PrepareFadeMaterials();
    }

    public void Initialize(
        Vector3 sailingDirection,
        float sailingSpeed,
        float sailingLifetime,
        float fadeIn,
        float fadeOut,
        Action<AmbientBoat> finishedCallback
    )
    {
        direction = sailingDirection.sqrMagnitude > 0.0001f
            ? sailingDirection.normalized
            : transform.forward;

        speed = Mathf.Max(0f, sailingSpeed);
        lifetime = Mathf.Max(0.05f, sailingLifetime);
        fadeInDuration = Mathf.Max(0f, fadeIn);
        fadeOutDuration = Mathf.Max(0f, fadeOut);
        onFinished = finishedCallback;
        age = 0f;
        initialized = true;

        transform.forward = direction;
        ApplyVisibility(0f);
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        transform.position += direction * speed * Time.deltaTime;
        age += Time.deltaTime;

        float alpha = CalculateVisibility(age, lifetime, fadeInDuration, fadeOutDuration);
        ApplyVisibility(alpha);

        if (age >= lifetime)
        {
            onFinished?.Invoke(this);
            Destroy(gameObject);
        }
    }

    private static float CalculateVisibility(float currentAge, float totalLifetime, float fadeIn, float fadeOut)
    {
        float fadeInAlpha = fadeIn <= 0f
            ? 1f
            : Mathf.Clamp01(currentAge / fadeIn);

        float remaining = totalLifetime - currentAge;

        float fadeOutAlpha = fadeOut <= 0f
            ? 1f
            : Mathf.Clamp01(remaining / fadeOut);

        float alpha = Mathf.Min(fadeInAlpha, fadeOutAlpha);
        return alpha * alpha * (3f - 2f * alpha);
    }

    private void PrepareFadeMaterials()
    {
        if (renderersToFade == null || renderersToFade.Length == 0)
        {
            renderersToFade = GetComponentsInChildren<Renderer>(true);
        }

        if (!fadeUsingMaterialAlpha)
        {
            return;
        }

        // Get materials from assignable fields, fallback to Resources.Load
        Material customBodyMat = bodyTransparentMaterial != null 
            ? bodyTransparentMaterial 
            : Resources.Load<Material>("Materials/Boat_DarkColor_Transparent");

        Material customLineMat = lineTransparentMaterial != null
            ? lineTransparentMaterial
            : Resources.Load<Material>("Materials/Boat_LineColor_Transparent");

        foreach (Renderer targetRenderer in renderersToFade)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] sharedMats = targetRenderer.sharedMaterials;
            bool replacedAny = false;

            for (int i = 0; i < sharedMats.Length; i++)
            {
                Material originalMat = sharedMats[i];
                if (originalMat == null)
                {
                    continue;
                }

                string originalName = originalMat.name;
                if (customBodyMat != null && IsBodyMaterialName(originalName))
                {
                    sharedMats[i] = customBodyMat;
                    replacedAny = true;
                }
                else if (customLineMat != null && IsLineMaterialName(originalName))
                {
                    sharedMats[i] = customLineMat;
                    replacedAny = true;
                }
            }

            if (replacedAny)
            {
                targetRenderer.sharedMaterials = sharedMats;
            }

            Material[] materials = targetRenderer.materials;

            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                instantiatedMaterials.Add(material);
                TryAddBinding(material, BaseColorId);
                TryAddBinding(material, ColorId);
                TryAddBinding(material, LineColorId);
                TryAddBinding(material, EmissionColorId);
            }
        }

        if (materialBindings.Count == 0)
        {
            Debug.LogWarning(
                "AmbientBoat: no fadeable material color properties were found. Check the boat renderer materials and transparent material references.",
                this
            );
        }
    }

    private static bool IsBodyMaterialName(string materialName)
    {
        return materialName.Contains("M_Dark_Blue_Model") ||
            materialName.Contains("Material.001") ||
            materialName.Contains("Boat_DarkColor");
    }

    private static bool IsLineMaterialName(string materialName)
    {
        return materialName.Contains("M_White_Emission_Lines") ||
            materialName.Contains("White_Emission_Lines") ||
            materialName.Contains("Boat_LineColor") ||
            materialName.Contains("OutlineLines");
    }

    private void TryAddBinding(Material material, int propertyId)
    {
        if (!material.HasProperty(propertyId))
        {
            return;
        }

        Color originalColor = material.GetColor(propertyId);
        materialBindings.Add(
            new MaterialColorBinding(
                material,
                propertyId,
                originalColor
            )
        );
    }

    private void ApplyVisibility(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (fadeUsingMaterialAlpha)
        {
            foreach (MaterialColorBinding binding in materialBindings)
            {
                Color color = binding.originalColor;
                if (binding.propertyId == EmissionColorId)
                {
                    color.r *= alpha;
                    color.g *= alpha;
                    color.b *= alpha;
                }
                else
                {
                    color.a *= alpha;
                }
                binding.material.SetColor(binding.propertyId, color);
            }
        }

        if (fadeUsingScale)
        {
            transform.localScale = initialScale * alpha;
        }
    }

    private void OnDestroy()
    {
        foreach (Material material in instantiatedMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
