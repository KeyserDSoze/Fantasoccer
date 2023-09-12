using Rystem.Business;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class StoredPushNotification : IDocument
    {
        public string PrimaryKey { get; set; }
        public string SecondaryKey { get; set; }
        public DateTime Timestamp { get; set; }
        public string Endpoint { get; set; }
        public string P256DH { get; set; }
    }
    public class StoredIdentity : IDocument
    {
        public string PrimaryKey { get; set; }
        public string SecondaryKey { get; set; }
        public DateTime Timestamp { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsSuperAdmin { get; set; }
        public List<TeamByGroup> Groups { get; set; }
        public Settings Settings { get; set; }
        public StoredIdentity() { }
        public async Task<bool> SaveAsync()
        {
            bool result = await this.UpdateAsync();
            if (result)
                await new IdentityKey { Username = PrimaryKey }.RestoreAsync();
            return result;
        }
        public static implicit operator StoredIdentity(Identity identity)
            => new()
            {
                PrimaryKey = identity.Username,
                SecondaryKey = identity.Password,
                Timestamp = DateTime.UtcNow,
                Name = identity.Name,
                Surname = identity.Surname,
                Email = identity.Email,
                IsActive = identity.IsActive,
                IsAdmin = identity.IsAdmin,
                IsSuperAdmin = identity.IsSuperAdmin,
                Groups = identity.Groups,
                Settings = identity.Settings
            };
        public static implicit operator Identity(StoredIdentity storedIdentity)
            => new(storedIdentity.PrimaryKey, storedIdentity.SecondaryKey, storedIdentity.Name, storedIdentity.Surname, storedIdentity.Email, storedIdentity.IsActive, storedIdentity.IsAdmin, storedIdentity.IsSuperAdmin, storedIdentity.Groups, storedIdentity.Settings);
        public static implicit operator SecuredIdentity(StoredIdentity storedIdentity)
            => new(storedIdentity.PrimaryKey, storedIdentity.Name, storedIdentity.Surname, storedIdentity.Email, storedIdentity.IsActive, storedIdentity.IsAdmin, storedIdentity.IsSuperAdmin, storedIdentity.Groups, storedIdentity.Settings);
    }
}