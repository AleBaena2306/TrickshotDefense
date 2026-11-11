using System.Collections.Generic;
using UnityEngine;

namespace TrickshotDefense
{
    // Efectos: Resources/Audio/sfx_<id>. Si falta un archivo, ese sonido simplemente no suena.
    // Los sonidos actuales son PROVISIONALES (Kenney): los definitivos se eligen en Publishing/tools/audicion-sonidos.html.
    public static class Sfx
    {
        public static bool Enabled = true;
        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private static AudioSource[] sources;
        private static int next;

        private static void Ensure()
        {
            if (sources != null && sources[0] != null) return;
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            sources = new AudioSource[10];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = go.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
        }

        public static void Play(string id, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (!Enabled) return;
            // El mismo sonido, como mucho cada 50 ms (muchos impactos a la vez no saturan)
            float now = Time.unscaledTime, last;
            if (lastPlayed.TryGetValue(id, out last) && now - last < 0.05f) return;
            lastPlayed[id] = now;
            AudioClip clip;
            if (!clips.TryGetValue(id, out clip))
            {
                clip = Resources.Load<AudioClip>("Audio/sfx_" + id);
                clips[id] = clip;
            }
            if (clip == null) return;
            Ensure();
            var s = sources[next];
            next = (next + 1) % sources.Length;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(clip, volume);
        }
    }

    // Música en bucle: Resources/Audio/music_menu y music_game
    public static class Music
    {
        private static AudioSource source;
        private static string current;
        private static bool enabled = true;

        public static bool Enabled
        {
            get { return enabled; }
            set { enabled = value; if (source != null) source.mute = !value; }
        }

        public static void Play(string id)
        {
            if (current == id) return;
            current = id;
            if (source == null)
            {
                var go = new GameObject("Music");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.loop = true;
                source.volume = 0.45f;
                source.playOnAwake = false;
                source.mute = !enabled;
            }
            var clip = Resources.Load<AudioClip>("Audio/music_" + id);
            source.Stop();
            source.clip = clip;
            if (clip != null) source.Play();
        }
    }
}
