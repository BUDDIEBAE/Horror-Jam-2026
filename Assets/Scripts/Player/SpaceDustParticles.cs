using System;
using UnityEngine;

namespace SpaceHorror.Player
{
    /// <summary>
    /// Spawns and manages a local cloud of floating micro-dust and ice crystals in Zero-G.
    /// Uses World Simulation Space so particulates drift past the astronaut's visor when moving or rotating,
    /// giving visceral visual feedback of inertia and speed in pitch black deep space.
    /// </summary>
    public class SpaceDustParticles : MonoBehaviour
    {
        [Header("Target Tracking")]
        [Tooltip("Transform that the dust emitter follows (usually MainCamera or Head)")]
        [SerializeField] private Transform followTarget;

        [Header("Particle Cloud Parameters")]
        [Tooltip("Maximum active dust motes in the volume")]
        [Range(50, 400)]
        [SerializeField] private int maxParticles = 160;

        [Tooltip("Dimensions of the spawn bounding volume around player in meters")]
        [SerializeField] private Vector3 spawnVolume = new Vector3(16f, 16f, 16f);

        [Tooltip("Minimum particle size")]
        [Range(0.01f, 0.1f)]
        [SerializeField] private float minParticleSize = 0.02f;

        [Tooltip("Maximum particle size")]
        [Range(0.02f, 0.2f)]
        [SerializeField] private float maxParticleSize = 0.05f;

        [Tooltip("Color tint and opacity of cosmic dust grains")]
        [SerializeField] private Color dustColor = new Color(0.85f, 0.92f, 1.0f, 0.55f);

        [Tooltip("Custom particle material. If null, automatically loads Resources/Materials/M_SpaceDust")]
        [SerializeField] private Material dustMaterial;

        private ParticleSystem ps;
        private ParticleSystemRenderer psRenderer;

        public void Initialize(Transform target, Material customMat = null)
        {
            followTarget = target;
            if (customMat != null) dustMaterial = customMat;
            SetupParticleSystem();
        }

        private void Awake()
        {
            if (followTarget == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) followTarget = cam.transform;
                else if (Camera.main != null) followTarget = Camera.main.transform;
                else followTarget = transform;
            }

            SetupParticleSystem();
        }

        private void Start()
        {
            PopulateInitialVolume();
        }

        private void LateUpdate()
        {
            // Only move transform if we don't have a Rigidbody attached (e.g. child object or standalone follower)
            if (followTarget != null && transform != followTarget && GetComponent<Rigidbody>() == null)
            {
                // Move emitter position to match player/camera.
                // Because simulationSpace is World, already-emitted particles maintain their world positions!
                transform.position = followTarget.position;
            }
        }

        /// <summary>
        /// Configures the ParticleSystem for deep space zero-g particulate drift.
        /// </summary>
        public void SetupParticleSystem()
        {
            ps = GetComponent<ParticleSystem>();
            if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();

            psRenderer = GetComponent<ParticleSystemRenderer>();
            if (psRenderer == null) psRenderer = gameObject.AddComponent<ParticleSystemRenderer>();

            // Resolve material
            if (dustMaterial == null)
            {
                dustMaterial = Resources.Load<Material>("Materials/M_SpaceDust");
                if (dustMaterial == null)
                {
                    Shader dustShader = Shader.Find("PSX/PSX_SpaceDust");
                    if (dustShader != null) dustMaterial = new Material(dustShader);
                }
            }

            if (dustMaterial != null)
            {
                psRenderer.material = dustMaterial;
            }

            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.alignment = ParticleSystemRenderSpace.View;

            // Main module
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startLifetime = 8.0f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
            main.startSize = new ParticleSystem.MinMaxCurve(minParticleSize, maxParticleSize);
            main.startColor = dustColor;
            main.gravityModifier = 0f; // Zero-G deep space

            // Emission module
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 20f;

            // Shape module
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = spawnVolume;

            // Velocity over lifetime (gentle micro-drift in zero-g)
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            // Color over lifetime (gentle fade-in and fade-out so particles don't pop)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.15f),
                    new GradientAlphaKey(1.0f, 0.85f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = gradient;
        }

        /// <summary>
        /// Seeds the volume with live particles on start so the player is immediately enveloped in space dust.
        /// </summary>
        private void PopulateInitialVolume()
        {
            if (ps == null) return;

            Vector3 center = followTarget != null ? followTarget.position : transform.position;
            int initialCount = maxParticles / 2;

            for (int i = 0; i < initialCount; i++)
            {
                Vector3 randomOffset = new Vector3(
                    UnityEngine.Random.Range(-spawnVolume.x * 0.5f, spawnVolume.x * 0.5f),
                    UnityEngine.Random.Range(-spawnVolume.y * 0.5f, spawnVolume.y * 0.5f),
                    UnityEngine.Random.Range(-spawnVolume.z * 0.5f, spawnVolume.z * 0.5f)
                );

                var emitParams = new ParticleSystem.EmitParams
                {
                    position = center + randomOffset,
                    velocity = UnityEngine.Random.insideUnitSphere * 0.02f,
                    startSize = UnityEngine.Random.Range(minParticleSize, maxParticleSize),
                    startLifetime = UnityEngine.Random.Range(3f, 8f),
                    startColor = dustColor
                };

                ps.Emit(emitParams, 1);
            }
        }
    }
}
