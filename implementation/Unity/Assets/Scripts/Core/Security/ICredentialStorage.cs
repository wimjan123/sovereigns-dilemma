using System;
using System.Threading.Tasks;

namespace SovereignsDilemma.Core.Security
{
    /// <summary>
    /// Cross-platform secure credential storage interface.
    /// Supports Windows Credential Manager, macOS Keychain, Linux Secret Service,
    /// and encrypted fallback storage.
    /// </summary>
    public interface ICredentialStorage
    {
        /// <summary>
        /// Stores a credential securely using platform-native storage.
        /// </summary>
        /// <param name="key">Unique identifier for the credential</param>
        /// <param name="value">The credential value to store securely</param>
        /// <param name="description">Optional description for the credential</param>
        Task<bool> StoreCredentialAsync(string key, string value, string description = null);

        /// <summary>
        /// Retrieves a credential from secure storage.
        /// </summary>
        /// <param name="key">Unique identifier for the credential</param>
        /// <returns>The credential value if found, null otherwise</returns>
        Task<string> RetrieveCredentialAsync(string key);

        /// <summary>
        /// Removes a credential from secure storage.
        /// </summary>
        /// <param name="key">Unique identifier for the credential</param>
        /// <returns>True if successfully removed, false if not found</returns>
        Task<bool> RemoveCredentialAsync(string key);

        /// <summary>
        /// Checks if a credential exists in secure storage.
        /// </summary>
        /// <param name="key">Unique identifier for the credential</param>
        /// <returns>True if the credential exists, false otherwise</returns>
        Task<bool> CredentialExistsAsync(string key);

        /// <summary>
        /// Gets the platform-specific storage type being used.
        /// </summary>
        CredentialStorageType StorageType { get; }

        /// <summary>
        /// Whether the current platform supports native secure storage.
        /// </summary>
        bool SupportsNativeStorage { get; }
    }

    /// <summary>
    /// Types of credential storage available.
    /// </summary>
    public enum CredentialStorageType
    {
        /// <summary>
        /// Windows Credential Manager
        /// </summary>
        WindowsCredentialManager,

        /// <summary>
        /// macOS Keychain Services
        /// </summary>
        MacOSKeychain,

        /// <summary>
        /// Linux Secret Service (GNOME Keyring, KDE Wallet)
        /// </summary>
        LinuxSecretService,

        /// <summary>
        /// Encrypted file-based fallback storage
        /// </summary>
        EncryptedFile,

        /// <summary>
        /// In-memory storage (for testing only)
        /// </summary>
        InMemory
    }

    /// <summary>
    /// Exception thrown when credential operations fail.
    /// </summary>
    public class CredentialStorageException : Exception
    {
        public CredentialStorageType StorageType { get; }

        public CredentialStorageException(string message, CredentialStorageType storageType)
            : base(message)
        {
            StorageType = storageType;
        }

        public CredentialStorageException(string message, CredentialStorageType storageType, Exception innerException)
            : base(message, innerException)
        {
            StorageType = storageType;
        }
    }
}