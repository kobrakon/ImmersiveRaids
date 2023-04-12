import { DependencyContainer, inject, injectable } from "tsyringe";
import { IPreAkiLoadMod } from "@spt-aki/models/external/IPreAkiLoadMod";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import { DynamicRouterModService } from "@spt-aki/services/mod/dynamicRouter/DynamicRouterModService";
import { StaticRouterModService } from "@spt-aki/services/mod/staticRouter/StaticRouterModService";
import { ProfileHelper } from "@spt-aki/helpers/ProfileHelper";
import { HashUtil } from "@spt-aki/models/spt/utils/HashUtil";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { IPmcData } from "@spt-aki/models/eft/common/IPmcData"

class ImmersiveRaids implements IPreAkiLoadMod, IPostDBLoadMod
{
    constructor
    (
        @inject("WinstonLogger") private Logger: ILogger,
        @inject("DatabaseServer") private databaseServer : DatabaseServer,
        @inject("StaticRouterModService") private staticRouterModService : StaticRouterModService,
        @inject("DynamicRouterModService") private dynamicRouterModService : DynamicRouterModService,
        @inject("ProfileHelper") private profileHelper : ProfileHelper,
        @inject("HashUtil") private hashUtil : HashUtil
    )

    public preAkiLoad(container: DependencyContainer): void 
    {    
        staticRouterModService.RegisterStaticRouter
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
            ]
        );

        dynamicRouterModService.RegisterDynamicRouter
        (
            "inventorydynrouter",
            [
                {
                    url: "/ir/inventory/add", // id/template/parentid/parentname/stack/x/y/r/durability
                    action: (url: string, info, sessionID, output) =>
                    {
                        const splitUrl = url.split("/");
                        const database = databaseServer.getTables();
                        const items = database.templates.items;

                        const id = splitUrl[splitUrl.length - 9];
                        const template = splitUrl[splitUrl.length - 8]
                        const parent = splitUrl[splitUrl.length - 7];
                        const parentname = splitUrl[splitUrl.length - 6];
                        const stack = splitUrl[splitUrl.length - 5];
                        const locX = splitUrl[splitUrl.length - 4];
                        const locY = splitUrl[splitUrl.length - 3];
                        const locR = splitUrl[splitUrl.length - 2];
                        const durability = splitUrl[splitUrl.length - 1];

                        const profile = profileHelper.getPmcProfile(sessionID);

                        profile.Inventory.items.push(
                        {
                            "_id": id,
                            "_tpl": template,
                            "parentId": parent,
                            "slotId": parentname,
                            "location": {
                                "x": locX,
                                "y": locY,
                                "r": locR,
                                "isSearched": true
                            },
                            "upd":
                            {
                                "StackObjectsCount": stack,
                                "Repairable": 
                                {
                                    "MaxDurability": items[id]._props.MaxDurability,
                                    "Durability": durability == "NULL" ? items[id]._props.MaxDurability : durability
                                }
                            }
                         });
                    }
                }
            ]
        );

        dynamicRouterModService.RegisterDynamicRouter(
            "inventorydynmodrouter",
            [
                {
                    url: "/ir/inventory/add/mod", // id/template/parentid/slot
                    action: (url: string, info, sessionID, output) =>
                    {
                        const splitUrl = url.split("/");

                        const id = splitUrl[splitUrl.length - 4];
                        const template = splitUrl[splitUrl.length - 3]
                        const parentid = splitUrl[splitUrl.length - 2];
                        const slot = splitUrl[splitUrl.length - 1];

                        const profile = profileHelper.getPmcProfile(sessionID);

                        profile.Inventory.items.push(
                        {
                            "_id": id,
                            "_tpl": template,
                            "parentId": parentid,
                            "slotId": slot
                        });
                    }
                }
            ]
        );
    }

    public postDBLoad(container: DependencyContainer)
    {
        const database = databaseServer.getTables();
        const globals = database.globals.config;

        for (let location in database.locations)
            database.locations[location].base.exit_access_time = 99999999999999;
        
        globals.Health.Effects.Existence.EnergyDamage = 0.75;
        globals.Health.Effects.Existence.HydrationDamage = 0.75;
    }
}

// buh??
interface IUpdateProfileRequest
{
    player: IPmcData;
}

module.exports = { mod: new ImmersiveRaids() };