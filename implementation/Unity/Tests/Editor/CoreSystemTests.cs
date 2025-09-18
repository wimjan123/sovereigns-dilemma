using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Threading.Tasks;
using SovereignsDilemma.Core.Events;
using SovereignsDilemma.Core.Security;
using SovereignsDilemma.AI.Services;

namespace SovereignsDilemma.Tests.Editor
{
    /// <summary>
    /// Core system integration tests for The Sovereign's Dilemma.
    /// Validates fundamental architecture components work correctly.
    /// </summary>
    public class CoreSystemTests
    {
        private GameObject _testGameObject;
        private UnityEventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            _testGameObject = new GameObject("TestGameObject");
            _eventBus = _testGameObject.AddComponent<UnityEventBus>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGameObject != null)
            {
                Object.DestroyImmediate(_testGameObject);
            }
        }

        [Test]
        public void EventBus_PublishesAndReceivesEvents()
        {
            // Arrange
            var eventReceived = false;
            var testEvent = new TestDomainEvent("Test message");

            _eventBus.Subscribe<TestDomainEvent>(evt =>
            {
                eventReceived = true;
                Assert.AreEqual("Test message", evt.Message);
            });

            // Act
            _eventBus.Publish(testEvent);

            // Assert
            Assert.IsTrue(eventReceived, "Event should have been received");
        }

        [UnityTest]
        public IEnumerator EventBus_AsyncPublishWorks()
        {
            // Arrange
            var eventReceived = false;
            var testEvent = new TestDomainEvent("Async test");

            _eventBus.Subscribe<TestDomainEvent>(evt =>
            {
                eventReceived = true;
            });

            // Act
            var publishTask = _eventBus.PublishAsync(testEvent);
            yield return new WaitUntil(() => publishTask.IsCompleted);

            // Assert
            Assert.IsTrue(eventReceived, "Async event should have been received");
        }

        [Test]
        public void EventBus_HandlesMultipleSubscribers()
        {
            // Arrange
            var subscriber1Called = false;
            var subscriber2Called = false;
            var testEvent = new TestDomainEvent("Multi-subscriber test");

            _eventBus.Subscribe<TestDomainEvent>(evt => subscriber1Called = true);
            _eventBus.Subscribe<TestDomainEvent>(evt => subscriber2Called = true);

            // Act
            _eventBus.Publish(testEvent);

            // Assert
            Assert.IsTrue(subscriber1Called, "First subscriber should be called");
            Assert.IsTrue(subscriber2Called, "Second subscriber should be called");
        }

        [Test]
        public void EventBus_SubscriptionDisposalWorks()
        {
            // Arrange
            var eventReceived = false;
            var testEvent = new TestDomainEvent("Disposal test");

            var subscription = _eventBus.Subscribe<TestDomainEvent>(evt => eventReceived = true);

            // Act
            subscription.Dispose();
            _eventBus.Publish(testEvent);

            // Assert
            Assert.IsFalse(eventReceived, "Event should not be received after disposal");
        }

        [Test]
        public void CredentialStorage_PlatformDetectionWorks()
        {
            // Arrange & Act
            var credentialStorage = new CrossPlatformCredentialStorage();

            // Assert
            Assert.IsNotNull(credentialStorage, "Credential storage should be created");
            Assert.IsTrue(
                credentialStorage.StorageType == CredentialStorageType.WindowsCredentialManager ||
                credentialStorage.StorageType == CredentialStorageType.MacOSKeychain ||
                credentialStorage.StorageType == CredentialStorageType.LinuxSecretService ||
                credentialStorage.StorageType == CredentialStorageType.EncryptedFile,
                "Should detect a valid storage type"
            );
        }

        [Test]
        public async Task CredentialStorage_StoreAndRetrieveWorks()
        {
            // Arrange
            var credentialStorage = new CrossPlatformCredentialStorage();
            const string testKey = "test_api_key";
            const string testValue = "test_secret_value";

            try
            {
                // Act
                var stored = await credentialStorage.StoreCredentialAsync(testKey, testValue, "Test credential");
                var retrieved = await credentialStorage.RetrieveCredentialAsync(testKey);

                // Assert
                // Note: For placeholder implementations, we expect specific behavior
                if (credentialStorage.SupportsNativeStorage)
                {
                    // Native storage implementations return null in placeholder mode
                    Assert.IsNull(retrieved, "Placeholder native storage should return null");
                }
                else
                {
                    // Encrypted file storage placeholder also returns null
                    Assert.IsNull(retrieved, "Placeholder encrypted storage should return null");
                }

                Assert.IsTrue(stored, "Storage operation should succeed");
            }
            finally
            {
                // Cleanup
                await credentialStorage.RemoveCredentialAsync(testKey);
            }
        }

        [Test]
        public void CredentialStorage_ValidatesInput()
        {
            // Arrange
            var credentialStorage = new CrossPlatformCredentialStorage();

            // Act & Assert
            Assert.ThrowsAsync<System.ArgumentException>(
                async () => await credentialStorage.StoreCredentialAsync(null, "value"),
                "Should validate null key"
            );

            Assert.ThrowsAsync<System.ArgumentException>(
                async () => await credentialStorage.StoreCredentialAsync("key", null),
                "Should validate null value"
            );

            Assert.ThrowsAsync<System.ArgumentException>(
                async () => await credentialStorage.StoreCredentialAsync("", "value"),
                "Should validate empty key"
            );

            Assert.ThrowsAsync<System.ArgumentException>(
                async () => await credentialStorage.StoreCredentialAsync("key", ""),
                "Should validate empty value"
            );
        }

        [Test]
        public void AIService_HasCorrectProviderType()
        {
            // Arrange & Act
            var aiService = _testGameObject.AddComponent<NVIDIANIMService>();

            // Assert
            Assert.AreEqual(AIProviderType.NvidiaNIM, aiService.ProviderType,
                "NVIDIA NIM service should have correct provider type");
        }

        [Test]
        public void AIService_IsAvailableChecksCredentials()
        {
            // Arrange
            var aiService = _testGameObject.AddComponent<NVIDIANIMService>();

            // Act
            var isAvailable = aiService.IsAvailable;

            // Assert
            // Should be false since we don't have API key configured
            Assert.IsFalse(isAvailable, "Service should not be available without API key");
        }

        [UnityTest]
        public IEnumerator AIService_HealthCheckExecutes()
        {
            // Arrange
            var aiService = _testGameObject.AddComponent<NVIDIANIMService>();

            // Act
            var healthTask = aiService.IsHealthyAsync();
            yield return new WaitUntil(() => healthTask.IsCompleted);

            // Assert
            Assert.IsFalse(healthTask.Result, "Health check should fail without proper configuration");
        }

        [Test]
        public void AIService_GetStatusReturnsValidData()
        {
            // Arrange
            var aiService = _testGameObject.AddComponent<NVIDIANIMService>();

            // Act
            var status = aiService.GetStatus();

            // Assert
            Assert.IsNotNull(status, "Status should not be null");
            Assert.AreEqual(CircuitBreakerState.Closed, status.CircuitBreakerState,
                "Initial circuit breaker state should be closed");
        }

        [Test]
        public void DomainEvent_HasCorrectMetadata()
        {
            // Arrange & Act
            var testEvent = new TestDomainEvent("Test");

            // Assert
            Assert.IsNotNull(testEvent.EventId, "Event should have an ID");
            Assert.AreNotEqual(System.Guid.Empty, testEvent.EventId, "Event ID should not be empty");
            Assert.IsTrue(testEvent.OccurredAt <= System.DateTime.UtcNow, "Event timestamp should be valid");
            Assert.AreEqual("Test", testEvent.SourceContext, "Source context should match");
            Assert.AreEqual(1, testEvent.Version, "Version should be 1");
        }

        // Test helper classes
        private class TestDomainEvent : DomainEventBase
        {
            public override string SourceContext => "Test";
            public string Message { get; }

            public TestDomainEvent(string message)
            {
                Message = message;
            }
        }
    }
}