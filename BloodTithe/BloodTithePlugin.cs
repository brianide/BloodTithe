using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using static TShockAPI.GetDataHandlers;

namespace BloodTithe
{
	[ApiVersion(2, 1)]
	public class BloodTithePlugin : TerrariaPlugin
	{
		public override string Name => "Blood Tithe";
		public override string Description => "Provides a means to prevent Demon Altar corruption";
		public override string Author => "gigabarn";
		public override Version Version => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

		private BloodTitheConfig Config;

		private readonly ConditionalWeakTable<object, EntityAttachment> NPCAttachments = new ConditionalWeakTable<object, EntityAttachment>();
		private readonly IDictionary<(int x, int y), int> PendingAltars = new Dictionary<(int, int), int>();

		public BloodTithePlugin(Main game) : base(game) { }

		public override void Initialize()
		{
			Config = BloodTitheConfig.Load();

			RegisterCorruptionTracking();
			RegisterAIHandling();

			// Add debug commands
			Util.RegisterChatCommands("bloodtithe.debug",
				("bt_printconfig", args => args.Player.SendInfoMessage(Config.ToString())),
				("bt_pending", args => PendingAltars.ForEach(kv => args.Player.SendInfoMessage("{0},{1}: {2}", kv.Key.x, kv.Key.y, kv.Value))),
				("bt_needammo", args => Item.NewItem(args.Player.TPlayer.Top, Vector2.Zero, Config.Item, Stack: 30, noGrabDelay: true)));
		}

		private void RegisterCorruptionTracking()
		{
			Vector2? altarPopLocation = null;

			// Track Life Crystals on altars
			OTAPI.Hooks.Item.PreUpdate += delegate (Item item, ref int id)
			{
				// Don't pop altars pre-HM
				if (!Main.hardMode)
					return OTAPI.HookResult.Continue;

				// Only care if it's a Life Crystal (or whatever)
				if (item.type != Config.Item)
					return OTAPI.HookResult.Continue;

				// Only care if it's on top of a Demon Altar
				var tileCoord = item.Center.ToTileCoordinates();
				if (Main.tile.TileAt(tileCoord).type != TileID.DemonAltar)
					return OTAPI.HookResult.Continue;

				// Find out how many times this altar has already been hit with Life Crystals
				var (bounds, origin) = Util.GetAltarBoundsFromTile(tileCoord.X, tileCoord.Y);
				var altarHealth = PendingAltars.GetValue((origin.X, origin.Y), Config.ItemsRequired);

				// Hit it with the current stack
				altarHealth = Util.ConsumeFromStack(altarHealth, item, id, goPoof: true, bounceLeftovers: true);

				// Record the new health value if the altar is still "alive"
				if (altarHealth > 0)
				{
					PendingAltars[(origin.X, origin.Y)] = altarHealth;
					return OTAPI.HookResult.Continue;
				}

				// Record the altar destruction
				altarPopLocation = bounds.Center.ToVector2();
				PendingAltars.Remove((origin.X, origin.Y));

				// Manually fire the normal altar-nuking results
				TSPlayer.All.SendData(PacketTypes.Tile, null, 0, tileCoord.X, tileCoord.Y);
				Util.DoFairyFX(altarPopLocation.Value, 0);
				WorldGen.KillTile(origin.X, origin.Y, false);

				return OTAPI.HookResult.Continue;
			};

			// Catch tile corruption events
			ServerApi.Hooks.NetSendData.Register(this, args =>
			{
				// Bail if we're not tracking an altar being popped
				if (!altarPopLocation.HasValue)
					return;

				if (args.MsgId == PacketTypes.TileSendSquare)
				{
					var tilePos = new Point((int)args.number2, (int)args.number3);
					var (tx, ty) = tilePos;
					var tile = Main.tile[tx, ty];

					// Limit our search to infectious biomes; otherwise we'll catch the ore packets
					if (tile.IsInfectious() && tile.IsStone())
					{
						TShock.Log.Debug("Tile corrupted at {0},{1} (TileID: {2})", tilePos.X, tilePos.Y, tile.type);

						// Flip the tile back on the server, and cancel the packet
						if (Config.PreventCorruption)
						{
							TShock.Log.Debug("Tile corruption cancelled");
							tile.type = TileID.Stone;
							args.Handled = true;
						}

						// Spawn fairy to warp to the tile
						if (Config.SpawnWarpFairy)
						{
							var loc = Util.FindProbablySafeTeleportLocation(tilePos);
							SpawnWarpFairy(altarPopLocation.Value, loc, tilePos);
						}

						// Stop watching, since we've caught our corruption event
						altarPopLocation = null;
					}
				}
				// If no corruption event occurs, the next outgoing packet we see should be the wraith(s) spawning
				else if (args.MsgId == PacketTypes.NpcUpdate)
				{
					TShock.Log.Info("No corruption event was observed");
					altarPopLocation = null;
				}
			});

			// Cleanup altar health tracking if it gets broken the old fashioned way
			TShockAPI.GetDataHandlers.TileEdit += delegate (object sender, TileEditEventArgs args)
			{
				if (args.Action == EditAction.KillTile && Main.tile[args.X, args.Y].type == TileID.DemonAltar && args.EditData == 0)
					Util.GetAltarBoundsFromTile(args.X, args.Y).tileOrigin.Let(p => PendingAltars.Remove((p.X, p.Y)));
			};
		}

