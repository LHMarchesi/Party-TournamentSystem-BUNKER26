#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Bunker26.Party;
using Bunker26.Party.Persistence;
using NUnit.Framework;
using System.Collections.Generic;

namespace Bunker26.Party.Tests
{
    public sealed class InMemoryProfileRepository : IProfileRepository
    {
        private readonly List<PlayerProfile> _profiles = new List<PlayerProfile>();
        public IReadOnlyList<PlayerProfile> LoadAll() { return _profiles.AsReadOnly(); }
        public PlayerProfile Load(string id) { return _profiles.Find(p => p.id == id); }
        public void Save(PlayerProfile p) { int i = _profiles.FindIndex(x => x.id == p.id); if (i >= 0) _profiles[i] = p; else _profiles.Add(p); }
        public bool Delete(string id) { int i = _profiles.FindIndex(p => p.id == id); if (i < 0) return false; _profiles.RemoveAt(i); return true; }
    }

    public sealed class InMemoryPlayerPreferencesRepository : IPlayerPreferencesRepository
    {
        private readonly Dictionary<int, string> _preferences = new Dictionary<int, string>();
        public string GetPreferredProfileId(int slot) { string value; return _preferences.TryGetValue(slot, out value) ? value : string.Empty; }
        public void SetPreferredProfileId(int slot, string id) { _preferences[slot] = id ?? string.Empty; }
        public void ClearPreferredProfileId(int slot) { _preferences.Remove(slot); }
    }

    public sealed class PartyServiceTests
    {
        [Test]
        public void SameProfileCannotBeAssignedTwice()
        {
            var profiles = new PlayerProfileService(new InMemoryProfileRepository());
            PlayerProfile profile = profiles.Create("Lautaro");
            var party = new PartyService(profiles);
            PartyParticipant a = party.AddLocalPlayer();
            PartyParticipant b = party.AddLocalPlayer();
            string error;
            Assert.IsTrue(party.AssignProfile(a.id, profile.id, out error));
            Assert.IsFalse(party.AssignProfile(b.id, profile.id, out error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void KeyboardHasTwoSeparateLogicalSlots()
        {
            var profiles = new PlayerProfileService(new InMemoryProfileRepository());
            var party = new PartyService(profiles);
            PartyParticipant a = party.AddLocalPlayer();
            PartyParticipant b = party.AddLocalPlayer();
            string error;
            Assert.IsTrue(party.AssignInput(a.id, InputAssignment.Keyboard(0), out error));
            Assert.IsTrue(party.AssignInput(b.id, InputAssignment.Keyboard(1), out error));
            Assert.IsFalse(party.AssignInput(b.id, InputAssignment.Keyboard(0), out error));
        }

        [Test]
        public void CurrentKeyboardSlotCannotBeAssignedAgain()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()));
            PartyParticipant player = party.InitializeLocalParty();
            string error;

            Assert.IsTrue(party.AssignInput(player.id, InputAssignment.Keyboard(0), out error), error);
            Assert.IsFalse(party.IsKeyboardSlotAvailable(0, player.id));
            Assert.IsFalse(party.AssignInput(player.id, InputAssignment.Keyboard(0), out error));
            StringAssert.Contains("already assigned", error);
            Assert.IsTrue(party.IsKeyboardSlotAvailable(1, player.id));
        }

