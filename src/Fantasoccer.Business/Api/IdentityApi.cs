using FantacalcioV4.Database;
using Microsoft.AspNetCore.Http;
using Rystem.Text;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebPush;

namespace Fantasoccer.Business
{
    public class IdentityApi
    {
        private Logger Logger;
        public IdentityApi(Logger logger)
        {
            Logger = logger;
        }
        public async Task<SecuredIdentity> GetUserAsync(string username, string password)
            => await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == username && x.SecondaryKey == password).NoContext();
        public async Task<SecuredIdentity> GetUserAsync(string username)
            => await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == username).NoContext();
        public async Task ChangeTeamNameAsync(string username, string groupName, int year, string name)
        {
            var identity = await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == username).NoContext();
            var group = identity.Groups.FirstOrDefault(x => x.Name == groupName);
            if (group != default)
            {
                var team = group.Teams?.FirstOrDefault(x => x.Year == year);
                if (team != default)
                {
                    group.Teams.Remove(team);
                    group.Teams.Add(team with { Name = name });
                }
                else
                {
                    group.Teams.Add(new TeamByYear(FantacalcioId.GetTeam(groupName, year, username), name, new()));
                }
            }
            await identity.SaveAsync().NoContext();
        }
        public async Task SaveTeamImageAsync(string teamId, IFormFile image)
        {
            await new ImageWrapper
            {
                Name = $"teams/{teamId}.png",
            }.SaveAsync(image.OpenReadStream(), image.ContentType);
        }
        public async Task<bool> NewUserAsync(StoredIdentity identity)
        {
            if (!await identity.ExistsAsync().NoContext())
                return await identity.SaveAsync().NoContext();
            return false;
        }
        public Task<bool> UpdateUserAsync(StoredIdentity identity)
            => identity.SaveAsync();
        public async Task AddPushSubscriptionAsync(string username, PushSubscription pushSubscription)
        {
            if (pushSubscription != default)
            {
                await new StoredPushNotification
                {
                    PrimaryKey = username,
                    SecondaryKey = pushSubscription.Auth,
                    Endpoint = pushSubscription.Endpoint,
                    P256DH = pushSubscription.P256DH
                }.UpdateAsync().NoContext();
            }
        }
        public async Task RemovePushSubscriptionAsync(string username, PushSubscription pushSubscription)
        {
            if (pushSubscription != default)
            {
                await new StoredPushNotification
                {
                    PrimaryKey = username,
                    SecondaryKey = pushSubscription.Auth,
                }.DeleteAsync().NoContext();
            }
        }
        private const string PublicKey = "BC-_PiSEDN7I_pQsMKcDZeIDSPG-_2cCVvseMFvug6SNCI0hI7lqR8viPNdCKig7ny2iSdOeGI7ioBup2vd4tJ8";
        private const string PrivateKey = "uPF1e0ItKwn4WRSD8f88M9U43XjUuUCY9Bn1tA0GPWg";
        public async Task SendNotificationAsync(string username, Notification message)
        {
            var notificationPoints = await new StoredPushNotification().GetAsync(x => x.PrimaryKey == username).NoContext();
            foreach (var notification in notificationPoints)
            {
                var subscription = new PushSubscription(notification.Endpoint, notification.P256DH, notification.SecondaryKey);
                var vapidDetails = new VapidDetails($"mailto:{username.Split('@').First()}@fantacalcio.xyz", PublicKey, PrivateKey);

                var webPushClient = new WebPushClient();
                try
                {
                    string jsonMessage = message.ToJson();
                    await webPushClient.SendNotificationAsync(subscription, jsonMessage, vapidDetails);
                }
                catch (WebPushException wpe)
                {
                    if (wpe.Message.ToLower().Contains("subscription no longer valid"))
                    {
                        await RemovePushSubscriptionAsync(username, new PushSubscription
                        {
                            Endpoint = wpe.PushSubscription.Endpoint,
                            Auth = wpe.PushSubscription.Auth,
                            P256DH = wpe.PushSubscription.P256DH
                        }).NoContext();
                    }
                    await Logger.CatchAsync<WebPushClient>(wpe).NoContext();
                }
                catch
                {
                }
            }
        }
    }
}