using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed class LocalSaveService : ISaveService
    {
        [Serializable]
        private sealed class Envelope
        {
            public int Format = 1;
            public string Payload;
            public string Checksum;
        }
        private sealed class NewerSaveException : Exception { }
        private readonly string path;
        public string Status { get; private set; }
        public bool CanWrite { get; private set; }
        public LocalSaveService(string filePath)
        {
            path = Path.GetFullPath(filePath); CanWrite = true; Status = "";
        }
        public AccountData Load()
        {
            CanWrite = true; Status = "";
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new AccountData();
            try { return Read(path); }
            catch (NewerSaveException) { return ProtectNewerSave(); }
            catch (Exception e) when (Recoverable(e)) { }
            try
            {
                var restored = Read(path + ".bak");
                Status = "Recovered your account from its backup."; return restored;
            }
            catch (NewerSaveException) { return ProtectNewerSave(); }
            catch (Exception e) when (Recoverable(e))
            {
                CanWrite = false;
                Status = "Account file could not be read. Playing without saving; existing files were preserved.";
                return new AccountData();
            }
        }
        private AccountData ProtectNewerSave()
        {
            CanWrite = false; Status = "This account belongs to a newer game version. Existing save preserved; saving disabled.";
            return new AccountData();
        }
        public void Save(AccountData account)
        {
            if (!CanWrite) throw new IOException(Status);
            if (account == null || account.Version < 1 || account.Version > 3) throw new ArgumentException("Invalid account snapshot.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string payload = JsonUtility.ToJson(account);
            string text = JsonUtility.ToJson(new Envelope { Payload = payload, Checksum = Hash(payload) },true);
            if (Encoding.UTF8.GetByteCount(text) > 1024 * 1024) throw new IOException("Account exceeds local save capacity; nothing was written.");
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                stream.Write(bytes,0,bytes.Length); stream.Flush(true);
            }
            if (File.Exists(path))
            {
                // A corrupt primary must never replace a healthy recovery copy.
                bool primaryValid = false;
                try { Read(path); primaryValid = true; }
                catch (NewerSaveException) { CanWrite = false; throw new IOException("Newer save version preserved."); }
                catch (Exception e) when (Recoverable(e)) { }
                File.Replace(temporary,path,primaryValid ? path + ".bak" : null);
            }
            else File.Move(temporary,path);
            Status = "Saved locally";
        }
        private static AccountData Read(string file)
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length > 1024 * 1024) throw new IOException("Account file unavailable or too large.");
            Envelope envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
            if (envelope != null && envelope.Format > 1) throw new NewerSaveException();
            if (envelope == null || envelope.Format != 1 || string.IsNullOrEmpty(envelope.Payload) || envelope.Checksum != Hash(envelope.Payload))
                throw new IOException("Invalid account checksum.");
            AccountData account = JsonUtility.FromJson<AccountData>(envelope.Payload);
            if (account != null && account.Version > 3) throw new NewerSaveException();
            if (account == null || account.Version < 1 || account.TotalXp < 0 || account.UnlockedCharacters == null)
                throw new IOException("Invalid account data.");
            SocialState.Copy(account.Social,CollectionCatalog.CreateDefault());
            return account;
        }
        private static bool Recoverable(Exception e)
        { return e is IOException || e is UnauthorizedAccessException || e is ArgumentException; }
        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");
        }
    }
}
