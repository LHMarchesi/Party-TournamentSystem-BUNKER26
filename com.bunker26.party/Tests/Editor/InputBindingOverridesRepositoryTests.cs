using System.IO;
using Bunker26.Party.Persistence;
using NUnit.Framework;

namespace Bunker26.Party.Tests
{
    public sealed class InputBindingOverridesRepositoryTests
    {
        private string _filePath;
        private JsonInputBindingOverridesRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), "bunker26-party-bindings-" + System.Guid.NewGuid().ToString("N") + ".json");
            _repository = new JsonInputBindingOverridesRepository(_filePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
            if (File.Exists(_filePath + ".tmp")) File.Delete(_filePath + ".tmp");
            if (File.Exists(_filePath + ".bak")) File.Delete(_filePath + ".bak");
        }

        [Test]
        public void Overrides_AreIsolatedByProfileGameAndScheme()
        {
            _repository.Save("profile-a", "game-a", "KeyboardLeft", "{\"override\":\"W\"}");
            _repository.Save("profile-a", "game-a", "Gamepad", "{\"override\":\"South\"}");
            _repository.Save("profile-b", "game-a", "KeyboardLeft", "{\"override\":\"UpArrow\"}");
            _repository.Save("profile-a", "game-b", "KeyboardLeft", "{\"override\":\"A\"}");

            Assert.AreEqual("{\"override\":\"W\"}", _repository.Load("profile-a", "game-a", "KeyboardLeft"));
            Assert.AreEqual("{\"override\":\"South\"}", _repository.Load("profile-a", "game-a", "Gamepad"));
            Assert.AreEqual("{\"override\":\"UpArrow\"}", _repository.Load("profile-b", "game-a", "KeyboardLeft"));
            Assert.AreEqual("{\"override\":\"A\"}", _repository.Load("profile-a", "game-b", "KeyboardLeft"));
        }

        [Test]
        public void Overrides_PersistAcrossRepositoryInstances()
        {
            _repository.Save("profile-a", "game-a", "KeyboardRight", "[1]");
            JsonInputBindingOverridesRepository reloaded = new JsonInputBindingOverridesRepository(_filePath);

            Assert.AreEqual("[1]", reloaded.Load("profile-a", "game-a", "KeyboardRight"));
        }

        [Test]
        public void Delete_RemovesOnlyTheRequestedScheme()
        {
            _repository.Save("profile-a", "game-a", "KeyboardLeft", "[1]");
            _repository.Save("profile-a", "game-a", "Gamepad", "[2]");
            _repository.Delete("profile-a", "game-a", "KeyboardLeft");

            Assert.AreEqual(string.Empty, _repository.Load("profile-a", "game-a", "KeyboardLeft"));
            Assert.AreEqual("[2]", _repository.Load("profile-a", "game-a", "Gamepad"));
        }
    }
}
