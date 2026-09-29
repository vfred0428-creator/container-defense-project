using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed class GameSession : MonoBehaviour
    {
        private const float Step = 1f / 30;
        public MatchSimulation Match { get; private set; }
        public bool Started { get; private set; }
        public bool Paused { get; private set; }
        public int SpectatedPlayer { get; private set; }
        public string Notice { get; private set; }
        public ArenaView Arena { get; private set; }
        public AccountProgression Account { get; private set; }
        public CharacterCatalog Characters { get; private set; }
        public CollectionCatalog Collections { get; private set; }
        public InventorySystem Inventory { get { return Account.Inventory; } }
        public ProgressionRules Progression { get; private set; }
        public MatchReward LastReward { get; private set; }
        public string SaveStatus { get; private set; }
        public string AccountPath { get; private set; }
        public bool SaveDirty { get; private set; }
        private ISaveService saves;
        private long matchSequence;
        private LocalBotController bots;
        private IPlayerInput input;
        private MatchConfig config;
        private float accumulated, noticeUntil;
        private int matchNumber;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            config = Resources.Load<MatchConfig>("DefaultMatch");
            InitializeAccount();
            input = new DesktopPlayerInput();
            Arena = gameObject.AddComponent<ArenaView>();
            Arena.Build(Collections);
            gameObject.AddComponent<MatchHud>().Initialize(this);
            ResetMatch();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--smoke-test") >= 0)
            {
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--collection-smoke-test") >= 0)
                    gameObject.AddComponent<CollectionSmokeDriver>().Initialize(this);
                else gameObject.AddComponent<MatchSmokeDriver>().Initialize(this);
            }
#endif
        }

        private void ResetMatch()
        {
            if (Match != null) Match.Changed -= OnMatchEvent;
            var roster = new CharacterDefinition[6];
            roster[0] = Characters.Get(Account.Selected);
            for (int i = 1; i < 6; i++) roster[i] = Characters.Get((CharacterId)(((int)Account.Selected + i) % 7));
            Match = new MatchSimulation(config != null ? config.Rules : new MatchRules(), 731 + matchNumber++, roster);
            Match.Changed += OnMatchEvent;
            bots = new LocalBotController(Match);
            accumulated = 0; SpectatedPlayer = 0; Paused = false;
            Arena.Bind(Match,Collections.Skin(Inventory.Equipped(Account.Selected))); Notify("Run to a free door. Press E to claim it.");
        }

        public void Play()
        {
            ResetMatch(); matchSequence = Account.BeginMatch(Match); PersistAccount(); LastReward = null;
            Started = true;
        }
        public void ReturnToTitle() { Started = false; ResetMatch(); }
        public void TogglePause() { if (Started && !Match.Finished) { Paused = !Paused; accumulated = 0; } }
        public void Quit() { Application.Quit(); }

        private void InitializeAccount()
        {
            var content = Resources.Load<CharacterConfig>("Characters");
            Characters = new CharacterCatalog(content != null ? content.Characters : CharacterCatalog.Defaults());
            Progression = (content != null ? content.Progression : new ProgressionRules()).Snapshot();
            var collectionContent = Resources.Load<CollectionConfig>("Collections");
            Collections = collectionContent != null ? collectionContent.Catalog() : CollectionCatalog.CreateDefault();
            AccountPath = System.IO.Path.Combine(Application.persistentDataPath,"account-v1.json");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args,"--smoke-test") >= 0)
            {
                int outputIndex = System.Array.IndexOf(args,"--smoke-output");
                string output = outputIndex >= 0 && outputIndex + 1 < args.Length ? args[outputIndex + 1] :
                    System.IO.Path.Combine(Application.temporaryCachePath,"ContainerDefense-Smoke");
                AccountPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(output,"account-v1.json"));
                if (System.Array.IndexOf(args,"--smoke-seed-account") >= 0 && !System.IO.File.Exists(AccountPath))
                    new LocalSaveService(AccountPath).Save(new AccountData { TotalXp = Progression.XpForLevel(3) - 30 });
            }
