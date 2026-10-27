using System;
using UnityEngine;
using SpaceHorror.Player;
using SpaceHorror.Interaction;
using SpaceHorror.UI;

namespace SpaceHorror.Gameplay
{
    /// <summary>
    /// Central manager for scripted horror anomalies and loop events across expedition days.
    /// Manages:
    /// - Day 2: Event 0 - Catastrophic O2 Substation Rupture (Forces suit integrity to 15%, creating the 1st corpse)
    /// - Day 3: Event 1 - The Unsettled Corpse (Retrieving flight telemetry from player's Day 2 corpse)
    /// - Day 4: Event 2 - Phantom Frequency 00.0 MHz (Signal tracker anomaly)
    /// </summary>
    public class HorrorEventManager : MonoBehaviour
    {
        private static HorrorEventManager instance;
        public static HorrorEventManager Instance => instance;

        [Header("Event 0: Catastrophic Rupture (Day 2)")]
        [Tooltip("Force of the explosive decompression blast throwing the astronaut into zero-g spin")]
        [SerializeField] private float explosionBlastForce = 16.0f;

        [Tooltip("Rotational torque applied to player upon explosion for disorienting 6DoF tumble")]
        [SerializeField] private float explosionTorque = 10.0f;

        [Tooltip("Target suit integrity percent forced upon rupture (leaves 15% remaining = 85% catastrophic leak)")]
        [SerializeField] private float ruptureSuitIntegrityPercent = 15.0f;

        [Tooltip("Duration of the blinding stark white chiaroscuro noir flash on the helmet visor")]
        [SerializeField] private float flashDuration = 0.45f;

        [Tooltip("Optional custom audio clip for the deep vacuum explosion thump. If empty, generates procedural sound.")]
        [SerializeField] private AudioClip customExplosionClip;

        [Header("Runtime State")]
        [SerializeField] private bool hasTriggeredO2Explosion = false;
        [SerializeField] private bool hasTriggeredCorpseEvent = false;
        [SerializeField] private bool hasTriggeredPhantomEvent = false;

        private AudioSource horrorAudioSource;
        private AudioClip proceduralExplosionClip;

        public bool HasTriggeredO2Explosion => hasTriggeredO2Explosion;
        public bool HasTriggeredCorpseEvent => hasTriggeredCorpseEvent;
        public bool HasTriggeredPhantomEvent => hasTriggeredPhantomEvent;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            SetupAudioSource();
        }

        private void SetupAudioSource()
        {
            horrorAudioSource = gameObject.GetComponent<AudioSource>();
            if (horrorAudioSource == null)
            {
                horrorAudioSource = gameObject.AddComponent<AudioSource>();
            }
            horrorAudioSource.playOnAwake = false;
            horrorAudioSource.spatialBlend = 0f; // In-helmet conduction
            horrorAudioSource.volume = 1.0f;

            if (customExplosionClip == null)
            {
                proceduralExplosionClip = GenerateProceduralExplosion();
            }
        }

        [Tooltip("If true, the explosion only triggers when the player completes the LAST station of Day 2. If false, triggers on the first one.")]
        [SerializeField] private bool triggerOnLastStationOnly = true;

        public bool TriggerOnLastStationOnly
        {
            get => triggerOnLastStationOnly;
            set => triggerOnLastStationOnly = value;
        }

        /// <summary>
        /// Called by DayCycleManager when any mission objective is completed.
        /// Evaluates whether the completed objective triggers a scripted horror sequence.
        /// </summary>
        public void HandleObjectiveCompleted(int dayNumber, MissionObjectiveData mission, GameObject player, bool isLastObjective = true)
        {
            if (mission == null) return;

            // Day 2 Event 0: O2 Station Catastrophic Rupture
            if (dayNumber == 2 && !hasTriggeredO2Explosion)
            {
                // If configured to trigger only on the final station, wait until all other stations are completed
                if (triggerOnLastStationOnly && !isLastObjective)
                {
                    Debug.Log($"<color=#FFFFFF>[HorrorEventManager]</color> O2 Station '{mission.directiveText}' repaired safely. Pending remaining stations before rupture.");
                    return;
                }

                bool isO2Station = (!string.IsNullOrEmpty(mission.beaconTag) && mission.beaconTag.IndexOf("O2", StringComparison.OrdinalIgnoreCase) >= 0)
                                   || (!string.IsNullOrEmpty(mission.directiveText) && mission.directiveText.IndexOf("O2", StringComparison.OrdinalIgnoreCase) >= 0);

                if (isO2Station)
                {
                    GameObject stationObj = mission.targetInteractable != null ? mission.targetInteractable.gameObject : null;
                    TriggerO2StationRupture(stationObj, player);
                }
            }
        }

