using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CareerManager : MonoBehaviour
{
    public static CareerManager Instance { get; private set; }
    public static event Action<CareerManager> InstanceReady;

    [Header("Fresh Game Defaults")]
    [SerializeField] private CareerTier startingTier = CareerTier.Intern;
    [SerializeField] private SceneReference startingCurrentFloorScene;
    [SerializeField] private List<SceneReference> startingUnlockedFloorScenes = new();

    private CareerTier _currentTier;
    private string _currentFloorSceneName;
    private readonly HashSet<string> _unlockedFloors = new(StringComparer.Ordinal);

    public CareerTier CurrentTier => _currentTier;
    public string CurrentFloorSceneName => _currentFloorSceneName;
    public IReadOnlyCollection<string> UnlockedFloors => _unlockedFloors;

    public event Action<CareerTier> OnTierChanged;
    public event Action<string> OnFloorUnlocked;
    public event Action<string> OnCurrentFloorChanged;
    public event Action OnCareerStateChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResetToFreshStateInternal(raiseEvents: false);
        InstanceReady?.Invoke(this);
    }

    public bool HasReachedTier(CareerTier tier) => _currentTier >= tier;

    public bool IsFloorUnlocked(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        return _unlockedFloors.Contains(sceneName);
    }

    public bool UnlockFloor(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (_unlockedFloors.Add(sceneName))
        {
            OnFloorUnlocked?.Invoke(sceneName);
            RaiseChanged();
            return true;
        }

        return false;
    }

    public bool SetCurrentFloor(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        bool changed = !string.Equals(_currentFloorSceneName, sceneName, StringComparison.Ordinal);
        _currentFloorSceneName = sceneName;

        _unlockedFloors.Add(sceneName);

        if (changed)
            OnCurrentFloorChanged?.Invoke(sceneName);

        RaiseChanged();
        return changed;
    }

    public bool PromoteTo(CareerTier newTier)
    {
        if (newTier <= _currentTier)
            return false;

        _currentTier = newTier;
        OnTierChanged?.Invoke(_currentTier);
        RaiseChanged();
        return true;
    }

    public bool ApplyPromotion(CareerTier newTier, string unlockFloorSceneName, bool setUnlockedFloorAsCurrent)
    {
        bool changed = false;

        if (newTier > _currentTier)
        {
            _currentTier = newTier;
            OnTierChanged?.Invoke(_currentTier);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(unlockFloorSceneName))
        {
            if (_unlockedFloors.Add(unlockFloorSceneName))
            {
                OnFloorUnlocked?.Invoke(unlockFloorSceneName);
                changed = true;
            }

            if (setUnlockedFloorAsCurrent &&
                !string.Equals(_currentFloorSceneName, unlockFloorSceneName, StringComparison.Ordinal))
            {
                _currentFloorSceneName = unlockFloorSceneName;
                OnCurrentFloorChanged?.Invoke(_currentFloorSceneName);
                changed = true;
            }
        }

        if (changed)
            RaiseChanged();

        return changed;
    }

    public void ResetToFreshState()
    {
        ResetToFreshStateInternal(raiseEvents: true);
    }

    private void ResetToFreshStateInternal(bool raiseEvents)
    {
        _currentTier = startingTier;

        _currentFloorSceneName = startingCurrentFloorScene != null && startingCurrentFloorScene.IsValid
            ? startingCurrentFloorScene.SceneName
            : null;

        _unlockedFloors.Clear();

        if (startingUnlockedFloorScenes != null)
        {
            for (int i = 0; i < startingUnlockedFloorScenes.Count; i++)
            {
                var sceneRef = startingUnlockedFloorScenes[i];
                if (sceneRef != null && sceneRef.IsValid)
                    _unlockedFloors.Add(sceneRef.SceneName);
            }
        }

        if (!string.IsNullOrWhiteSpace(_currentFloorSceneName))
            _unlockedFloors.Add(_currentFloorSceneName);

        if (raiseEvents)
            RaiseChanged();
    }

    private void RaiseChanged()
    {
        OnCareerStateChanged?.Invoke();
    }

    // -----------------------
    // Save / Load
    // -----------------------

    [Serializable]
    public class Snapshot
    {
        public int currentTier;
        public string currentFloorSceneName;
        public List<string> unlockedFloors = new();
    }

    public Snapshot CaptureSnapshot()
    {
        var snap = new Snapshot
        {
            currentTier = (int)_currentTier,
            currentFloorSceneName = _currentFloorSceneName,
            unlockedFloors = new List<string>(_unlockedFloors)
        };

        return snap;
    }

    public void RestoreSnapshot(Snapshot snap)
    {
        _unlockedFloors.Clear();

        if (snap == null)
        {
            ResetToFreshStateInternal(raiseEvents: true);
            return;
        }

        _currentTier = Enum.IsDefined(typeof(CareerTier), snap.currentTier)
            ? (CareerTier)snap.currentTier
            : startingTier;

        string defaultFloor = startingCurrentFloorScene != null && startingCurrentFloorScene.IsValid
            ? startingCurrentFloorScene.SceneName
            : null;

        _currentFloorSceneName = string.IsNullOrWhiteSpace(snap.currentFloorSceneName)
            ? defaultFloor
            : snap.currentFloorSceneName;

        if (snap.unlockedFloors != null)
        {
            for (int i = 0; i < snap.unlockedFloors.Count; i++)
            {
                var sceneName = snap.unlockedFloors[i];
                if (!string.IsNullOrWhiteSpace(sceneName))
                    _unlockedFloors.Add(sceneName);
            }
        }

        if (!string.IsNullOrWhiteSpace(_currentFloorSceneName))
            _unlockedFloors.Add(_currentFloorSceneName);

        RaiseChanged();
    }
}