#endif
            saves = new LocalSaveService(AccountPath);
            Account = new AccountProgression(saves.Load(),Characters,Progression,Collections);
            SaveStatus = string.IsNullOrEmpty(saves.Status) ? "Account saves automatically on this device" : saves.Status;
        }
        public bool SelectCharacter(CharacterId id)
        {
            if (Started || !Account.Select(id)) return false;
            PersistAccount(); ResetMatch(); return true;
        }
        public bool EquipSkin(CharacterId character, string skinId)
        {
            if (Started || !Account.IsUnlocked(character) || !Inventory.TryEquip(character,skinId)) return false;
            PersistAccount(); ResetMatch(); return true;
        }
        public bool ClaimStarterCollection()
        {
            if (Started || !Inventory.ClaimStarter()) return false;
            PersistAccount(); return true;
        }
        public void PersistAccount()
        {
            SaveDirty = true;
            try
            {
                saves.Save(Account.Snapshot()); SaveDirty = false; SaveStatus = saves.Status;
            }
            catch (System.Exception e) when (e is System.IO.IOException || e is System.UnauthorizedAccessException)
            { SaveStatus = "Progress is in memory only. " + (saves.CanWrite ? "Could not write account; retry saving." : saves.Status); }
        }

        private void Update()
        {
            if (input.PausePressed) TogglePause();
            if (!Started || Paused || Match.Finished) return;
            if (input.InteractPressed) Interact();
            if (input.SpectatePressed) CycleSpectator();
            if (input.UpgradePressed >= 0) Buy((UpgradeKind)input.UpgradePressed);
            Vector2 move = input.Movement;
            accumulated += Mathf.Min(Time.deltaTime, 0.25f);
            while (accumulated >= Step)
            {
                Match.Move(0, move.x, move.y, Step);
                bots.Tick(Step); Match.Tick(Step); accumulated -= Step;
            }
            if (Match.Players[0].Eliminated && Match.Players[SpectatedPlayer].Eliminated) CycleSpectator();
        }

        public int NearbyHouse()
        {
            PlayerState p = Match.Players[0]; float best = 1.65f; int result = -1;
            // Use the same configured radius as command validation.
            if (config != null) best = config.Rules.ClaimRadius;
            foreach (HouseState h in Match.Houses)
            {
                float distance = p.Position.Distance(h.Entry);
                if (distance <= best) { best = distance; result = h.Id; }
            }
            return result;
        }

        public void Interact()
        {
            if (!Started || Paused || Match.Finished || Match.Players[0].Eliminated) return;
            PlayerState p = Match.Players[0];
            if (p.HouseId >= 0)
            {
                if (!Match.TryToggleSleep(0)) Notify("Return to your door to get into bed.");
                return;
            }
            int house = NearbyHouse();
            if (house < 0) { Notify("Move closer to a container door."); return; }
            if (!Match.TryClaim(0, house)) Notify("Already claimed. Find another free house.");
        }

        public void Buy(UpgradeKind kind)
        {
            if (!Started || Paused || Match.Finished) return;
            int home = Match.Players[0].HouseId;
            if (home < 0 || Match.Players[0].Eliminated) return;
            if (!Match.TryUpgrade(0, home, kind)) Notify(Match.Houses[home].IsBuilding ? "An upgrade is already building." : "Not enough gold, or this upgrade is at its maximum.");
        }

        public void CycleSpectator()
        {
            if (!Match.Players[0].Eliminated) return;
            for (int i = 1; i <= 6; i++)
            {
                int id = (SpectatedPlayer + i) % 6;
                if (!Match.Players[id].Eliminated) { SpectatedPlayer = id; return; }
            }
        }

        public void Notify(string text) { Notice = text; noticeUntil = Time.unscaledTime + 4; }
        public string CurrentNotice { get { return Time.unscaledTime < noticeUntil ? Notice : ""; } }
        private void OnMatchEvent(MatchEvent e)
        {
            Arena.Handle(e);
            if (e.Kind == MatchEventKind.Claimed && e.PlayerId == 0) Notify("House secured! Press E again to sleep and earn gold.");
            if (e.Kind == MatchEventKind.Sleeping && e.PlayerId == 0)
                Notify(Match.Players[0].Sleeping ? "Earning gold. Choose bed, door or weapon upgrades below." : "Awake. Income paused.");
            if (e.Kind == MatchEventKind.UpgradeStarted && e.PlayerId == 0)
                Notify(Match.Houses[e.HouseId].LastUpgradeDiscounted ? "Mochi's lucky discount! Paid " + e.Amount.ToString("0.#") + " gold." : "Building your " + Match.Houses[e.HouseId].BuildingKind.ToString().ToLowerInvariant() + " upgrade...");
            if (e.Kind == MatchEventKind.CombatStarted && !Match.Players[0].Eliminated) Notify("The storm is here. Protect your own door!");
            if (e.Kind == MatchEventKind.Eliminated && e.PlayerId == 0) Notify("Eliminated. Press Tab to spectate the remaining residents.");
            if (e.Kind == MatchEventKind.Finished)
            {
                MatchReward reward;
                if (Account.TryAward(Match,matchSequence,out reward)) { LastReward = reward; PersistAccount(); }
            }
        }
        private void OnApplicationPause(bool paused) { if (paused && SaveDirty && Account != null) PersistAccount(); }
        private void OnApplicationQuit() { if (SaveDirty && Account != null) PersistAccount(); }
        private void OnDestroy() { if (Match != null) Match.Changed -= OnMatchEvent; }
    }
}
