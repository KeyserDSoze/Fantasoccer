using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Fantasoccer.Application.Api.Controllers;

namespace Fantasoccer.Application.Api.LocalDevelopment;

// A deployment without PostgreSQL must not advertise endpoints whose services do not exist.
public sealed class WorkspaceControllerFeatureProvider(bool enabled) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (!enabled)
        {
            feature.Controllers.Remove(typeof(GroupWorkspaceController).GetTypeInfo());
            feature.Controllers.Remove(typeof(AccountsController).GetTypeInfo());
            feature.Controllers.Remove(typeof(GroupsController).GetTypeInfo());
            feature.Controllers.Remove(typeof(TeamsController).GetTypeInfo());
            feature.Controllers.Remove(typeof(TeamAssetsController).GetTypeInfo());
            feature.Controllers.Remove(typeof(FootballController).GetTypeInfo());
            feature.Controllers.Remove(typeof(CompetitionsController).GetTypeInfo());
            feature.Controllers.Remove(typeof(TradesController).GetTypeInfo());
            feature.Controllers.Remove(typeof(AuctionsController).GetTypeInfo());
            feature.Controllers.Remove(typeof(HallArchiveController).GetTypeInfo());
            feature.Controllers.Remove(typeof(CardsController).GetTypeInfo());
            feature.Controllers.Remove(typeof(NotificationSettingsController).GetTypeInfo());
            feature.Controllers.Remove(typeof(PlayerImagesController).GetTypeInfo());
            feature.Controllers.Remove(typeof(EditorialController).GetTypeInfo());
            feature.Controllers.Remove(typeof(OperationsController).GetTypeInfo());
        }
        else
        {
            // Do not leave a second Blob-backed authorization/data path available
            // after SQL becomes authoritative. Unported adapters stay unavailable.
            var supported = new[] { typeof(AccountsController), typeof(GroupsController), typeof(TeamsController),
                typeof(GroupWorkspaceController), typeof(TeamAssetsController), typeof(FootballController), typeof(CompetitionsController), typeof(TradesController), typeof(AuctionsController), typeof(HallArchiveController), typeof(CardsController), typeof(NotificationSettingsController), typeof(PlayerImagesController), typeof(EditorialController), typeof(OperationsController), typeof(ContextController), typeof(PingController) };
            foreach (var controller in feature.Controllers.Where(type => !supported.Contains(type.AsType())).ToArray())
                feature.Controllers.Remove(controller);
        }
    }
}