        /// <summary>
        /// Executes Event 0: The Catastrophic Rupture.
        /// - Explosive decompression knocks the astronaut back in 6DoF zero-g
        /// - Visor blinding Film Noir white flash
        /// - Forces suit integrity to 15% (85% leak rate, rapid suffocation)
        /// - Cuts station radio beacon abruptly
        /// - Deep muffled hull explosion audio
        /// </summary>
        [ContextMenu("Trigger O2 Station Rupture")]
        public void TriggerO2StationRupture(GameObject stationObj = null, GameObject playerObj = null)
        {
            if (hasTriggeredO2Explosion) return;
            hasTriggeredO2Explosion = true;

            // Resolve Player References
            if (playerObj == null)
            {
                var survival = FindFirstObjectByType<AstronautSurvival>();
                if (survival != null) playerObj = survival.gameObject;
            }

            if (playerObj == null)
            {
                Debug.LogWarning("<color=#FF4444>[HorrorEventManager]</color> Cannot trigger O2 rupture: Player object not found!");
                return;
            }

            // Resolve Station Object
            if (stationObj == null)
            {
                var o2Substation = GameObject.Find("Auxiliary_O2_Substation");
                if (o2Substation != null) stationObj = o2Substation;
            }

            Vector3 stationPos = stationObj != null ? stationObj.transform.position : playerObj.transform.position + playerObj.transform.forward * 1.5f;

            // 1. Violent 6DoF Zero-G Explosive Recoil
            Rigidbody playerRb = playerObj.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 blastDir = (playerObj.transform.position - stationPos).normalized;
                if (blastDir.sqrMagnitude < 0.01f) blastDir = -playerObj.transform.forward;

                // Add slight upward zero-g kick
                blastDir = (blastDir + UnityEngine.Random.insideUnitSphere * 0.35f).normalized;

                playerRb.AddForce(blastDir * explosionBlastForce, ForceMode.Impulse);

                // Add chaotic tumble torque
                Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * explosionTorque;
                playerRb.AddTorque(randomTorque, ForceMode.Impulse);
            }

            // 2. Helmet Visor Noir Stark White Flash
            AstronautHelmetHUD hud = FindFirstObjectByType<AstronautHelmetHUD>();
            if (hud != null)
            {
                hud.TriggerNoirFlash(flashDuration);
                hud.ShowInteractionStatusMessage("[!] CATASTROPHIC BREACH // SUIT INTEGRITY 15%", 5.0f);
            }

            // 3. Force Suit Integrity to 15% (Inescapable 85% Leak Rate)
            AstronautSurvival playerSurvival = playerObj.GetComponent<AstronautSurvival>();
            if (playerSurvival != null)
            {
                playerSurvival.ForceSuitIntegrity(ruptureSuitIntegrityPercent, true);
            }

            // 4. Play Deep Vacuum Explosion Thump
            AudioClip clipToPlay = customExplosionClip != null ? customExplosionClip : proceduralExplosionClip;
            if (horrorAudioSource != null && clipToPlay != null)
            {
                horrorAudioSource.pitch = UnityEngine.Random.Range(0.92f, 1.05f);
                horrorAudioSource.PlayOneShot(clipToPlay, 1.0f);
            }

            // 5. Shatter and obliterate the ruptured O2 Station from the Scene
            if (stationObj != null)
            {
                SignalBeacon beacon = stationObj.GetComponent<SignalBeacon>();
                if (beacon != null)
                {
                    beacon.SetTransmitting(false);
                }

                // Station is shattered by catastrophic explosive decompression - completely removed from the scene
                stationObj.SetActive(false);
            }

            Debug.LogWarning($"<color=#FFFFFF><b>[HorrorEventManager]</b></color> EVENT 0 TRIGGERED: O2 Substation ruptured! Suit integrity reduced to {ruptureSuitIntegrityPercent}%.");
        }

        /// <summary>
        /// Procedurally generates a deep, muffled space-suit vacuum explosion thump.
        /// Concussive sub-bass waveform decaying into muted mechanical static.
        /// </summary>
        private AudioClip GenerateProceduralExplosion()
        {
            int sampleRate = 44100;
            float duration = 1.2f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 4.5f); // Fast exponential decay

                // Pitch envelope drops rapidly from 90Hz to 32Hz (sub-bass concussion)
                float freq = Mathf.Lerp(90f, 32f, t / duration);
                float subBass = Mathf.Sin(2f * Mathf.PI * freq * t);

                // Muffled noise component (debris / gas decompression)
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.35f;

                samples[i] = (subBass * 0.75f + noise * 0.25f) * envelope;
            }

            AudioClip clip = AudioClip.Create("Procedural_Vacuum_Explosion", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void ResetForNewGame()
        {
            hasTriggeredO2Explosion = false;
            hasTriggeredCorpseEvent = false;
            hasTriggeredPhantomEvent = false;
        }
    }
}
