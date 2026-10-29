using Bunker26.Party.Persistence;
using UnityEngine;

namespace Bunker26.Party
{
    /// <summary>
    /// Owns the active Party session and its services across scene loads.
    /// Scene-specific controllers should consume this runtime instead of creating services again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyRuntime : MonoBehaviour
    {
        private static PartyRuntime _instance;

        private PlayerProfileService _profiles;
        private PartyService _party;
        private InputRebindingService _inputRebinding;

        public static PartyRuntime Current
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<PartyRuntime>();
                return _instance;
            }
        }

        public PlayerProfileService Profiles { get { return _profiles; } }
        public PartyService Party { get { return _party; } }
        public InputRebindingService InputRebinding { get { return _inputRebinding; } }

        public static PartyRuntime GetOrCreate(int maxLocalPlayers = 8, bool createLocalPlayerOne = true)
        {
            PartyRuntime runtime = Current;
            if (runtime == null)
            {
                GameObject runtimeObject = new GameObject("Bunker26PartyRuntime");
                runtime = runtimeObject.AddComponent<PartyRuntime>();
            }

            runtime.InitializeServices(maxLocalPlayers, createLocalPlayerOne);
            return runtime;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void InitializeServices(int maxLocalPlayers, bool createLocalPlayerOne)
        {
            if (_party == null)
            {
                _profiles = new PlayerProfileService(new JsonProfileRepository());
                _party = new PartyService(
                    _profiles,
                    new InputAssignmentService(),
                    new JsonPlayerPreferencesRepository(),
                    maxLocalPlayers);
                _inputRebinding = new InputRebindingService(new JsonInputBindingOverridesRepository());
            }

            if (createLocalPlayerOne && _party.Participants.Count == 0)
                _party.InitializeLocalParty();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
