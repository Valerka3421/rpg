using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace Aethelgard.Audio;

/// <summary>
/// Procedural sound synthesis — generates sound effects on the fly.
/// Ported from Python audio/audio.py.
///
/// Uses DynamicSoundEffectInstance to play synthesized waveforms.
/// No external .wav files needed — all sounds are generated from sine/square/noise.
///
/// Sounds:
/// - attack: short square wave burst
/// - hit: noise burst with decay
/// - pickup: rising sine sweep
/// - stairs: descending sine
/// - level_up: ascending arpeggio
/// - death: low noise rumble
/// - boss: low sine + noise
/// - chat: short blip
/// - menu_move: short click
/// - error: descending buzz
/// </summary>
public class AudioSystem : IDisposable
{
    // Пул голосов: раньше был один DynamicSoundEffectInstance с очередью
    // буферов — звуки игрались строго по очереди (короткий «клик» откладывал
    // удар). Пул позволяет одновременные звуки.
    private const int VoiceCount = 6;
    private readonly List<DynamicSoundEffectInstance> _voices = new();
    private readonly object _lock = new();
    private float _volume = 0.5f;
    private bool _enabled = true;
    private int _sampleRate = 22050;

    public AudioSystem()
    {
        try
        {
            for (int i = 0; i < VoiceCount; i++)
            {
                var v = new DynamicSoundEffectInstance(_sampleRate, AudioChannels.Mono);
                v.Volume = _volume;
                v.IsLooped = false;
                _voices.Add(v);
            }
        }
        catch
        {
            _enabled = false;
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            lock (_lock)
                foreach (var v in _voices)
                    v.Volume = _volume;
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
        }
    }

    /// <summary>
    /// Start playback (must be called after creation).
    /// </summary>
    public void Start()
    {
        if (!_enabled) return;
        lock (_lock)
        {
            foreach (var v in _voices)
            {
                try { if (v.State != SoundState.Playing) v.Play(); }
                catch { _enabled = false; return; }
            }
        }
    }

    /// <summary>
    /// Play a sound by name.
    /// </summary>
    public void Play(string soundName)
    {
        if (!_enabled) return;

        byte[]? data = soundName switch
        {
            "attack" => GenerateAttack(),
            "hit" => GenerateHit(),
            "pickup" => GeneratePickup(),
            "stairs" => GenerateStairs(),
            "level_up" => GenerateLevelUp(),
            "death" => GenerateDeath(),
            "boss" => GenerateBoss(),
            "chat" => GenerateBlip(800, 0.05f),
            "menu_move" => GenerateBlip(600, 0.03f),
            "error" => GenerateError(),
            "unlock" => GeneratePickup(),
            "chest" => GeneratePickup(),
            // Недостающие звуки (audio/audio.py: 12 звуков, паритет):
            "crit" => GenerateCrit(),
            "miss" => GenerateMiss(),
            "death_monster" => GenerateDeathMonster(),
            "death_player" => GenerateDeathPlayer(),
            "fireball" => GenerateFireball(),
            "heal" => GenerateHeal(),
            "menu_select" => GenerateBlip(600, 0.05f),
            "menu_open" => GenerateBlip(400, 0.05f),
            _ => null,
        };

        if (data == null) return;

        // SubmitBuffer под общим _lock: он может вызываться из любого потока
        // (хост дёргает звук из серверного UpdateLoop) — параллельные
        // SubmitBuffer на одном инстансе не потокобезопасны. Голос берём
        // с наименьшим числом ожидающих буферов (иначе обрываем самый
        // «длинный» — пусть новые звуки не копятся в очереди).
        lock (_lock)
        {
            DynamicSoundEffectInstance? best = null;
            int bestPending = int.MaxValue;
            foreach (var v in _voices)
            {
                int pending = v.PendingBufferCount;
                if (pending < bestPending)
                {
                    bestPending = pending;
                    best = v;
                }
            }
            if (best == null) return;
            try
            {
                if (bestPending > 2)
                {
                    // Все голоса заняты — сбрасываем и перезапускаем,
                    // чтобы новый звук не ждал конца старого.
                    best.Stop();
                    best.Play();
                }
                best.SubmitBuffer(data, 0, data.Length);
                if (best.State != SoundState.Playing) best.Play();
            }
            catch { }
        }
    }

    // ── Sound generators ──

