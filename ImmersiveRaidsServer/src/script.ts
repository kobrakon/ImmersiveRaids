import { DependencyContainer } from "tsyringe";
import { IPreAkiLoadMod } from "@spt-aki/models/external/IPreAkiLoadMod";
import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import { StaticRouterModService } from "@spt-aki/services/mod/staticRouter/StaticRouterModService";
import { ProfileHelper } from "@spt-aki/helpers/ProfileHelper";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { IPmcData } from "@spt-aki/models/eft/common/IPmcData"
import { IDatabaseTables } from "@spt-aki/models/spt/server/IDatabaseTables";

class ImmersiveRaids implements IPreAkiLoadMod, IPostDBLoadMod
{
    // ignore dis
    public preAkiLoad(container: DependencyContainer): void 
    {    
        const staticRouterModService = container.resolve<StaticRouterModService>("StaticRouterModService");
        const Logger = container.resolve<ILogger>("WinstonLogger");
        const profileHelper = container.resolve<ProfileHelper>("ProfileHelper");

        staticRouterModService.registerStaticRouter
        (
            "inventoryupdaterouter",
            [
                {
                    url: "/ir/profile/update",
                    action: (url, info: IUpdateProfileRequest, sessionID, output) =>
                    {
                        const profile = profileHelper.getPmcProfile(sessionID);
                        const hideout = info.player.Inventory.items;

                        profile.Inventory.items = hideout;
                        Logger.info(`[IR] Imported extracted items to inventory (${profile._id})`);
                    }
                }
            ],
            "ir-update-profile-route"
        );

    }

    public postDBLoad(container: DependencyContainer)
    {
        const database = container.resolve<DatabaseServer>("DatabaseServer").getTables() as IDatabaseTables;
        const Logger = container.resolve<ILogger>("WinstonLogger");
        const globals = database.globals.config;

        for (let location in database.locations)
        {
            if (location == "base") continue;
            database.locations[location].base.EscapeTimeLimit = 9999999; // literally makes it so the game session lasts a couple months lmao
        }
        
        globals.Health.Effects.Existence.EnergyDamage = 0.75;
        globals.Health.Effects.Existence.HydrationDamage = 0.75;

        Logger.info("[IR] Init complete");
    }
}

// buh??
interface IUpdateProfileRequest
{
    player: IPmcData;
}

module.exports = { mod: new ImmersiveRaids() };