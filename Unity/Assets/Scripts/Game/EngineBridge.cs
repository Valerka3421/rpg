// Мостики движкового слоя Unity: то, что MonoGame-версия берёт из
// World/Game.cs (AethelgardGame) и Audio/AudioSystem.cs, здесь живёт
// по-другому, но с теми же именами — GameController перенесён дословно
// и ссылается на эти типы.
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Aethelgard.Systems
{
    /// <summary>Статика окна: масштаб мыши (letterbox). GameApp обновляет
    /// каждый кадр из RectTransform/камеры.</summary>
    public static class AethelgardGame
    {
        public static float MouseScaleX = 1f;
        public static float MouseScaleY = 1f;
        public const int ScreenW = 1920;
        public const int ScreenH = 1080;
    }
}

namespace Aethelgard.Audio
{
    /// <summary>
    /// Процедурный звук — полный порт audio/AudioSystem.cs: те же
    /// GenerateX()-генераторы (синтез 22050 Гц mono, 16-бит PCM в byte[]),
    /// но воспроизведение через Unity: на первом Play(name) byte[] конвертируется
    /// в AudioClip (лениво, с кешем) и играется через пул из 6 AudioSource.
    /// </summary>
    public class AudioSystem : IDisposable
    {
        private const int VoiceCount = 6;
        private const int SampleRate = 22050;

        private readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        private readonly Dictionary<string, AudioClip?> _clips = new();
        private readonly System.Random _rng = new();
        private float _volume = 0.5f;
        private bool _enabled = true;

        public AudioSystem()
        {
            try
            {
                var root = new GameObject("AudioPool");
                root.hideFlags = HideFlags.HideInHierarchy;
                for (int i = 0; i < VoiceCount; i++)
                {
                    var src = root.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.volume = _volume;
                    _voices[i] = src;
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
                _volume = Mathf.Clamp(value, 0f, 1f);
                foreach (var v in _voices)
                    if (v != null) v.volume = _volume;
            }
        }

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>На desktop Start() запускал DynamicSoundEffectInstance;
        /// AudioSource в Unity играть заранее не нужно.</summary>
        public void Start() { }

        public void Play(string soundName)
        {
            if (!_enabled) return;
            var clip = GetOrCreateClip(soundName);
            if (clip == null) return;

            // Свободный голос (не isPlaying), иначе — самый ранний запущенный.
            AudioSource? best = null;
            foreach (var v in _voices)
            {
                if (v == null) continue;
                if (!v.isPlaying) { best = v; break; }
            }
            if (best == null) best = _voices[0];
            if (best == null) return;
            try
            {
                best.clip = clip;
                best.volume = _volume;
                best.Play();
            }
            catch { }
        }

        private AudioClip? GetOrCreateClip(string soundName)
        {
            if (_clips.TryGetValue(soundName, out var cached)) return cached;

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
            if (data == null)
            {
                _clips[soundName] = null;
                return null;
            }

            // byte[] (16-бит LE) → float[] −1..1 для AudioClip.SetData.
            int samples = data.Length / 2;
            var floats = new float[samples];
            for (int i = 0; i < samples; i++)
                floats[i] = BitConverter.ToInt16(data, i * 2) / 32768f;

            var clip = AudioClip.Create("sfx_" + soundName, samples, 1, SampleRate, false);
            clip.SetData(floats, 0);
            _clips[soundName] = clip;
            return clip;
        }

        // ── Sound generators (порты 1:1 из audio/AudioSystem.cs) ──

        /// <summary>Крит: два тона 300+200 Гц (audio.py:crit).</summary>
        private byte[] GenerateCrit()
        {
            float duration = 0.15f;
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = (1 - t) * 0.4f;
                float wave = (float)(Math.Sin(2 * Math.PI * 300 * i / SampleRate)
                    + Math.Sin(2 * Math.PI * 200 * i / SampleRate)) * 0.5f;
                short val = (short)(wave * env * short.MaxValue);
                BitConverter.GetBytes(val).CopyTo(data, i * 2);
            }
            return data;
        }

        /// <summary>Промах: короткий шум (audio.py:miss).</summary>
        private byte[] GenerateMiss()
        {
            float duration = 0.08f;
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = (float)(_rng.NextDouble() * 2 - 1);
                float env = (1 - t) * 0.15f;
                short val = (short)(noise * env * short.MaxValue);
                BitConverter.GetBytes(val).CopyTo(data, i * 2);
            }
            return data;
        }