        [Test]
        public void KeyboardSlotOwnedByAnotherPlayerIsUnavailable()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()));
            PartyParticipant playerOne = party.InitializeLocalParty();
            PartyParticipant playerTwo = party.AddLocalPlayer();
            string error;

            Assert.IsTrue(party.AssignInput(playerOne.id, InputAssignment.Keyboard(0), out error), error);
            Assert.IsFalse(party.IsKeyboardSlotAvailable(0, playerTwo.id));
            Assert.IsTrue(party.IsKeyboardSlotAvailable(1, playerTwo.id));
        }

        [Test]
        public void InitializeCreatesLeaderAndRestoresPreferredProfile()
        {
            var profiles = new PlayerProfileService(new InMemoryProfileRepository());
            PlayerProfile lautaro = profiles.Create("Lautaro");
            var preferences = new InMemoryPlayerPreferencesRepository();
            preferences.SetPreferredProfileId(1, lautaro.id);
            var party = new PartyService(profiles, new InputAssignmentService(), preferences, 4);

            PartyParticipant leader = party.InitializeLocalParty();

            Assert.AreEqual(1, leader.localSlotIndex);
            Assert.IsTrue(leader.IsLeader);
            Assert.AreEqual(lautaro.id, leader.profileId);
            Assert.AreEqual("Lautaro", leader.DisplayName);
            Assert.AreSame(leader, party.InitializeLocalParty(), "Initialize should be idempotent.");
        }

        [Test]
        public void LeadershipCanTransferWithoutChangingLocalSlots()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()), maxLocalPlayers: 4);
            PartyParticipant playerOne = party.InitializeLocalParty();
            PartyParticipant playerTwo = party.AddLocalPlayer();
            string error;

            Assert.IsTrue(party.AssignInput(playerOne.id, InputAssignment.Keyboard(0), out error), error);
            Assert.IsTrue(party.AssignInput(playerTwo.id, InputAssignment.Keyboard(1), out error), error);

            int partyChangedCount = 0;
            party.PartyChanged += () => partyChangedCount++;

            Assert.IsTrue(party.TrySetLeader(playerTwo.id, out error), error);
            Assert.IsFalse(playerOne.IsLeader);
            Assert.IsTrue(playerTwo.IsLeader);
            Assert.AreSame(playerTwo, party.Leader);
            Assert.AreEqual(1, playerOne.localSlotIndex);
            Assert.AreEqual(2, playerTwo.localSlotIndex);
            Assert.AreEqual(1, partyChangedCount);
        }

        [Test]
        public void ReinitializingPartyPreservesTransferredLeader()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()), maxLocalPlayers: 4);
            PartyParticipant playerOne = party.InitializeLocalParty();
            PartyParticipant playerTwo = party.AddLocalPlayer();
            string error;
            Assert.IsTrue(party.AssignInput(playerOne.id, InputAssignment.Keyboard(0), out error), error);
            Assert.IsTrue(party.AssignInput(playerTwo.id, InputAssignment.Keyboard(1), out error), error);
            Assert.IsTrue(party.TrySetLeader(playerTwo.id, out error), error);

            Assert.AreSame(playerOne, party.InitializeLocalParty());
            Assert.IsFalse(playerOne.IsLeader);
            Assert.IsTrue(playerTwo.IsLeader);
            Assert.AreSame(playerTwo, party.Leader);
        }

        [Test]
        public void LeadershipCannotTransferToUnassignedOrDisconnectedParticipant()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()), maxLocalPlayers: 4);
            PartyParticipant playerOne = party.InitializeLocalParty();
            PartyParticipant playerTwo = party.AddLocalPlayer();
            string error;

            Assert.IsFalse(party.TrySetLeader(playerTwo.id, out error));
            Assert.IsNotEmpty(error);
            Assert.IsTrue(playerOne.IsLeader);

            Assert.IsTrue(party.AssignInput(playerTwo.id, InputAssignment.Gamepad("42", "Disconnected", false), out error), error);
            Assert.IsFalse(party.TrySetLeader(playerTwo.id, out error));
            StringAssert.Contains("disconnected", error);
            Assert.IsTrue(playerOne.IsLeader);
        }

        [Test]
        public void LeadershipCannotBeTransferredToAI()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()), maxLocalPlayers: 4);
            PartyParticipant playerOne = party.InitializeLocalParty();
            PartyParticipant cpu = party.AddAI();
            string error;
            Assert.IsFalse(party.TrySetLeader(cpu.id, out error));
            Assert.IsNotEmpty(error);
            Assert.IsTrue(playerOne.IsLeader);
        }

        [Test]
        public void FirstGamepadClaimsPlayerOneThenCreatesPlayerTwo()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()), maxLocalPlayers: 4);
            PartyParticipant p1 = party.InitializeLocalParty();
            PartyParticipant joined1;
            PartyParticipant joined2;
            string error;

            Assert.IsTrue(party.TryJoinWithGamepad("10", "Gamepad A", out joined1, out error), error);
            Assert.AreSame(p1, joined1);
            Assert.AreEqual(InputDeviceKind.Gamepad, p1.inputAssignment.deviceKind);
            Assert.IsTrue(party.TryJoinWithGamepad("11", "Gamepad B", out joined2, out error), error);
            Assert.AreEqual(2, joined2.localSlotIndex);
            Assert.IsFalse(joined2.IsLeader);
        }

        [Test]
        public void DisconnectedGamepadSlotIsReusedByNextGamepad()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()), maxLocalPlayers: 4);
            PartyParticipant p1 = party.InitializeLocalParty();
            PartyParticipant firstJoin;
            PartyParticipant reconnect;
            string error;
            Assert.IsTrue(party.TryJoinWithGamepad("10", "Gamepad A", out firstJoin, out error), error);
            Assert.IsTrue(party.MarkGamepadDisconnected("10"));
            Assert.IsFalse(p1.inputAssignment.isConnected);

            Assert.IsTrue(party.TryJoinWithGamepad("12", "Replacement Gamepad", out reconnect, out error), error);
            Assert.AreSame(p1, reconnect);
            Assert.AreEqual("12", reconnect.inputAssignment.deviceId);
            Assert.IsTrue(reconnect.inputAssignment.isConnected);
        }


        [Test]
        public void NewGamepadParticipantRequestsProfileSelectionAfterInputAssignment()
        {
            var party = new PartyService(new PlayerProfileService(new InMemoryProfileRepository()), maxLocalPlayers: 4);
            PartyParticipant p1 = party.InitializeLocalParty();
            string error;
            Assert.IsTrue(party.AssignInput(p1.id, InputAssignment.Keyboard(0), out error), error);

            PartyParticipant joined;
            bool shouldOpenProfileSelection;
            Assert.IsTrue(party.TryJoinWithGamepad("21", "Gamepad B", out joined, out shouldOpenProfileSelection, out error), error);

            Assert.AreEqual(2, joined.localSlotIndex);
            Assert.AreEqual(InputDeviceKind.Gamepad, joined.inputAssignment.deviceKind);
            Assert.IsTrue(shouldOpenProfileSelection, "A newly added player must be taken to profile selection after input assignment.");
        }

        [Test]
        public void ReplacingDisconnectedKnownGamepadDoesNotForceProfileSelection()
        {
            var profiles = new PlayerProfileService(new InMemoryProfileRepository());
            PlayerProfile profile = profiles.Create("Gonzalo");
            var party = new PartyService(profiles, maxLocalPlayers: 4);
            PartyParticipant p1 = party.InitializeLocalParty();
            string error;
            Assert.IsTrue(party.AssignInput(p1.id, InputAssignment.Gamepad("31", "Old Gamepad", true), out error), error);
            Assert.IsTrue(party.AssignProfile(p1.id, profile.id, out error), error);
            Assert.IsTrue(party.MarkGamepadDisconnected("31"));

            PartyParticipant reconnected;
            bool shouldOpenProfileSelection;
            Assert.IsTrue(party.TryJoinWithGamepad("32", "Replacement Gamepad", out reconnected, out shouldOpenProfileSelection, out error), error);

            Assert.AreSame(p1, reconnected);
            Assert.IsFalse(shouldOpenProfileSelection, "A known participant replacing a disconnected gamepad should keep their profile without a forced picker.");
        }

        [Test]
        public void ProfilePreferenceIsSavedWhenAssignedAndClearedWhenRemoved()
        {
            var profiles = new PlayerProfileService(new InMemoryProfileRepository());
            PlayerProfile profile = profiles.Create("Gonzalo");
            var preferences = new InMemoryPlayerPreferencesRepository();
            var party = new PartyService(profiles, new InputAssignmentService(), preferences, 4);
            PartyParticipant p1 = party.InitializeLocalParty();
            string error;

            Assert.IsTrue(party.AssignProfile(p1.id, profile.id, out error), error);
            Assert.AreEqual(profile.id, preferences.GetPreferredProfileId(1));
            Assert.IsTrue(party.ClearProfile(p1.id));
            Assert.AreEqual(string.Empty, preferences.GetPreferredProfileId(1));
        }
    }
}
#endif
