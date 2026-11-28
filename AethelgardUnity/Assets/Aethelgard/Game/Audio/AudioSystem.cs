using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethelgard.Audio;

/// <summary>
/// Процедурный синтез звуков — порт MonoGame AudioSystem (оригинал из
/// Python audio/audio.py). Вместо DynamicSoundEffectInstance — AudioClip,
/// создаваемые из float[] и кэшируемые; воспроизведение через пул
/// AudioSource (до 4 голосов, чтобы звуки не обрывали друг друга).
/// Внешние .wav не нужны — все звуки генерируются кодом.
/// </summary>
public class AudioSystem : AudioSystemCore
{
    private const int SampleRate = 22050;
    private readonly Dictionary<string, AudioClip> _cache = new();
    private readonly List<AudioSource> _voices = new();
    private readonly GameObject _root;
    private float _volume = 0.5f;
    private bool _enabled = true;

    public AudioSystem()
    {
        _root = new GameObject("AethelgardAudio");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        for (int i = 0; i < 4; i++)
        {
            var src = _root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            _voices.Add(src);
        }
    }

    public override float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            foreach (var v in _voices) v.volume = _volume;
        }
    }

    public override bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value) foreach (var v in _voices) v.Stop();
        }
    }

    /// <summary>Совместимость с MonoGame-версией (там запускал поток буферов).</summary>
    public override void Start() { }

    /// <summary>Проиграть звук по имени (parity: список звуков audio.py).</summary>
    public override void Play(string soundName)
    {
        if (!_enabled) return;
        var clip = GetOrCreate(soundName);
        if (clip == null) return;
        // Свободный голос: первый, который закончил играть
        foreach (var v in _voices)
        {
            if (!v.isPlaying)
            {
                v.clip = clip;
                v.volume = _volume;
                v.Play();
                return;
            }
        }
        // Все заняты — прерываем самый старый (голос 0)
        _voices[0].clip = clip;
        _voices[0].volume = _volume;
        _voices[0].Play();
    }

    private AudioClip? GetOrCreate(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;
        float[]? samples = name switch
        {
            "attack" => Sweep(0.08f, 200, 100, 0.3f, square: true),
            "hit" => Noise(0.12f, 0.4f),
            "pickup" => Sweep(0.15f, 300, 900, 0.3f, siness: true),
            "stairs" => Sweep(0.2f, 400, 200, 0.25f),
            "level_up" => Arpeggio(new[] { 262f, 330f, 392f, 523f }, 0.08f),
            "death" => Rumble(),
            "boss" => Boss(),
            "chat" => Blip(800, 0.05f),
            "menu_move" => Blip(600, 0.03f),
            "error" => Buzz(),
            "unlock" => Sweep(0.15f, 300, 900, 0.3f),
            "chest" => Sweep(0.15f, 300, 900, 0.3f),
            "crit" => TwoTone(300, 200, 0.15f, 0.4f),
            "miss" => Noise(0.08f, 0.15f),
            "death_monster" => Sweep(0.3f, 300, 90, 0.35f),
            "death_player" => Tone(120, 0.8f, 0.4f),
            "fireball" => Fireball(),
            "heal" => Sweep(0.25f, 400, 800, 0.3f),
            "menu_select" => Blip(600, 0.05f),
            "menu_open" => Blip(400, 0.05f),
            _ => null,
        };
        if (samples == null) return null;
        var clip = AudioClip.Create("sfx_" + name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        _cache[name] = clip;
        return clip;
    }

    // ── Генераторы (паритет MonoGame-версии) ──

    private static float[] Tone(float freq, float dur, float amp,
        Func<int, int, float>? wave = null)
    {
        int n = (int)(SampleRate * dur);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float w = wave != null
                ? wave(i, SampleRate)
                : Mathf.Sin(2 * Mathf.PI * freq * i / SampleRate);
            d[i] = w * (1 - t) * amp;
        }
        return d;
    }

    private static float[] Sweep(float dur, float f0, float f1, float amp,
        bool square = false, bool siness = false)
    {
        int n = (int)(SampleRate * dur);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float freq = f0 + (f1 - f0) * t;
            float ph = 2 * Mathf.PI * freq * i / SampleRate;
            float w = square ? Mathf.Sign(Mathf.Sin(ph))
                : Mathf.Sin(ph);
            float env = siness ? Mathf.Sin(Mathf.PI * t) * amp : (1 - t) * amp;
            d[i] = w * env;
        }
        return d;
    }

    private static float[] Noise(float dur, float amp)
    {
        int n = (int)(SampleRate * dur);
        var d = new float[n];
        var rng = new System.Random();
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float env = (1 - t) * (1 - t) * amp;
            d[i] = ((float)rng.NextDouble() * 2 - 1) * env;
        }
        return d;
    }

    private static float[] Blip(float freq, float dur) =>
        Sweep(dur, freq, freq, 0.2f, siness: true);

    private static float[] Arpeggio(float[] notes, float noteDur)
    {
        int per = (int)(SampleRate * noteDur);
        var d = new float[per * notes.Length];
        for (int k = 0; k < notes.Length; k++)
        {
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / per;
                d[k * per + i] = Mathf.Sin(2 * Mathf.PI * notes[k] * i / SampleRate)
                    * Mathf.Sin(Mathf.PI * t) * 0.3f;
            }
        }
        return d;
    }

    private static float[] TwoTone(float f1, float f2, float dur, float amp)
    {
        int n = (int)(SampleRate * dur);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float w = (Mathf.Sin(2 * Mathf.PI * f1 * i / SampleRate)
                + Mathf.Sin(2 * Mathf.PI * f2 * i / SampleRate)) * 0.5f;
            d[i] = w * (1 - t) * amp;
        }
        return d;
    }

    private static float[] Rumble()
    {
        int n = (int)(SampleRate * 0.4f);
        var d = new float[n];
        var rng = new System.Random();
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float noise = (float)rng.NextDouble() * 2 - 1;
            float sine = Mathf.Sin(2 * Mathf.PI * 80 * i / SampleRate);
            d[i] = (noise + sine * 0.5f) * (1 - t) * 0.175f;
        }
        return d;
    }

    private static float[] Boss()
    {
        int n = (int)(SampleRate * 0.5f);
        var d = new float[n];
        var rng = new System.Random();
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float sine = Mathf.Sin(2 * Mathf.PI * 60 * i / SampleRate);
            float noise = (float)rng.NextDouble() * 2 - 1;
            d[i] = (sine + noise * 0.3f) * Mathf.Sin(Mathf.PI * t) * 0.28f;
        }
        return d;
    }

    private static float[] Buzz()
    {
        int n = (int)(SampleRate * 0.15f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float freq = 300 - 150 * t;
            float w = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * freq * i / SampleRate));
            d[i] = w * (1 - t) * 0.2f;
        }
        return d;
    }

    private static float[] Fireball()
    {
        int n = (int)(SampleRate * 0.35f);
        var d = new float[n];
        var rng = new System.Random();
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float freq = 400 - 360 * t;
            float wave = Mathf.Sin(2 * Mathf.PI * freq * i / SampleRate)
                + ((float)rng.NextDouble() * 2 - 1) * 0.3f;
            d[i] = Mathf.Clamp(wave, -1f, 1f) * (1 - t) * 0.35f;
        }
        return d;
    }

    public override void Dispose()
    {
        foreach (var v in _voices) v.Stop();
        UnityEngine.Object.Destroy(_root);
    }
}
