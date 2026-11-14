using UnityEngine;

namespace SalonIntern
{
    // 효과음과 배경음. 소리 파일 없이 코드로 파형을 만들어서 낸다.
    public static class Sfx
    {
        public enum Kind { Click, Good, Bad, Coin, Fanfare, Snip, Water, Pop }

        const int Rate = 22050;
        const string PrefKey = "SalonIntern.Sound";

        static AudioSource sfxSource;
        static AudioSource bgmSource;
        static AudioClip[] clips;
        static AudioClip bgm;
        static bool loaded;
        static bool muted;
        static float volume = 1f;
        const string VolKey = "SalonIntern.Volume";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sfxSource = null;
            bgmSource = null;
            clips = null;
            bgm = null;
            loaded = false;
        }

        public static bool Muted
        {
            get
            {
                if (!loaded) Load();
                return muted;
            }
        }

        static void Load()
        {
            loaded = true;
            try { muted = PlayerPrefs.GetInt(PrefKey, 1) == 0; volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolKey, 1f)); }
            catch (System.Exception) { muted = false; volume = 1f; }
        }

        // 음량 0~1. 효과음과 배경음에 함께 곱해진다.
        public static float Volume
        {
            get
            {
                if (!loaded) Load();
                return volume;
            }
        }

        public static void SetVolume(float v)
        {
            if (!loaded) Load();
            volume = Mathf.Clamp01(v);
            try { PlayerPrefs.SetFloat(VolKey, volume); PlayerPrefs.Save(); }
            catch (System.Exception) { }
            if (Application.isPlaying && bgmSource != null) bgmSource.volume = 0.22f * volume;
        }

        static void Ensure()
        {
            if (!loaded) Load();
            if (sfxSource != null) return;

            GameObject go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            sfxSource = go.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            bgmSource = go.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.volume = 0.22f * volume;

            clips = new AudioClip[8];
            clips[(int)Kind.Click] = Make("click", Blip(880f, 0.05f, 0.5f));
            clips[(int)Kind.Good] = Make("good", Seq(new[] { 523f, 659f, 784f }, 0.09f, 0.5f));
            clips[(int)Kind.Bad] = Make("bad", Slide(260f, 140f, 0.30f, 0.5f));
            clips[(int)Kind.Coin] = Make("coin", Seq(new[] { 988f, 1319f }, 0.08f, 0.45f));
            clips[(int)Kind.Fanfare] = Make("fanfare", Seq(new[] { 523f, 659f, 784f, 1047f }, 0.13f, 0.5f));
            clips[(int)Kind.Snip] = Make("snip", Noise(0.07f, 0.35f, 0.05f, 0.0f, 2));
            clips[(int)Kind.Water] = Make("water", Noise(0.35f, 0.18f, 0.0f, 0.5f, 1));
            clips[(int)Kind.Pop] = Make("pop", Slide(500f, 900f, 0.07f, 0.45f));
        }

        public static void Play(Kind kind)
        {
            if (!Application.isPlaying) return;
            Ensure();
            if (muted) return;
            AudioClip c = clips[(int)kind];
            if (c != null) sfxSource.PlayOneShot(c, 0.7f * volume);
        }

        public static void StartBgm()
        {
            if (!Application.isPlaying) return;
            Ensure();
            if (bgm == null) bgm = Make("bgm", BuildBgm());
            bgmSource.clip = bgm;
            if (!muted && !bgmSource.isPlaying) bgmSource.Play();
        }

        public static void Toggle()
        {
            if (!Application.isPlaying) return;
            Ensure();
            muted = !muted;
            try { PlayerPrefs.SetInt(PrefKey, muted ? 0 : 1); PlayerPrefs.Save(); }
            catch (System.Exception) { }
            if (muted) bgmSource.Pause();
            else
            {
                if (bgmSource.clip != null) bgmSource.UnPause();
                if (bgmSource.clip != null && !bgmSource.isPlaying) bgmSource.Play();
                Play(Kind.Click);
            }
        }

        // ---------- 파형 만들기 ----------

        static AudioClip Make(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Env(int i, int n)
        {
            float t = (float)i / n;
            float attack = Mathf.Clamp01(i / (Rate * 0.004f));
            return attack * (1f - t) * (1f - t);
        }

        static float[] Blip(float freq, float dur, float vol)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            float[] d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Sin(2f * Mathf.PI * freq * i / Rate) * vol * Env(i, n);
            return d;
        }

        static float[] Slide(float f0, float f1, float dur, float vol)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            float[] d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float f = Mathf.Lerp(f0, f1, (float)i / n);
                phase += 2f * Mathf.PI * f / Rate;
                d[i] = Mathf.Sin(phase) * vol * Env(i, n);
            }
            return d;
        }

        static float[] Seq(float[] freqs, float noteDur, float vol)
        {
            int per = Mathf.RoundToInt(Rate * noteDur);
            int tail = Mathf.RoundToInt(Rate * 0.12f);
            float[] d = new float[per * freqs.Length + tail];
            for (int k = 0; k < freqs.Length; k++)
            {
                int len = per + tail;
                for (int i = 0; i < len; i++)
                {
                    int idx = k * per + i;
                    if (idx >= d.Length) break;
                    float s = Mathf.Sin(2f * Mathf.PI * freqs[k] * i / Rate) + 0.3f * Mathf.Sin(4f * Mathf.PI * freqs[k] * i / Rate);
                    d[idx] += s * vol * 0.6f * Env(i, len);
                }
            }
            return d;
        }

        // 잡음 소리. mode 1은 부드러운(물소리), 2는 날카로운(가위질)
        static float[] Noise(float dur, float vol, float gap, float swell, int mode)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            float[] d = new float[n];
            System.Random rng = new System.Random(7);
            float lp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                float s;
                if (mode == 1) { lp += (w - lp) * 0.25f; s = lp; }
                else { s = w - prev; prev = w; }
                float t = (float)i / n;
                float env = mode == 1 ? Mathf.Sin(Mathf.PI * t) : Env(i, n);
                if (mode == 2 && t > 0.45f && t < 0.55f + gap) env *= 0.1f; // 가위 두 번 "싹둑"
                d[i] = s * vol * env * (1f + swell * Mathf.Sin(40f * t));
            }
            return d;
        }

        // 간단한 배경 음악: 펜타토닉 멜로디와 낮은 반주를 반복한다 (약 21초)
        static float[] BuildBgm()
        {
            float bpm = 96f;
            float beat = 60f / bpm;
            int beats = 32;
            int n = Mathf.RoundToInt(Rate * beat * beats);
            float[] d = new float[n];

            float[] scale = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f }; // 도 레 미 솔 라 높은 도
            int[] melody =
            {
                2, -1, 3, 4, 3, -1, 2, 0,   4, -1, 5, 4, 3, -1, 2, -1,
                0, -1, 2, 3, 2, -1, 0, 1,   2, -1, 3, 2, 1, -1, 0, -1,
            };
            float[] bass = { 130.81f, 196f, 220f, 174.61f };

            for (int b = 0; b < beats; b++)
            {
                // 베이스: 두 박자마다 한 음
                if (b % 2 == 0) AddNote(d, b * beat, beat * 2f, bass[(b / 8) % bass.Length], 0.16f, 0.15f);
                // 멜로디: 한 박자 간격으로 8분음 정도 길이
                int m = melody[b];
                if (m >= 0) AddNote(d, b * beat, beat * 0.9f, scale[m], 0.13f, 0.4f);
            }
            return d;
        }

        static void AddNote(float[] d, float startSec, float lenSec, float freq, float vol, float decay)
        {
            int start = Mathf.RoundToInt(startSec * Rate);
            int len = Mathf.RoundToInt(lenSec * Rate);
            for (int i = 0; i < len; i++)
            {
                int idx = start + i;
                if (idx >= d.Length) break;
                float t = (float)i / len;
                float attack = Mathf.Clamp01(i / (Rate * 0.01f));
                float env = attack * Mathf.Exp(-t * (1f / Mathf.Max(0.05f, decay)));
                float x = 2f * Mathf.PI * freq * i / Rate;
                d[idx] += (Mathf.Sin(x) + 0.25f * Mathf.Sin(2f * x)) * vol * env;
            }
        }
    }
}
