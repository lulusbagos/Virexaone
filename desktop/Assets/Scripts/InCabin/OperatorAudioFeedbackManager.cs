using System;
using UnityEngine;

namespace Virexa.FMS.Mobile
{
    /// <summary>
    /// Generates in-cabin procedural audio alerts, voice cue chimes, overspeed warnings,
    /// and tactile acoustic feedback for the operator.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class OperatorAudioFeedbackManager : MonoBehaviour
    {
        public static OperatorAudioFeedbackManager Instance { get; private set; }

        private AudioSource audioSource;
        private AudioClip clickClip;
        private AudioClip warningBeepClip;
        private AudioClip chimeSuccessClip;
        private AudioClip alertSirenClip;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            GenerateProceduralAudioClips();
        }

        private void GenerateProceduralAudioClips()
        {
            clickClip = CreateTone(0.04f, 880f, 0.3f);
            warningBeepClip = CreateTone(0.18f, 1200f, 0.7f);
            chimeSuccessClip = CreateChime(0.35f, 587.33f, 880f, 0.6f);
            alertSirenClip = CreateTone(0.35f, 1600f, 0.8f);
        }

        private AudioClip CreateTone(float duration, float frequency, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1.0f - (t / duration); // Linear fade-out
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("ProceduralTone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateChime(float duration, float freq1, float freq2, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1.0f - Mathf.Pow(t / duration, 0.7f);
                float f = (t < duration * 0.4f) ? freq1 : freq2;
                samples[i] = Mathf.Sin(2 * Mathf.PI * f * t) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("ProceduralChime", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void PlayButtonClick()
        {
            if (audioSource != null && clickClip != null)
                audioSource.PlayOneShot(clickClip, 0.5f);
        }

        public void PlaySuccessChime()
        {
            if (audioSource != null && chimeSuccessClip != null)
                audioSource.PlayOneShot(chimeSuccessClip, 0.8f);
        }

        public void PlayWarningBeep()
        {
            if (audioSource != null && warningBeepClip != null)
                audioSource.PlayOneShot(warningBeepClip, 0.9f);
        }

        public void PlayDispatchAlert()
        {
            if (audioSource != null && alertSirenClip != null)
                audioSource.PlayOneShot(alertSirenClip, 1.0f);
        }
    }
}