        /// <summary>Смерть монстра: нисходящий тон 300→90 Гц.</summary>
        private byte[] GenerateDeathMonster()
        {
            float duration = 0.3f;
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = 300 - 210 * t;
                float env = (1 - t) * 0.35f;
                float wave = (float)Math.Sin(2 * Math.PI * freq * i / SampleRate);
                short val = (short)(wave * env * short.MaxValue);
                BitConverter.GetBytes(val).CopyTo(data, i * 2);
            }
            return data;
        }

        /// <summary>Смерть игрока: низкий тон 120 Гц, 0.8 с.</summary>
        private byte[] GenerateDeathPlayer()
        {
            float duration = 0.8f;
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = (1 - t) * 0.4f;
                float wave = (float)Math.Sin(2 * Math.PI * 120 * i / SampleRate);
                short val = (short)(wave * env * short.MaxValue);
                BitConverter.GetBytes(val).CopyTo(data, i * 2);
            }
            return data;
        }

        /// <summary>Огненный шар: шум + 400→40 Гц.</summary>
        private byte[] GenerateFireball()
        {
            float duration = 0.35f;
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = 400 - 360 * t;
                float noise = (float)(_rng.NextDouble() * 2 - 1) * 0.3f;
                float wave = (float)Math.Sin(2 * Math.PI * freq * i / SampleRate) + noise;
                float env = (1 - t) * 0.35f;
                short val = (short)(Math.Clamp(wave, -1f, 1f) * env * short.MaxValue);
                BitConverter.GetBytes(val).CopyTo(data, i * 2);
            }
            return data;
        }

        /// <summary>Лечение: восходящий тон 400→800 Гц.</summary>
        private byte[] GenerateHeal()
        {
            float duration = 0.25f;
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = 400 + 400 * t;
                float env = (1 - t) * 0.3f;
                float wave = (float)Math.Sin(2 * Math.PI * freq * i / SampleRate);
                short val = (short)(wave * env * short.MaxValue);
                BitConverter.GetBytes(val).CopyTo(data, i * 2);
            }
            return data;
        }

        private byte[] GenerateAttack()
        {
            // Short square wave burst — weapon swing
            float duration = 0.08f;
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = 200 - 100 * t;  // descending
                float wave = Math.Sign(Math.Sin(2 * Math.PI * freq * i / SampleRate));
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
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = (float)(_rng.NextDouble() * 2 - 1);
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
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = 300 + 600 * t;  // rising
                float wave = MathF.Sin(2 * MathF.PI * freq * i / SampleRate);
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
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = 400 - 200 * t;
                float wave = MathF.Sin(2 * MathF.PI * freq * i / SampleRate);
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
            int totalSamples = (int)(SampleRate * noteDur * notes.Length);
            var data = new byte[totalSamples * 2];
            int pos = 0;
            foreach (var freq in notes)
            {
                int noteSamples = (int)(SampleRate * noteDur);
                for (int i = 0; i < noteSamples; i++)
                {
                    float t = (float)i / noteSamples;
                    float wave = MathF.Sin(2 * MathF.PI * freq * i / SampleRate);
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
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = (float)(_rng.NextDouble() * 2 - 1);
                float sine = MathF.Sin(2 * MathF.PI * 80 * i / SampleRate);
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
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float sine = MathF.Sin(2 * MathF.PI * 60 * i / SampleRate);
                float noise = (float)(_rng.NextDouble() * 2 - 1) * 0.3f;
                float env = MathF.Sin(MathF.PI * t) * 0.4f;
                short val = (short)((sine + noise) * env * short.MaxValue * 0.7f);
                BitConverter.GetBytes(val).CopyTo(data, i * 2);
            }
            return data;
        }

        private byte[] GenerateBlip(float freq, float duration)
        {
            // Short sine blip — UI sound
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float wave = MathF.Sin(2 * MathF.PI * freq * i / SampleRate);
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
            int samples = (int)(SampleRate * duration);
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = 300 - 150 * t;
                float wave = Math.Sign(MathF.Sin(2 * MathF.PI * freq * i / SampleRate));
                float env = (1 - t) * 0.2f;
                short val = (short)(wave * env * short.MaxValue);
                BitConverter.GetBytes(val).CopyTo(data, i * 2);
            }
            return data;
        }

        public void Dispose()
        {
            _enabled = false;
            foreach (var v in _voices)
            {
                if (v != null) UObject.Destroy(v.gameObject);
            }
            for (int i = 0; i < _voices.Length; i++) _voices[i] = null!;
            _clips.Clear();
        }
    }
}

namespace Aethelgard.Render
{
    using Microsoft.Xna.Framework;

    /// <summary>Геометрия журнала чата в координатах дизайна 1920×1080 —
    /// GameController использует её для кликов по вкладкам. Настоящая
    /// uGUI-панель чата будет рисовать по этим же координатам.</summary>
    public static class ChatPanel
    {
        public static readonly string[] TabNames =
            { "Всё", "Бой", "Предметы", "Общение", "Опыт" };

        public static Rectangle TabRect(int i) => new(8 + i * 220, 1080 - 92, 210, 34);
    }
}
