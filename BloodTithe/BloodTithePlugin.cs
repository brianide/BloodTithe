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

		private readonly ConditionalWeakTable<NPC, object> NPCAttachments = new ConditionalWeakTable<NPC, object>();

		public BloodTithePlugin(Main game) : base(game) { }

		public override void Initialize()
		{
			Config = BloodTitheConfig.Load();

			RegisterCorruptionTracking();
			RegisterAIHandling();

			Util.RegisterChatCommands("bloodtithe.debug",
				("bt_printconfig", args => args.Player.SendInfoMessage(Config.ToString())),
				("bt_needammo", args => Item.NewItem(args.Player.TPlayer.Top, Vector2.Zero, Config.Item, Stack: 30, noGrabDelay: true)));
		}

		private void RegisterCorruptionTracking()
		{
			(TSPlayer who, Vector2 where)? lastAltarBash = null;

			// Watch SendTileSquare packets for altars being broken
			TShockAPI.GetDataHandlers.TileEdit += delegate (object sender, TileEditEventArgs args)
			{
				if (args.Action == EditAction.KillTile && Main.tile[args.X, args.Y].type == TileID.DemonAltar && args.EditData == 0)
				{
					TShock.Log.Debug("Altar smashed by {0}", args.Player.Name);
					var (altarBounds, _) = Util.GetAltarBoundsFromTile(args.X, args.Y, fluff: 1);
					var heartsOnAltar = Util.GetItemsOfType(Config.Item).Where(t => t.item.Hitbox.Intersects(altarBounds));
					if (Util.ConsumeItems(Config.ItemsRequired, heartsOnAltar, bounceLeftovers: true))
					{
						TShock.Log.Debug("Successfully consumed items from {0} stack(s)", heartsOnAltar);
						lastAltarBash = (args.Player, altarBounds.Center.ToVector2());
					}
				}
			};

			ServerApi.Hooks.NetSendData.Register(this, args =>
			{
				// Bail if we haven't tracked an altar-bash event
				if (!lastAltarBash.HasValue)
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
							var loc = Util.FindProbablySafeTeleportLocation(lastAltarBash.Value.who, tilePos);
							SpawnWarpFairy(lastAltarBash.Value.where, loc, tilePos);
						}
						else
						{
							// Play standard FX letting them know it worked
							Util.DoFairyFX(lastAltarBash.Value.where, 2);
						}

						// Stop watching, since we've caught our corruption event
						lastAltarBash = null;
					}
				}
				// If no corruption event occurs, the next outgoing packet we see should be the wraith(s) spawning
				else if (args.MsgId == PacketTypes.NpcUpdate)
				{
					// Play standard FX letting them know it worked
					Util.DoFairyFX(lastAltarBash.Value.where, 2);

					TShock.Log.Info("No corruption event was observed");
					lastAltarBash = null;
				}
			});
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

			// Attach special handling data
			RemoteClient.CheckSection(ferryFairy.target, dest);
			NPCAttachments.Add(ferryFairy, new FairyExtendedIdle()
			{
				HomePoint = pos,
				Timer = 0,
				OnCollision = player =>
				{
					// Pop the idler fairy and teleport the player
					Util.DoFairyFX(pos, 0);
					ferryFairy.active = false;

					player.Teleport(dest.X, dest.Y, TeleportationStyleID.MagicConch);
					Util.DoFairyFX(dest, 0);

					// The guide fairy doesn't actually need any special logic; we just have to set its "treasure"
					// coordinates ai[0] and ai[1] manually and initialize its state ai[2] to 3
					var guideFairy = Main.npc[NPC.NewNPC((int)dest.X, (int)dest.Y - 40, NPCID.FairyCritterPink, ai0: corruptedTile.X, ai1: corruptedTile.Y, ai2: 3)];

					// Safeguarding against the player again
					guideFairy.life = 124950;
					guideFairy.lifeMax = 124950;

					// Make sure we don't despawn before the player shows up
					guideFairy.timeLeft *= 100;
				}
			});
		}

		private void RegisterAIHandling()
		{
			ServerApi.Hooks.NpcAIUpdate.Register(this, args =>
			{
				var npc = args.Npc;

				NPCAttachments.GetValue(args.Npc)?.Let(data =>
				{
					if (data is FairyExtendedIdle extra)
					{
						// Detach special handling if the fairy's five-minute timer is up
						if (npc.ai[2] == 7)
						{
							NPCAttachments.Remove(npc);
							return;
						}

						// Prevent despawning
						if (npc.ai[3] == 200)
							npc.ai[3] = 16;

						// Move toward the home point periodically (or when the player manages to hit us) so that
						// we don't get too far out of position
						if (extra.Timer % 360 == 0 || npc.life < npc.lifeMax)
						{
							npc.ai[3] = -15;
							npc.velocity = (extra.HomePoint - npc.Center) * 0.1f;
							npc.life = npc.lifeMax;
							npc.netUpdate = true;
						}

						// On collision, run the handler and then detach the special handling
						if (extra.OnCollision is object && extra.Timer % 6 == 0)
						{
							var colliding = TShock.Players.Where(p => p is object && p.ConnectionAlive && p.TPlayer.Hitbox.Contains(extra.HomePoint.ToPoint()));

							if (colliding.Any())
							{
								colliding.ForEach(extra.OnCollision);
								NPCAttachments.Remove(npc);
							}

						}

						extra.Timer++;
					}
				});
			});
		}

	}
}
