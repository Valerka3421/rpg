using System;

    namespace Aethelgard.Audio
    {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.BaseClass;
    using Aethelgard.Entities;
    using Aethelgard.Entities.Monsters;
    using Aethelgard.Structs;
    using Aethelgard.World;
    using Aethelgard.Systems;
    using Aethelgard.Data;
    using Aethelgard.Net;
    using Aethelgard.Audio;
    /// <summary>
    /// Базовый класс аудио (без UnityEngine — живёт в Core). Реализация
    /// UnityAudioSystem в Game-слои переопределяет Play(). Паритет API
    /// MonoGame AudioSystem: Play(name), Volume, Enabled, Start(), Dispose().
    /// </summary>
    public class AudioSystemCore : IDisposable
    {
        public virtual float Volume { get => _volume; set => _volume = Math.Clamp(value, 0f, 1f); }
        private float _volume = 0.5f;

        public virtual bool Enabled { get => _enabled; set => _enabled = value; }
        private bool _enabled = true;

        public virtual void Start() { }

        /// <summary>Проиграть звук по имени (attack/hit/pickup/stairs/...).</summary>
        public virtual void Play(string soundName) { }

        public virtual void Dispose() { }
    }
}
