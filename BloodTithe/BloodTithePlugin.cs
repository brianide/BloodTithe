using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
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

		public BloodTithePlugin(Main game) : base(game) { }

		public override void Initialize()
		{
			Config = BloodTitheConfig.Load();

			RegisterCorruptionTracking();
			RegisterDebugCommands();
		}

		private void RegisterCorruptionTracking()
		{
			(TSPlayer who, Vector2 where)? lastAltarBash = null;

			// Watch SendTileSquare packets for altars being broken
			TShockAPI.GetDataHandlers.TileEdit += delegate (object sender, TileEditEventArgs args)
			{
				if (args.Action == EditAction.KillTile && Main.tile[args.X, args.Y].type == TileID.DemonAltar && args.EditData == 0)
				{
					TShock.Log.Info("Altar smashed by {0}", args.Player.Name);
					var (altarPos, altarDims, _) = Util.GetAltarBoundsFromTile(args.X, args.Y, fluff: 1);
					var heartsOnAltar = Util.GetItemsOfType(Config.Item).Where(t => Collision.CheckAABBvAABBCollision(altarPos, altarDims, t.item.position, t.item.Size));
					if (Util.ConsumeItems(Config.ItemsRequired, heartsOnAltar, bounceLeftovers: true))
					{
						TShock.Log.Debug("Successfully consumed items from {0} stack(s)", heartsOnAltar);
						lastAltarBash = (args.Player, altarPos + altarDims / 2);
						lastAltarBash?.Let(bash => Util.DoFairyFX(bash.where, 0));
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
						TShock.Log.Info("Cancelled tile corruption at {0},{1} (TileID: {2})", tilePos.X, tilePos.Y, tile.type);

						// Flip the tile back on the server, and cancel the packet
						tile.type = TileID.Stone;
						args.Handled = true;

						// Stop watching, since we've caught our corruption event
						lastAltarBash = null;
					}
				}
				// If no corruption event occurs, the next outgoing packet we see should be the wraith(s) spawning
				else if (args.MsgId == PacketTypes.NpcUpdate)
				{
					TShock.Log.Info("No corruption event was observed");
					lastAltarBash = null;
				}
			});
		}

		private void RegisterDebugCommands()
		{
			Util.RegisterChatCommands("bloodtithe.debug",
				("bt_printconfig", args => args.Player.SendInfoMessage(Config.ToString())),
				("bt_needammo", args => Item.NewItem(args.Player.TPlayer.Top, Vector2.Zero, Config.Item, Stack: 30, noGrabDelay: true)));
		}

	}
}
