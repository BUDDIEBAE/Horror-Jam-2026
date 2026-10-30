using System;
using UnityEngine;
using SpaceHorror.Interaction;

namespace SpaceHorror.Gameplay
{
    /// <summary>
    /// Auxiliary O2 Sub-Station Controller.
    /// Handles repair interactions, life support feedback, and spawns completion particle effects.
    /// Equipped with strict null safety-guards so that if no particle is assigned,
    /// interactions continue smoothly with zero errors.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MissionInteractable))]
    public class AuxiliaryO2Substation : MonoBehaviour
    {
        [Header("Particle Effects")]
        [Tooltip("Particle effect prefab instantiated when interaction/repair completes (e.g. O2 gas vapor hiss / repair sparks).")]
        [SerializeField] private GameObject interactionParticlePrefab;

        [Tooltip("Optional existing ParticleSystem component on this substation to play upon interaction completion.")]
        [SerializeField] private ParticleSystem interactionParticleSystem;

        [Tooltip("Custom spawn anchor for particle effect. If null, spawns at substation or prompt anchor position.")]
        [SerializeField] private Transform particleSpawnAnchor;

        [Header("Audio Feedback (Optional)")]
        [Tooltip("Optional sound clip played when repair completes")]
        [SerializeField] private AudioClip completionAudioClip;

        private MissionInteractable interactable;
        private AudioSource audioSource;

        public GameObject InteractionParticlePrefab
        {
            get => interactionParticlePrefab;
            set => interactionParticlePrefab = value;
        }

        public ParticleSystem InteractionParticleSystem
        {
            get => interactionParticleSystem;
            set => interactionParticleSystem = value;
        }

        public Transform ParticleSpawnAnchor
        {
            get => particleSpawnAnchor;
            set => particleSpawnAnchor = value;
        }

        // Auto-detect and attach to scene substations at runtime if missing
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBindSceneSubstations()
        {
            var interactables = UnityEngine.Object.FindObjectsByType<MissionInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < interactables.Length; i++)
            {
                var mi = interactables[i];
                if (mi == null) continue;

                string objName = mi.gameObject.name.ToUpperInvariant();
                string title = mi.ObjectiveTitle != null ? mi.ObjectiveTitle.ToUpperInvariant() : "";

                bool isSubstation = objName.Contains("SUBSTATION") || title.Contains("AUXILIARY O2") || title.Contains("REGULATOR");
                if (isSubstation)
                {
                    var existingSub = mi.GetComponent<AuxiliaryO2Substation>();
                    if (existingSub == null)
                    {
                        mi.gameObject.AddComponent<AuxiliaryO2Substation>();
                    }
                }
            }
        }

        private void Awake()
        {
            interactable = GetComponent<MissionInteractable>();
            SetupAudio();
            ConfigureListener();
        }

        private void SetupAudio()
        {
            if (completionAudioClip != null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f; // 3D positional acoustic
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 20f;
            }
        }

        private void ConfigureListener()
        {
            if (interactable == null) interactable = GetComponent<MissionInteractable>();
            if (interactable != null)
            {
                interactable.onInteracted.RemoveListener(HandleInteracted);
                interactable.onInteracted.AddListener(HandleInteracted);
            }
        }

        private void HandleInteracted(GameObject player)
        {
            SpawnInteractionParticles();

            if (audioSource != null && completionAudioClip != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(completionAudioClip, 0.9f);
            }
        }

        /// <summary>
        /// Spawns or plays the assigned interaction particle effect with strict null safety guards.
        /// </summary>
        public void SpawnInteractionParticles()
        {
            // Null safety-guard: Check if particle prefab is assigned
            if (interactionParticlePrefab != null)
            {
                Vector3 spawnPos = particleSpawnAnchor != null ? particleSpawnAnchor.position : transform.position;
                Quaternion spawnRot = particleSpawnAnchor != null ? particleSpawnAnchor.rotation : Quaternion.identity;

                GameObject spawned = Instantiate(interactionParticlePrefab, spawnPos, spawnRot);
                if (spawned != null)
                {
                    ParticleSystem ps = spawned.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Play();
                        float maxDuration = ps.main.duration + ps.main.startLifetime.constantMax;
                        Destroy(spawned, Mathf.Max(maxDuration, 3.5f));
                    }
                }
            }

            // Null safety-guard: Check if particle system component is assigned
            if (interactionParticleSystem != null)
            {
                interactionParticleSystem.Play();
            }
        }
    }
}