		private void SpawnWarpFairy(Vector2 pos, Vector2 dest, Point corruptedTile)
		{
			// Spawn the idler fairy to teleport us
			var ferryFairy = Main.npc[NPC.NewNPC((int)pos.X, (int)pos.Y, NPCID.FairyCritterPink, ai2: 5)];
			ferryFairy.TargetClosest();
			Util.DoFairyFX(pos, 0);

			// Funny story; damage immunity doesn't kick in for a fairy until its first AI update, so it's
			// quite possible for an errant hammerswing to splatter it before it's had the chance to finish
			// spawning. You might suppose we could set the dontTakeDamage flag manually, but there's not a packet
			// that sets it, so the fairy would still die on the client's end, which would be a headache to deal
			// with. I've found a suitable workaround is to just give the fairy a shitload of HP and have the AI
			// quickly recenter itself in the event that it gets smacked around.

			// Incidentally this is the Empress's max HP value. All fairies are queens.
			ferryFairy.life = 124950;
			ferryFairy.lifeMax = 124950;

			// Load the destination section
			Main.player.Where(p => p.active).ForEach(p => RemoteClient.CheckSection(p.whoAmI, dest));

			// Attach special handling data
			NPCAttachments.Add(ferryFairy, new FairyExtendedIdle(ferryFairy, pos, player =>
			{
				// Pop the idler fairy and teleport the player
				Util.DoFairyFX(pos, 0);
				ferryFairy.active = false;

				player.Teleport(dest.X, dest.Y, TeleportationStyleID.MagicConch);
				Util.DoFairyFX(dest, 0);

				// The guide fairy doesn't actually need any special logic; we just have to set its "treasure"
				// coordinates ai[0] and ai[1] manually and initialize its state ai[2] to 3
				var guideFairy = Main.npc[NPC.NewNPC((int)dest.X, (int)dest.Y - 20, NPCID.FairyCritterPink, ai0: corruptedTile.X, ai1: corruptedTile.Y, ai2: 3)];

				// Safeguarding against the player again
				guideFairy.life = 124950;
				guideFairy.lifeMax = 124950;

				// Make sure we don't despawn before the player shows up
				guideFairy.timeLeft *= 100;
			}));
		}

		private void RegisterAIHandling()
		{
			ServerApi.Hooks.NpcAIUpdate.Register(this, args =>
			{
				// Try and find an attachment for the current NPC;
				// if it has one, run its Update method
				NPCAttachments.GetValue(args.Npc)?.Update().Let(keep =>
				{
					// If the Update method returned false, detach from the NPC
					if (!keep)
						NPCAttachments.Remove(args.Npc);
				});
			});
		}

	}
}