    /// <summary>Крит: два тона 300+200 Гц (audio.py:crit).</summary>
    private byte[] GenerateCrit()
    {
        float duration = 0.15f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float env = (1 - t) * 0.4f;
            float wave = (float)(Math.Sin(2 * Math.PI * 300 * i / _sampleRate)
                + Math.Sin(2 * Math.PI * 200 * i / _sampleRate)) * 0.5f;
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    /// <summary>Промах: короткий шум (audio.py:miss).</summary>
    private byte[] GenerateMiss()
    {
        float duration = 0.08f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        var rng = new Random();
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float env = (1 - t) * 0.15f;
            short val = (short)(noise * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    /// <summary>Смерть монстра: нисходящий тон 300→90 Гц (audio.py:death_monster).</summary>
    private byte[] GenerateDeathMonster()
    {
        float duration = 0.3f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = 300 - 210 * t;
            float env = (1 - t) * 0.35f;
            float wave = (float)Math.Sin(2 * Math.PI * freq * i / _sampleRate);
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    /// <summary>Смерть игрока: низкий тон 120 Гц, 0.8 с (audio.py:death_player).</summary>
    private byte[] GenerateDeathPlayer()
    {
        float duration = 0.8f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float env = (1 - t) * 0.4f;
            float wave = (float)Math.Sin(2 * Math.PI * 120 * i / _sampleRate);
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    /// <summary>Огненный шар: шум + 400→40 Гц (audio.py:fireball).</summary>
    private byte[] GenerateFireball()
    {
        float duration = 0.35f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        var rng = new Random();
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = 400 - 360 * t;
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.3f;
            float wave = (float)Math.Sin(2 * Math.PI * freq * i / _sampleRate) + noise;
            float env = (1 - t) * 0.35f;
            short val = (short)(Math.Clamp(wave, -1f, 1f) * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    /// <summary>Лечение: восходящий тон 400→800 Гц (audio.py:heal).</summary>
    private byte[] GenerateHeal()
    {
        float duration = 0.25f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = 400 + 400 * t;
            float env = (1 - t) * 0.3f;
            float wave = (float)Math.Sin(2 * Math.PI * freq * i / _sampleRate);
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    private byte[] GenerateAttack()
    {
        // Short square wave burst — weapon swing
        float duration = 0.08f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = 200 - 100 * t;  // descending
            float wave = Math.Sign(Math.Sin(2 * Math.PI * freq * i / _sampleRate));
            float env = (1 - t) * 0.3f;  // decay
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    private byte[] GenerateHit()
    {
        // Noise burst with decay — impact
        float duration = 0.12f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        var rng = new Random();
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float env = (1 - t) * (1 - t) * 0.4f;
            short val = (short)(noise * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    private byte[] GeneratePickup()
    {
        // Rising sine sweep — item pickup
        float duration = 0.15f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = 300 + 600 * t;  // rising
            float wave = MathF.Sin(2 * MathF.PI * freq * i / _sampleRate);
            float env = MathF.Sin(MathF.PI * t) * 0.3f;
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    private byte[] GenerateStairs()
    {
        // Descending sine — going down stairs
        float duration = 0.2f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = 400 - 200 * t;
            float wave = MathF.Sin(2 * MathF.PI * freq * i / _sampleRate);
            float env = (1 - t) * 0.25f;
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    private byte[] GenerateLevelUp()
    {
        // Ascending arpeggio — level up fanfare
        float[] notes = { 262, 330, 392, 523 };  // C E G C
        float noteDur = 0.08f;
        int totalSamples = (int)(_sampleRate * noteDur * notes.Length);
        var data = new byte[totalSamples * 2];
        int pos = 0;
        foreach (var freq in notes)
        {
            int noteSamples = (int)(_sampleRate * noteDur);
            for (int i = 0; i < noteSamples; i++)
            {
                float t = (float)i / noteSamples;
                float wave = MathF.Sin(2 * MathF.PI * freq * i / _sampleRate);
                float env = MathF.Sin(MathF.PI * t) * 0.3f;
                short val = (short)(wave * env * short.MaxValue);
                if (pos + 1 < data.Length)
                {
                    data[pos] = (byte)(val & 0xFF);
                    data[pos + 1] = (byte)(val >> 8);
                }
                pos += 2;
            }
        }
        return data;
    }

    private byte[] GenerateDeath()
    {
        // Low noise rumble — death
        float duration = 0.4f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        var rng = new Random();
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float sine = MathF.Sin(2 * MathF.PI * 80 * i / _sampleRate);
            float env = (1 - t) * 0.35f;
            short val = (short)((noise + sine * 0.5f) * env * short.MaxValue * 0.5f);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    private byte[] GenerateBoss()
    {
        // Low sine + noise — boss appearance
        float duration = 0.5f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        var rng = new Random();
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float sine = MathF.Sin(2 * MathF.PI * 60 * i / _sampleRate);
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.3f;
            float env = MathF.Sin(MathF.PI * t) * 0.4f;
            short val = (short)((sine + noise) * env * short.MaxValue * 0.7f);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    private byte[] GenerateBlip(float freq, float duration)
    {
        // Short sine blip — UI sound
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float wave = MathF.Sin(2 * MathF.PI * freq * i / _sampleRate);
            float env = MathF.Sin(MathF.PI * t) * 0.2f;
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    private byte[] GenerateError()
    {
        // Descending buzz — error
        float duration = 0.15f;
        int samples = (int)(_sampleRate * duration);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = 300 - 150 * t;
            float wave = Math.Sign(MathF.Sin(2 * MathF.PI * freq * i / _sampleRate));
            float env = (1 - t) * 0.2f;
            short val = (short)(wave * env * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(data, i * 2);
        }
        return data;
    }

    public void Dispose()
    {
        // Под _lock: буферы могут отправляться из любого потока
        lock (_lock)
        {
            _enabled = false;
            foreach (var v in _voices)
            {
                try { v.Dispose(); } catch { }
            }
            _voices.Clear();
        }
    }
}
