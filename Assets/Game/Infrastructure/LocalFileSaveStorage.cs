using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Application;

namespace Game.Infrastructure
{
    internal enum AtomicSaveWriteStage : byte
    {
        TemporaryFileFlushed = 0,
        BackupCopied = 1,
        PrimaryReplaced = 2
    }

    internal interface IAtomicSaveWriteObserver
    {
        void OnStage(AtomicSaveWriteStage stage);
    }

    /// <summary>Local, cancellation-aware, atomic save storage with one previous-version backup.</summary>
    public sealed class LocalFileSaveStorage : ISaveStorage
    {
        private readonly string rootDirectory;
        private readonly IAtomicSaveWriteObserver observer;

        /// <summary>Creates storage rooted at an absolute or resolvable local directory.</summary>
        public LocalFileSaveStorage(string rootPath)
            : this(rootPath, null)
        {
        }

        internal LocalFileSaveStorage(string rootPath, IAtomicSaveWriteObserver writeObserver)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Save root is required.", nameof(rootPath));
            rootDirectory = Path.GetFullPath(rootPath);
            observer = writeObserver;
        }

        /// <summary>Gets the normalized local save directory.</summary>
        public string RootDirectory => rootDirectory;

        /// <summary>Reads primary and backup bytes for a validated slot name.</summary>
        public async ValueTask<SaveStorageReadResult> ReadAsync(string slot, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var primaryPath = ResolveSlot(slot);
                var backupPath = primaryPath + ".bak";
                var primary = File.Exists(primaryPath)
                    ? await ReadAllBytesAsync(primaryPath, cancellationToken).ConfigureAwait(false)
                    : Array.Empty<byte>();
                var backup = File.Exists(backupPath)
                    ? await ReadAllBytesAsync(backupPath, cancellationToken).ConfigureAwait(false)
                    : Array.Empty<byte>();
                if (primary.Length == 0 && backup.Length == 0)
                    return SaveStorageReadResult.Failure(new SaveDiagnostic(SaveFailureCode.NotFound, "save.error.not_found", slot));
                return SaveStorageReadResult.Success(primary, backup);
            }
            catch (OperationCanceledException)
            {
                return SaveStorageReadResult.Failure(new SaveDiagnostic(SaveFailureCode.Cancelled, "save.error.cancelled", slot));
            }
            catch (Exception exception) when (IsIoFailure(exception))
            {
                return SaveStorageReadResult.Failure(new SaveDiagnostic(SaveFailureCode.IoFailure, "save.error.io", exception.GetType().Name));
            }
        }

        /// <summary>Writes through a flushed temporary file and atomic replacement.</summary>
        public async ValueTask<SaveStorageWriteResult> WriteAtomicAsync(string slot, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            string temporaryPath = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(rootDirectory);
                var primaryPath = ResolveSlot(slot);
                temporaryPath = primaryPath + ".tmp";
                var backupPath = primaryPath + ".bak";
                await WriteAllBytesAsync(temporaryPath, data, cancellationToken).ConfigureAwait(false);
                observer?.OnStage(AtomicSaveWriteStage.TemporaryFileFlushed);
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(primaryPath))
                {
                    File.Copy(primaryPath, backupPath, true);
                    observer?.OnStage(AtomicSaveWriteStage.BackupCopied);
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Replace(temporaryPath, primaryPath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, primaryPath);
                }
                temporaryPath = null;
                observer?.OnStage(AtomicSaveWriteStage.PrimaryReplaced);
                return SaveStorageWriteResult.Success();
            }
            catch (OperationCanceledException)
            {
                return SaveStorageWriteResult.Failure(new SaveDiagnostic(SaveFailureCode.Cancelled, "save.error.cancelled", slot));
            }
            catch (Exception exception) when (IsIoFailure(exception))
            {
                return SaveStorageWriteResult.Failure(new SaveDiagnostic(SaveFailureCode.IoFailure, "save.error.io", exception.GetType().Name));
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryPath))
                {
                    try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        /// <summary>Deletes primary, temporary, and backup files for a slot.</summary>
        public ValueTask<SaveStorageWriteResult> DeleteAsync(string slot, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var primaryPath = ResolveSlot(slot);
                DeleteIfPresent(primaryPath);
                DeleteIfPresent(primaryPath + ".tmp");
                DeleteIfPresent(primaryPath + ".bak");
                return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Success());
            }
            catch (OperationCanceledException)
            {
                return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Failure(new SaveDiagnostic(SaveFailureCode.Cancelled, "save.error.cancelled", slot)));
            }
            catch (Exception exception) when (IsIoFailure(exception))
            {
                return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Failure(new SaveDiagnostic(SaveFailureCode.IoFailure, "save.error.io", exception.GetType().Name)));
            }
        }

        private string ResolveSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot) ||
                !string.Equals(slot, Path.GetFileName(slot), StringComparison.Ordinal) ||
                !slot.EndsWith(".json", StringComparison.Ordinal))
                throw new ArgumentException("Save slot must be a JSON file name without a path.", nameof(slot));
            var resolved = Path.GetFullPath(Path.Combine(rootDirectory, slot));
            var prefix = rootDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? rootDirectory
                : rootDirectory + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save slot escaped the configured root.", nameof(slot));
            return resolved;
        }

        private static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken token)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                if (stream.Length > int.MaxValue) throw new IOException("Save file is too large.");
                var data = new byte[(int)stream.Length];
                var offset = 0;
                while (offset < data.Length)
                {
                    var read = await stream.ReadAsync(data, offset, data.Length - offset, token).ConfigureAwait(false);
                    if (read == 0) break;
                    offset += read;
                }
                if (offset != data.Length) throw new EndOfStreamException("Save file ended unexpectedly.");
                return data;
            }
        }

        private static async Task WriteAllBytesAsync(string path, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                var array = data.ToArray();
                await stream.WriteAsync(array, 0, array.Length, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
        }

        private static bool IsIoFailure(Exception exception) =>
            exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException;

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
