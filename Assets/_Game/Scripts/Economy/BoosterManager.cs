using System.Collections.Generic;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Economy
{
    // Booster state (active/cooldown timers) is intentionally NOT persisted via ISaveable —
    // an active buff or running cooldown is lost on app restart. Acceptable for MVP; every
    // booster starts "ready" on load.
    public class BoosterManager : MonoBehaviour
    {
        [SerializeField]
        private BoosterData[] _boosters;

        private class BoosterState
        {
            public float ActiveRemaining;
            public float CooldownRemaining;

            // UI renders whole seconds only; ticking at 10 Hz would spam ~10x more events
            // (and TMP re-layouts) than the label can visibly change.
            public int LastPublishedSeconds = -1;
        }

        private readonly Dictionary<string, BoosterState> _states = new Dictionary<string, BoosterState>();

        private double _lastTapMultiplier = 1d;
        private double _lastPassiveMultiplier = 1d;

        private void Awake()
        {
            if (_boosters == null)
            {
                return;
            }

            foreach (var booster in _boosters)
            {
                if (booster == null || string.IsNullOrEmpty(booster.Id) || _states.ContainsKey(booster.Id))
                {
                    continue;
                }

                _states[booster.Id] = new BoosterState();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TickEvent>(HandleTick);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TickEvent>(HandleTick);
        }

        private void Start()
        {
            // Published in Start so HUD (subscribed in OnEnable) is ready to receive it.
            EventBus.Publish(new BoosterMultipliersChangedEvent(_lastTapMultiplier, _lastPassiveMultiplier));

            if (_boosters == null)
            {
                return;
            }

            foreach (var booster in _boosters)
            {
                if (booster == null || string.IsNullOrEmpty(booster.Id))
                {
                    continue;
                }

                EventBus.Publish(new BoosterStateChangedEvent(booster.Id, false, 0f, 0f));
            }
        }

        public BoosterData GetBooster(string id)
        {
            if (_boosters == null)
            {
                return null;
            }

            foreach (var booster in _boosters)
            {
                if (booster != null && booster.Id == id)
                {
                    return booster;
                }
            }

            return null;
        }

        public bool IsActive(string id)
        {
            return _states.TryGetValue(id, out BoosterState state) && state.ActiveRemaining > 0f;
        }

        public bool IsReady(string id)
        {
            return _states.TryGetValue(id, out BoosterState state) && state.ActiveRemaining <= 0f && state.CooldownRemaining <= 0f;
        }

        public bool TryActivate(string id)
        {
            BoosterData booster = GetBooster(id);

            if (booster == null || !IsReady(id) || !_states.TryGetValue(id, out BoosterState state))
            {
                return false;
            }

            state.ActiveRemaining = booster.DurationSeconds;
            state.LastPublishedSeconds = Mathf.CeilToInt(state.ActiveRemaining);
            EventBus.Publish(new BoosterStateChangedEvent(id, true, state.ActiveRemaining, state.CooldownRemaining));
            RecomputeMultipliers();
            return true;
        }

        private void HandleTick(TickEvent e)
        {
            if (_boosters == null)
            {
                return;
            }

            foreach (var booster in _boosters)
            {
                if (booster == null || string.IsNullOrEmpty(booster.Id) || !_states.TryGetValue(booster.Id, out BoosterState state))
                {
                    continue;
                }

                bool changed = false;

                if (state.ActiveRemaining > 0f)
                {
                    state.ActiveRemaining -= e.DeltaTime;
                    changed = true;

                    if (state.ActiveRemaining <= 0f)
                    {
                        state.ActiveRemaining = 0f;
                        state.CooldownRemaining = booster.CooldownSeconds;
                        RecomputeMultipliers();
                    }
                }
                else if (state.CooldownRemaining > 0f)
                {
                    state.CooldownRemaining = Mathf.Max(0f, state.CooldownRemaining - e.DeltaTime);
                    changed = true;
                }

                if (changed)
                {
                    int displaySeconds = Mathf.CeilToInt(state.ActiveRemaining > 0f ? state.ActiveRemaining : state.CooldownRemaining);

                    if (displaySeconds != state.LastPublishedSeconds)
                    {
                        state.LastPublishedSeconds = displaySeconds;
                        EventBus.Publish(new BoosterStateChangedEvent(booster.Id, state.ActiveRemaining > 0f, state.ActiveRemaining, state.CooldownRemaining));
                    }
                }
            }
        }

        private void RecomputeMultipliers()
        {
            double tapMultiplier = 1d;
            double passiveMultiplier = 1d;

            if (_boosters != null)
            {
                foreach (var booster in _boosters)
                {
                    if (booster == null || !IsActive(booster.Id))
                    {
                        continue;
                    }

                    if (booster.Target == BoosterTarget.TapIncome)
                    {
                        tapMultiplier *= booster.Multiplier;
                    }
                    else if (booster.Target == BoosterTarget.PassiveIncome)
                    {
                        passiveMultiplier *= booster.Multiplier;
                    }
                }
            }

            if (tapMultiplier == _lastTapMultiplier && passiveMultiplier == _lastPassiveMultiplier)
            {
                return;
            }

            _lastTapMultiplier = tapMultiplier;
            _lastPassiveMultiplier = passiveMultiplier;
            EventBus.Publish(new BoosterMultipliersChangedEvent(tapMultiplier, passiveMultiplier));
        }
    }
}
